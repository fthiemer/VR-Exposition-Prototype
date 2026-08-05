using System;
using System.Collections;
using UnityEngine;

namespace Exposure
{
    public enum SessionState
    {
        Idle,
        Onboarding,
        PacedBreathing,
        StepActive,
        AwaitingAnxiety,
        Completed,
        Aborted
    }

    /// <summary>
    /// Herzstück des Prototyps: steuert den Ablauf eines Expositions-Szenarios als
    /// State Machine (Onboarding -> Taktatmung -> Slots 1..n -> Abschluss).
    ///
    /// - datengetrieben über ein <see cref="ExposureScenarioDefinition"/>
    /// - nahtlose Zustandswechsel ohne Brille-Absetzen (via IEnvironmentController)
    /// - VAS-Angstabfrage zu Slot-Beginn/-Ende (via IAnxietyPrompt)
    /// - HF-Überwachung mit Abbruch bei Grenzwert (via IBiosignalSource)
    /// - vollständige Protokollierung (via ISessionLogger)
    ///
    /// Die Abhängigkeiten sind als Interfaces entkoppelt -> gut testbar & erweiterbar.
    /// </summary>
    public class ExposureSessionController : MonoBehaviour
    {
        [Header("Szenario")]
        [SerializeField] private ExposureScenarioDefinition scenario;
        [SerializeField] private bool startOnPlay = false;

        [Header("Abhängigkeiten (müssen die jeweiligen Interfaces implementieren)")]
        [SerializeField] private MonoBehaviour environmentControllerBehaviour; // IEnvironmentController
        [SerializeField] private MonoBehaviour anxietyPromptBehaviour;         // IAnxietyPrompt
        [SerializeField] private MonoBehaviour biosignalSourceBehaviour;       // IBiosignalSource
        [SerializeField] private MonoBehaviour sessionLoggerBehaviour;         // ISessionLogger

        // --- Events für UI/Audio ---
        public event Action<SessionState> OnStateChanged;
        public event Action<int, ExposureStepDefinition> OnStepChanged;
        public event Action<float, float> OnTimerTick; // (verstrichen, gesamt)

        public SessionState State { get; private set; } = SessionState.Idle;
        public int CurrentStepIndex { get; private set; } = -1;

        private IEnvironmentController _env;
        private IAnxietyPrompt _prompt;
        private IBiosignalSource _bio;
        private ISessionLogger _logger;

        private int? _lastAnswer;

        private void Awake()
        {
            _env    = environmentControllerBehaviour as IEnvironmentController;
            _prompt = anxietyPromptBehaviour as IAnxietyPrompt;
            _bio    = biosignalSourceBehaviour as IBiosignalSource;
            _logger = sessionLoggerBehaviour as ISessionLogger;

            if (environmentControllerBehaviour != null && _env == null)
                Debug.LogError("[Exposure] Zugewiesenes Environment-Objekt implementiert IEnvironmentController nicht.");
            if (anxietyPromptBehaviour != null && _prompt == null)
                Debug.LogError("[Exposure] Zugewiesenes Prompt-Objekt implementiert IAnxietyPrompt nicht.");
        }

        private void Start()
        {
            if (startOnPlay) StartSession();
        }

        public void StartSession()
        {
            if (scenario == null) { Debug.LogError("[Exposure] Kein Szenario zugewiesen."); return; }
            if (State != SessionState.Idle && State != SessionState.Completed && State != SessionState.Aborted) return;
            StopAllCoroutines();
            StartCoroutine(RunSession());
        }

        private IEnumerator RunSession()
        {
            _logger?.BeginSession(scenario.scenarioName);

            SetState(SessionState.Onboarding);
            // Startzustand hart setzen (Person sieht sofort korrekten Raum).
            _env?.Apply(RoomState.Default, instant: true);
            yield return null;

            // Optionale einleitende Taktatmung.
            if (scenario.pacedBreathingSeconds > 0f)
            {
                SetState(SessionState.PacedBreathing);
                yield return RunTimer(scenario.pacedBreathingSeconds);
                if (State == SessionState.Aborted) yield break;
            }

            for (int i = 0; i < scenario.steps.Count; i++)
            {
                var step = scenario.steps[i];
                if (step == null) continue;

                CurrentStepIndex = i;
                OnStepChanged?.Invoke(i, step);

                // Raumzustand weich überblenden (nahtlos, ohne Brille abzusetzen).
                _env?.Apply(step.roomState, instant: false);
                _logger?.LogStepStart(i, step.stepId, CurrentHeartRate());

                // VAS zu Beginn.
                if (step.askAnxietyAtStart)
                {
                    yield return AskAnxiety(i, step, "start");
                    if (State == SessionState.Aborted) yield break;
                }

                // Slot-Dauer laufen lassen (mit HF-Überwachung).
                SetState(SessionState.StepActive);
                yield return RunTimer(step.durationSeconds);
                if (State == SessionState.Aborted) yield break;

                // VAS am Ende.
                if (step.askAnxietyAtEnd)
                {
                    yield return AskAnxiety(i, step, "end");
                    if (State == SessionState.Aborted) yield break;
                }

                _logger?.LogStepEnd(i, step.stepId, CurrentHeartRate());
            }

            SetState(SessionState.Completed);
            _logger?.EndSession();
        }

        /// <summary>Timer mit laufender HF-Überwachung und Abbruchkriterium.</summary>
        private IEnumerator RunTimer(float durationSeconds)
        {
            float elapsed = 0f;
            while (elapsed < durationSeconds)
            {
                if (ShouldAbort())
                {
                    Abort($"Herzfrequenz >= {scenario.maxHeartRateAbort} bpm");
                    yield break;
                }
                elapsed += Time.deltaTime;
                OnTimerTick?.Invoke(elapsed, durationSeconds);
                yield return null;
            }
        }

        private IEnumerator AskAnxiety(int index, ExposureStepDefinition step, string phase)
        {
            SetState(SessionState.AwaitingAnxiety);
            _lastAnswer = null;

            if (_prompt == null)
            {
                // Ohne UI-Prompt (z. B. im Editor-Test) überspringen.
                _lastAnswer = -1;
            }
            else
            {
                string label = phase == "start" ? "Wie stark ist Ihre Angst? (Beginn)" : "Wie stark ist Ihre Angst? (Ende)";
                _prompt.Ask(label, v => _lastAnswer = Mathf.Clamp(v, 0, 100));
                while (_lastAnswer == null)
                {
                    if (ShouldAbort()) { Abort($"Herzfrequenz >= {scenario.maxHeartRateAbort} bpm"); yield break; }
                    yield return null;
                }
            }

            _logger?.LogAnxiety(index, step.stepId, phase, _lastAnswer ?? -1, CurrentHeartRate());
        }

        private bool ShouldAbort()
        {
            if (_bio == null || !_bio.HasSignal) return false;
            return _bio.CurrentHeartRate >= scenario.maxHeartRateAbort;
        }

        private float CurrentHeartRate() => _bio != null ? _bio.CurrentHeartRate : 0f;

        private void Abort(string reason)
        {
            _logger?.LogAbort(reason, CurrentHeartRate());
            _logger?.EndSession();
            SetState(SessionState.Aborted);
            Debug.LogWarning($"[Exposure] Abbruch: {reason}");
        }

        private void SetState(SessionState s)
        {
            State = s;
            OnStateChanged?.Invoke(s);
        }
    }
}
