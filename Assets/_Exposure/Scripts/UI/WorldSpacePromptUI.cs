using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Exposure.UI
{
    /// <summary>
    /// World-space behavioural-experiment prompts, operated by hand tracking.
    ///
    /// The panel is built at runtime and placed in front of the participant only when a
    /// question is due -- two appearances per level (predict, review), nothing in between,
    /// so the exposure itself stays uninterrupted.
    ///
    /// Ratings use discrete buttons rather than sliders on purpose: dragging a slider
    /// handle with tracked hands is unreliable, while poking a button is not.
    ///
    /// Visual styling is intentionally plain; swapping in the VR-template panel look is a
    /// later step and needs no change to the flow.
    /// </summary>
    public class WorldSpacePromptUI : MonoBehaviour, IPredictionPrompt
    {
        [Header("Placement")]
        [Tooltip("Head transform the panel is positioned in front of. Falls back to Camera.main.")]
        [SerializeField] private Transform head;
        [SerializeField, Min(0.3f)] private float distanceFromHead = 0.8f;
        [SerializeField] private float verticalOffset = -0.1f;
        [Tooltip("Used instead of the tracked head height while it looks implausible (e.g. before the XR pose has settled after session start).")]
        [SerializeField] private float fallbackEyeHeight = 1.6f;
        [Tooltip("Head heights below this are treated as not-yet-tracked and get the fallback instead.")]
        [SerializeField] private float minPlausibleHeadHeight = 1.0f;
        [Tooltip("How long to keep watching for a first plausible head pose after a panel appears. " +
                 "Once one arrives the panel is placed for good and stops following the head.")]
        [SerializeField] private float settleTimeoutSeconds = 10f;

        [Header("Panel")]
        [SerializeField] private Vector2 panelSize = new Vector2(0.7f, 0.5f);
        [SerializeField] private Color panelColor = new Color(0.06f, 0.09f, 0.11f, 0.94f);
        [SerializeField] private Color buttonColor = new Color(0.16f, 0.36f, 0.44f, 1f);
        [SerializeField] private Color textColor = Color.white;

        private static readonly int[] RatingSteps = { 0, 25, 50, 75, 100 };

        private Canvas _canvas;
        private RectTransform _root;
        private Text _title;
        private RectTransform _buttonArea;
        private readonly List<GameObject> _buttons = new List<GameObject>();

        private Coroutine _settleRoutine;
        private bool _placedFromValidPose;

        private Transform Head
        {
            get
            {
                if (head == null && Camera.main != null) head = Camera.main.transform;
                return head;
            }
        }

        private void Awake()
        {
            BuildCanvas();
            Hide();
        }

        // ---------------------------------------------------------------- IPredictionPrompt

public void AskPrediction(FearedOutcomeCatalog catalog, Action<Prediction> onAnswered)
        {
            if (catalog == null || catalog.outcomes.Count == 0)
            {
                onAnswered?.Invoke(new Prediction { outcomeId = "none", convictionPercent = -1 });
                return;
            }

            ShowOutcomeChoice(catalog, chosenId =>
                ShowRating(UIText.Get("conviction_before_question"), percent =>
                {
                    Hide();
                    onAnswered?.Invoke(new Prediction { outcomeId = chosenId, convictionPercent = percent });
                }));
        }

public void AskOutcome(FearedOutcomeCatalog catalog, Prediction prediction, Action<OutcomeReport> onAnswered)
        {
            string predictedText = TextForOutcome(catalog, prediction.outcomeId);

            ShowYesNo(UIText.Get("outcome_question", predictedText), occurred =>
                ShowRating(UIText.Get("conviction_after_question"), convictionAfter =>
                    ShowRating(UIText.Get("anxiety_question"), anxiety =>
                    {
                        Hide();
                        onAnswered?.Invoke(new OutcomeReport
                        {
                            occurred = occurred,
                            convictionPercent = convictionAfter,
                            anxiety0to100 = anxiety
                        });
                    })));
        }

        // ---------------------------------------------------------------- public helpers

        /// <summary>Simple confirmation panel, used for the ready screen between levels.</summary>
        public void ShowConfirm(string message, string buttonLabel, Action onConfirmed)
        {
            Show();
            _title.text = message;
            ClearButtons();
            AddButton(buttonLabel, () => { Hide(); onConfirmed?.Invoke(); });
        }

        /// <summary>Read-only panel, used for the end-of-session summary.</summary>
        public void ShowMessage(string message, string buttonLabel, Action onDismissed)
            => ShowConfirm(message, buttonLabel, onDismissed);

        // ---------------------------------------------------------------- panel states

private void ShowOutcomeChoice(FearedOutcomeCatalog catalog, Action<string> onChosen)
        {
            Show();
            _title.text = UIText.Get("predict_question");
            ClearButtons();
            foreach (var outcome in catalog.outcomes)
            {
                var captured = outcome.id;
                AddButton(outcome.text, () => onChosen?.Invoke(captured));
            }
        }

private void ShowYesNo(string question, Action<bool> onAnswered)
        {
            Show();
            _title.text = question;
            ClearButtons();
            AddButton(UIText.Get("outcome_yes"), () => onAnswered?.Invoke(true));
            AddButton(UIText.Get("outcome_no"), () => onAnswered?.Invoke(false));
        }

        private void ShowRating(string question, Action<int> onAnswered)
        {
            Show();
            _title.text = question;
            ClearButtons();
            foreach (int step in RatingSteps)
            {
                int captured = step;
                AddButton($"{step} %", () => onAnswered?.Invoke(captured));
            }
        }

        // ---------------------------------------------------------------- construction

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("ExposurePromptCanvas", typeof(Canvas), typeof(CanvasScaler),
                                          typeof(GraphicRaycaster), typeof(TrackedDeviceGraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;

            _root = canvasGo.GetComponent<RectTransform>();
            _root.sizeDelta = new Vector2(700f, 500f);
            _root.localScale = Vector3.one * (panelSize.x / 700f);

            var background = NewImage("Background", _root, panelColor);
            Stretch(background.rectTransform);

            var titleGo = new GameObject("Title", typeof(Text));
            titleGo.transform.SetParent(_root, false);
            _title = titleGo.GetComponent<Text>();
            _title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _title.fontSize = 30;
            _title.color = textColor;
            _title.alignment = TextAnchor.UpperCenter;
            _title.horizontalOverflow = HorizontalWrapMode.Wrap;
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0.68f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(30f, 0f);
            titleRect.offsetMax = new Vector2(-30f, -25f);

            var areaGo = new GameObject("Buttons", typeof(VerticalLayoutGroup));
            areaGo.transform.SetParent(_root, false);
            _buttonArea = areaGo.GetComponent<RectTransform>();
            _buttonArea.anchorMin = new Vector2(0f, 0f);
            _buttonArea.anchorMax = new Vector2(1f, 0.66f);
            _buttonArea.offsetMin = new Vector2(30f, 25f);
            _buttonArea.offsetMax = new Vector2(-30f, 0f);

            var layout = areaGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
        }

        private void AddButton(string label, Action onClick)
        {
            var go = new GameObject("Button", typeof(Image), typeof(Button));
            go.transform.SetParent(_buttonArea, false);

            go.GetComponent<Image>().color = buttonColor;

            var textGo = new GameObject("Label", typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 26;
            text.color = textColor;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.text = label;
            Stretch(textGo.GetComponent<RectTransform>());

            go.GetComponent<Button>().onClick.AddListener(() => onClick?.Invoke());
            _buttons.Add(go);
        }

        private void ClearButtons()
        {
            foreach (var b in _buttons) Destroy(b);
            _buttons.Clear();
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // ---------------------------------------------------------------- visibility

private void Show()
        {
            // Each new panel is placed once, in front of wherever the participant is looking.
            _placedFromValidPose = false;
            PlaceInFrontOfHead();
            _canvas.gameObject.SetActive(true);

            // A panel shown immediately after session start (or right after the headset is put
            // on) can land using a not-yet-valid head pose. Watch only until the first plausible
            // pose arrives, then stop -- a panel that keeps re-centring while being read is
            // worse than one placed slightly off.
            if (_settleRoutine != null) StopCoroutine(_settleRoutine);
            if (!_placedFromValidPose)
                _settleRoutine = StartCoroutine(PlaceOnceHeadPoseBecomesValid());
        }




private void Hide()
        {
            ClearButtons();
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            if (_settleRoutine != null)
            {
                StopCoroutine(_settleRoutine);
                _settleRoutine = null;
            }
        }

private void PlaceInFrontOfHead()
        {
            if (Head == null) return;
            Vector3 forward = Head.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();

            // Until the tracked pose is valid, use a plausible eye height rather than sinking
            // the panel into the floor.
            bool valid = HasPlausibleHeadPose();
            float headY = valid ? Head.position.y : fallbackEyeHeight;
            _placedFromValidPose = valid;

            Vector3 headPos = new Vector3(Head.position.x, headY, Head.position.z);

            _canvas.transform.position = headPos + forward * distanceFromHead + Vector3.up * verticalOffset;
            _canvas.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

private IEnumerator PlaceOnceHeadPoseBecomesValid()
        {
            float waited = 0f;
            while (waited < settleTimeoutSeconds)
            {
                if (HasPlausibleHeadPose())
                {
                    PlaceInFrontOfHead();
                    break;
                }
                waited += Time.deltaTime;
                yield return null;
            }
            _settleRoutine = null;
        }

        /// <summary>
        /// A head height near the floor means the tracked pose has not initialised yet -- the
        /// participant is not actually lying on the ground.
        /// </summary>
        private bool HasPlausibleHeadPose()
            => Head != null && Head.position.y >= minPlausibleHeadHeight;


        private static string TextForOutcome(FearedOutcomeCatalog catalog, string id)
        {
            if (catalog == null) return id;
            foreach (var o in catalog.outcomes)
                if (o.id == id) return o.text;
            return id;
        }
    }
}
