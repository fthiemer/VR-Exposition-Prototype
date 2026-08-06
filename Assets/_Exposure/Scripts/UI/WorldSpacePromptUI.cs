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
    /// question is due -- expectancy on the ground before the first task, review on the
    /// ground after the last one, plus the floor/task choices in between -- so the height
    /// exposure itself stays uninterrupted.
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
        [Tooltip("How long to keep watching for a first plausible head pose after a panel appears, " +
                 "before giving up and leaving it where it is.")]
        [SerializeField, Min(0f)] private float settleTimeoutSeconds = 10f;

        [Tooltip("How fast the panel matches head height. Height is the only thing it tracks -- " +
                 "following horizontally would make it retreat from a finger reaching for it.")]
        [SerializeField, Min(0.1f)] private float followSmoothing = 3.5f;

        [Tooltip("Buttons ignore presses for this long after a panel appears, so a poke meant " +
                 "for the previous panel cannot carry over into the next one.")]
        [SerializeField, Min(0f)] private float inputCooldownSeconds = 0.45f;

        [Header("Panel")]
        [SerializeField] private Vector2 panelSize = new Vector2(0.7f, 0.5f);
        [SerializeField] private Color panelColor = new Color(0.06f, 0.09f, 0.11f, 0.94f);
        [SerializeField] private Color buttonColor = new Color(0.16f, 0.36f, 0.44f, 1f);
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private Color mutedTextColor = new Color(0.72f, 0.78f, 0.82f, 1f);
        [SerializeField] private Color trackColor = new Color(0.10f, 0.15f, 0.18f, 1f);
        [SerializeField] private Color handleColor = new Color(0.85f, 0.93f, 0.97f, 1f);


        private Canvas _canvas;
        private RectTransform _root;
        private Text _title;
        private RectTransform _buttonArea;
        private readonly List<GameObject> _buttons = new List<GameObject>();

        private Coroutine _settleRoutine;
        private bool _placedFromValidPose;
        private bool _visible;
        private float _inputBlockedUntil;

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

public void AskExpectancyBefore(FearedOutcomeCatalog catalog, Action<Prediction> onAnswered)
        {
            if (catalog == null || catalog.outcomes.Count == 0)
            {
                onAnswered?.Invoke(new Prediction { outcomeId = "none", expectancy0to10 = -1 });
                return;
            }

            ShowOutcomeChoice(catalog, chosenId =>
                ShowRating(UIText.Get("expectancy_before_question"),
                           UIText.Get("scale_expectancy_low"),
                           UIText.Get("scale_expectancy_mid"),
                           UIText.Get("scale_expectancy_high"),
                           value =>
                {
                    Hide();
                    onAnswered?.Invoke(new Prediction { outcomeId = chosenId, expectancy0to10 = value });
                }));
        }

public void AskOutcome(FearedOutcomeCatalog catalog, Prediction prediction, Action<OutcomeReport> onAnswered)
        {
            string predictedText = TextForOutcome(catalog, prediction.outcomeId);

            ShowRating(UIText.Get("outcome_occurred_question", predictedText),
                       UIText.Get("scale_occurred_low"),
                       UIText.Get("scale_occurred_mid"),
                       UIText.Get("scale_occurred_high"),
                       occurred =>
                ShowRating(UIText.Get("expectancy_after_question"),
                           UIText.Get("scale_expectancy_low"),
                           UIText.Get("scale_expectancy_mid"),
                           UIText.Get("scale_expectancy_high"),
                           expectancyAfter =>
                {
                    Hide();
                    onAnswered?.Invoke(new OutcomeReport { occurred0to10 = occurred, expectancy0to10 = expectancyAfter });
                }));
        }

        /// <summary>Generic labelled-button choice, used for floor selection and the post-task menu.</summary>
        public void ShowChoice(string message, string[] labels, Action<int> onChosen)
        {
            Show();
            _title.text = message;
            ClearButtons();
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                AddButton(labels[i], () => onChosen?.Invoke(idx));
            }
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

/// <summary>
        /// Rating on a labelled 0-100 scale. A slider rather than discrete percentage buttons,
        /// because naming both ends and the middle is what tells the participant what the
        /// number means -- "75 %" on its own asks them to invent a scale. The percentage is
        /// still what gets recorded, it is just read off the slider instead of picked.
        ///
        /// Poke-driven rather than grab-and-drag, which is the interaction that is actually
        /// unreliable with tracked hands.
        /// </summary>
        private void ShowRating(string question, string lowLabel, string midLabel, string highLabel,
                                Action<int> onAnswered)
        {
            Show();
            _title.text = question;
            ClearButtons();

            var slider = AddSlider(lowLabel, midLabel, highLabel);
            AddButton(UIText.Get("rating_confirm"),
                      () => onAnswered?.Invoke(Mathf.RoundToInt(slider.value)));
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

            go.GetComponent<Button>().onClick.AddListener(() =>
            {
                // Swallow presses that arrive during the cooldown after a panel change.
                if (Time.time < _inputBlockedUntil) return;
                onClick?.Invoke();
            });
            _buttons.Add(go);
        }

