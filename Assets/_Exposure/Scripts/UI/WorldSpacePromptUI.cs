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
        [Tooltip("Metres in front of the head. Must stay inside comfortable poking range -- a " +
                 "panel you have to lean towards is one you keep missing.")]
        [SerializeField, Min(0.3f)] private float distanceFromHead = 0.5f;
        [Tooltip("How far below eye level the panel sits. Comfortably below the horizon, so " +
                 "reading it does not compete with looking out over the edge.")]
        [SerializeField] private float verticalOffset = -0.18f;
        [Tooltip("Used instead of the tracked head height while it looks implausible (e.g. before the XR pose has settled after session start).")]
        [SerializeField] private float fallbackEyeHeight = 1.6f;
        [Tooltip("Head heights below this are treated as not-yet-tracked and get the fallback instead.")]
        [SerializeField] private float minPlausibleHeadHeight = 1.0f;
        [Tooltip("How long to keep watching for a first plausible head pose after a panel appears, " +
                 "before giving up and leaving it where it is.")]
        [SerializeField, Min(0f)] private float settleTimeoutSeconds = 10f;

        [Tooltip("Buttons ignore presses for this long after a panel appears, so a poke meant " +
                 "for the previous panel cannot carry over into the next one.")]
        [SerializeField, Min(0f)] private float inputCooldownSeconds = 0.45f;

        [Header("Panel")]
        [Tooltip("Physical size in metres. Large enough that eight feared-outcome options stay " +
                 "readable, since that is the panel with the most text on it.")]
        [SerializeField] private Vector2 panelSize = new Vector2(0.85f, 0.64f);
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
        public void ShowChoice(string message, ChoiceOption[] options, Action<int> onChosen)
        {
            Show();
            _title.text = message;
            ClearButtons();
            for (int i = 0; i < options.Length; i++)
            {
                int idx = i;
                var option = options[i];

                if (option.enabled)
                {
                    AddButton(option.label, () => onChosen?.Invoke(idx));
                }
                else
                {
                    string label = string.IsNullOrEmpty(option.lockedHint)
                        ? option.label
                        : $"{option.label}\n{option.lockedHint}";
                    AddLockedButton(label);
                }
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
            _title.color = textColor;
            _title.alignment = TextAnchor.UpperCenter;
            _title.horizontalOverflow = HorizontalWrapMode.Wrap;
            // Questions range from four words to a full sentence plus a quoted outcome, so the
            // title shrinks to fit rather than being clipped at whichever size suits one of them.
            _title.resizeTextForBestFit = true;
            _title.resizeTextMinSize = 14;
            _title.resizeTextMaxSize = 34;
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
            text.color = textColor;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.text = label;
            FitLabel(text);
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
        /// <summary>
        /// Lets a label shrink to fit its button instead of being clipped.
        ///
        /// Button height is whatever is left after dividing the panel between however many
        /// options there are, so a fixed font size can only ever be right for one panel. The
        /// eight feared outcomes are full sentences in the smallest buttons, which is exactly
        /// where a fixed size silently swallowed half the text -- and an option you cannot read
        /// is an option you cannot choose.
        /// </summary>
        private static void FitLabel(Text text)
        {
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 12;
            text.resizeTextMaxSize = 32;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        /// <summary>
        /// A greyed, unclickable entry. Carries no Button at all rather than a disabled one:
        /// a disabled Button still takes the poke and swallows it, which feels like the panel
        /// is broken rather than like the option is not open yet.
        /// </summary>
        private void AddLockedButton(string label)
        {
            var go = new GameObject("LockedButton", typeof(Image));
            go.transform.SetParent(_buttonArea, false);

            var dimmed = buttonColor;
            dimmed.a = 0.28f;
            go.GetComponent<Image>().color = dimmed;

            var textGo = new GameObject("Label", typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = mutedTextColor;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.text = label;
            FitLabel(text);
            Stretch(textGo.GetComponent<RectTransform>());

            _buttons.Add(go);
        }

        private Slider AddSlider(string lowLabel, string midLabel, string highLabel)
        {
            var sliderGo = new GameObject("Rating", typeof(Image), typeof(Slider), typeof(LayoutElement));
            sliderGo.transform.SetParent(_buttonArea, false);
            _buttons.Add(sliderGo);
            // A tall track, because the whole bar is the poke target with tracked hands -- a
            // thin line is something you miss more often than you hit.
            sliderGo.GetComponent<LayoutElement>().preferredHeight = 130f;
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

            // Tick marks, one per whole step. The scale is discrete -- 0 to 10 -- but a smooth
            // bar hides that, so people read it as "somewhere around here" and the recorded
            // number is finer than the judgement behind it. Ticks make the steps visible and the
            // middle one is drawn taller, because "neither" is the answer people aim at most.
            for (int i = 0; i <= 10; i++)
            {
                bool isMiddle = i == 5;
                var tick = NewImage($"Tick_{i}", sliderGo.transform,
                                    isMiddle ? handleColor : mutedTextColor);
                var tr = tick.rectTransform;
                float x = i / 10f;
                tr.anchorMin = new Vector2(x, isMiddle ? 0.12f : 0.24f);
                tr.anchorMax = new Vector2(x, isMiddle ? 0.88f : 0.76f);
                tr.sizeDelta = new Vector2(isMiddle ? 5f : 3f, 0f);
                tr.anchoredPosition = new Vector2(24f * (1f - 2f * x), 0f); // stay inside the end caps
            }

            var handleArea = NewRect("Handle Slide Area", sliderGo.transform);
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(24f, 0f);
            handleArea.offsetMax = new Vector2(-24f, 0f);

            var handle = NewImage("Handle", handleArea, handleColor);
            var handleRect = handle.rectTransform;
            handleRect.anchorMin = new Vector2(0f, 0f);
            handleRect.anchorMax = new Vector2(0f, 1f);
            handleRect.sizeDelta = new Vector2(90f, 0f); // wide: it is poked, not pinched

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

        // The panel does not follow the head at all -- there is deliberately no Update().
        //
        // It tracked head height for a while, on the theory that only horizontal following
        // could make it flee from a reaching finger. Testing said otherwise: leaning in to poke
        // also changes head height, so the panel still drifted under the finger and touch stayed
        // unreliable. A target that does not move is one you can actually hit. It is placed once,
        // in front of and slightly below the eyes, and stays there.

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