/// <summary>
        /// Builds a 0-100 slider plus the three scale anchors underneath it. The handle is
        /// deliberately wide: it is poked, not pinched, so it needs to be an easy target.
        /// </summary>
        private Slider AddSlider(string lowLabel, string midLabel, string highLabel)
        {
            var sliderGo = new GameObject("Rating", typeof(Image), typeof(Slider), typeof(LayoutElement));
            sliderGo.transform.SetParent(_buttonArea, false);
            _buttons.Add(sliderGo);
            sliderGo.GetComponent<LayoutElement>().preferredHeight = 70f;
            sliderGo.GetComponent<Image>().color = trackColor;

            var fillArea = NewRect("Fill Area", sliderGo.transform);
            fillArea.anchorMin = new Vector2(0f, 0.25f);
            fillArea.anchorMax = new Vector2(1f, 0.75f);
            fillArea.offsetMin = new Vector2(24f, 0f);
            fillArea.offsetMax = new Vector2(-24f, 0f);

            var fill = NewImage("Fill", fillArea, buttonColor);
            var fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillRect.sizeDelta = new Vector2(24f, 0f);

            var handleArea = NewRect("Handle Slide Area", sliderGo.transform);
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(24f, 0f);
            handleArea.offsetMax = new Vector2(-24f, 0f);

            var handle = NewImage("Handle", handleArea, handleColor);
            var handleRect = handle.rectTransform;
            handleRect.anchorMin = new Vector2(0f, 0f);
            handleRect.anchorMax = new Vector2(0f, 1f);
            handleRect.sizeDelta = new Vector2(48f, 0f);

            var slider = sliderGo.GetComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 10f;
            slider.wholeNumbers = true;
            // Starts in the middle so neither end is suggested as the expected answer.
            slider.value = 5f;

            var labels = new GameObject("ScaleLabels", typeof(RectTransform), typeof(LayoutElement));
            labels.transform.SetParent(_buttonArea, false);
            _buttons.Add(labels);
            labels.GetComponent<LayoutElement>().preferredHeight = 40f;
            var labelsRect = labels.GetComponent<RectTransform>();

            AddScaleLabel(labelsRect, lowLabel, TextAnchor.MiddleLeft, 0f, 0.34f);
            AddScaleLabel(labelsRect, midLabel, TextAnchor.MiddleCenter, 0.33f, 0.67f);
            AddScaleLabel(labelsRect, highLabel, TextAnchor.MiddleRight, 0.66f, 1f);

            return slider;
        }

        private void AddScaleLabel(RectTransform parent, string text, TextAnchor alignment,
                                   float anchorMinX, float anchorMaxX)
        {
            var go = new GameObject("Label", typeof(Text));
            go.transform.SetParent(parent, false);

            var label = go.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 20;
            label.color = mutedTextColor;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.text = text;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(anchorMinX, 0f);
            rect.anchorMax = new Vector2(anchorMaxX, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
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
            _placedFromValidPose = false;
            PlaceInFrontOfHead();
            _canvas.gameObject.SetActive(true);
            _visible = true;

            // Every new panel starts unresponsive for a moment. Without this, a poke aimed at
            // the previous panel lands on whichever button now occupies that spot -- which is
            // how someone answers a question they never saw.
            _inputBlockedUntil = Time.time + inputCooldownSeconds;

            if (_settleRoutine != null) StopCoroutine(_settleRoutine);
            if (!_placedFromValidPose)
                _settleRoutine = StartCoroutine(PlaceOnceHeadPoseBecomesValid());
        }




private void Hide()
        {
            ClearButtons();
            _visible = false;
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            if (_settleRoutine != null)
            {
                StopCoroutine(_settleRoutine);
                _settleRoutine = null;
            }
        }

/// <summary>
        /// Keeps the panel at a constant distance without making it swim.
        ///
        /// Placing it once and leaving it there meant that walking towards the edge -- which is
        /// the whole task -- left the panel behind, so it drifted out of comfortable reading and
        /// poking range. Following the head every frame fixes that but makes the panel feel like
        /// it is attached to your face. The dead zone is the compromise: it holds still while you
        /// are roughly where you were, and glides back once you have actually moved away.
        /// </summary>
/// <summary>
        /// The panel holds the position and rotation it was given, and only tracks head
        /// *height*.
        ///
        /// It must not follow the head horizontally: poking at it pushes the head forward
        /// slightly, and a panel that reacts to that retreats from the finger -- it flees
        /// exactly when someone is trying to press it. Height is safe because standing taller
        /// or crouching never happens as a side effect of reaching.
        /// </summary>
        private void Update()
        {
            if (!_visible || _canvas == null || Head == null) return;
            if (!HasPlausibleHeadPose()) return;

            float targetY = Head.position.y + verticalOffset;
            var p = _canvas.transform.position;
            if (Mathf.Abs(p.y - targetY) < 0.02f) return;

            p.y = Mathf.Lerp(p.y, targetY, 1f - Mathf.Exp(-followSmoothing * Time.deltaTime));
            _canvas.transform.position = p;
        }

        /// <summary>Where the panel would ideally sit for the current head pose.</summary>
        private void ComputeTargetPose(out Vector3 position, out Quaternion rotation)
        {
            Vector3 forward = Head.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();

            float headY = HasPlausibleHeadPose() ? Head.position.y : fallbackEyeHeight;
            var headPos = new Vector3(Head.position.x, headY, Head.position.z);

            position = headPos + forward * distanceFromHead + Vector3.up * verticalOffset;
            rotation = Quaternion.LookRotation(forward, Vector3.up);
        }


private void PlaceInFrontOfHead()
        {
            if (Head == null) return;

            _placedFromValidPose = HasPlausibleHeadPose();
            ComputeTargetPose(out var position, out var rotation);

            _canvas.transform.position = position;
            _canvas.transform.rotation = rotation;
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
