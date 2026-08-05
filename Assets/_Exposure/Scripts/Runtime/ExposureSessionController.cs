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
    /// Core of the prototype: drives an exposure scenario as a state machine
    /// (Onboarding -> paced breathing -> Slots 1..n -> completion), generic over the
    /// scenario-specific environment state <typeparamref name="TState"/>.
    ///
    /// - data-driven via an <see cref="ExposureScenarioDefinition{TState}"/>
    /// - seamless state transitions without removing the headset (via IEnvironmentController)
    /// - VAS anxiety prompt at slot start/end (via IAnxietyPrompt)
    /// - heart-rate monitoring with abort at threshold (via IBiosignalSource)
    /// - full session logging (via ISessionLogger)
    ///
    /// Dependencies are decoupled via interfaces -> testable and extensible. To add a new
    /// scenario, implement IEnvironmentController&lt;TNewState&gt; and derive one closed,
    /// concrete subclass of this controller (see RoomExposureSessionController,
    /// HeightExposureSessionController) -- no changes to this shared flow are needed.
    /// </summary>
    public abstract class ExposureSessionController<TState> : MonoBehaviour
    {
        [Header("Scenario")]
        [SerializeField] private ExposureScenarioDefinition<TState> scenario;
        [SerializeField] private bool startOnPlay = false;

        [Header("Dependencies (must implement the respective interfaces)")]
        [SerializeField] private MonoBehaviour environmentControllerBehaviour; // IEnvironmentController<TState>
        [SerializeField] private MonoBehaviour anxietyPromptBehaviour;         // IAnxietyPrompt
        [SerializeField] private MonoBehaviour biosignalSourceBehaviour;       // IBiosignalSource
        [SerializeField] private MonoBehaviour sessionLoggerBehaviour;         // ISessionLogger

        // --- Events for UI/audio ---
        public event Action<SessionState> OnStateChanged;
        public event Action<int, ExposureStepDefinition<TState>> OnStepChanged;
        public event Action<float, float> OnTimerTick; // (elapsed, total)

        public SessionState State { get; private set; } = SessionState.Idle;
        public int CurrentStepIndex { get; private set; } = -1;

        private IEnvironmentController<TState> _env;
        private IAnxietyPrompt _prompt;
        private IBiosignalSource _bio;
        private ISessionLogger _logger;

        private int? _lastAnswer;

        /// <summary>Environment state to apply before the first step (e.g. entry level).</summary>
        protected abstract TState DefaultState { get; }

        private void Awake()
        {
            _env    = environmentControllerBehaviour as IEnvironmentController<TState>;
            _prompt = anxietyPromptBehaviour as IAnxietyPrompt;
            _bio    = biosignalSourceBehaviour as IBiosignalSource;
            _logger = sessionLoggerBehaviour as ISessionLogger;

            if (environmentControllerBehaviour != null && _env == null)
                Debug.LogError("[Exposure] Assigned environment object does not implement IEnvironmentController.");
            if (anxietyPromptBehaviour != null && _prompt == null)
                Debug.LogError("[Exposure] Assigned prompt object does not implement IAnxietyPrompt.");
        }

        private void Start()
        {
            if (startOnPlay) StartSession();
        }

        public void StartSession()
        {
            if (scenario == null) { Debug.LogError("[Exposure] No scenario assigned."); return; }
            if (State != SessionState.Idle && State != SessionState.Completed && State != SessionState.Aborted) return;
            StopAllCoroutines();
            StartCoroutine(RunSession());
        }

        private IEnumerator RunSession()
        {
            _logger?.BeginSession(scenario.scenarioName);

            SetState(SessionState.Onboarding);
            // Hard-set entry state (participant sees the correct scene immediately).
            _env?.Apply(DefaultState, instant: true);
            yield return null;

            // Optional introductory paced breathing.
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

                // Soft-blend environment state (seamless, no headset removal).
                _env?.Apply(step.state, instant: false);
                _logger?.LogStepStart(i, step.stepId, CurrentHeartRate());

                // VAS at start.
                if (step.askAnxietyAtStart)
                {
                    yield return AskAnxiety(i, step, "start");
                    if (State == SessionState.Aborted) yield break;
                }

                // Run slot duration (with heart-rate monitoring).
                SetState(SessionState.StepActive);
                yield return RunTimer(step.durationSeconds);
                if (State == SessionState.Aborted) yield break;

                // VAS at end.
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

        /// <summary>Timer with ongoing heart-rate monitoring and abort criterion.</summary>
        private IEnumerator RunTimer(float durationSeconds)
        {
            float elapsed = 0f;
            while (elapsed < durationSeconds)
            {
                if (ShouldAbort())
                {
                    Abort($"Heart rate >= {scenario.maxHeartRateAbort} bpm");
                    yield break;
                }
                elapsed += Time.deltaTime;
                OnTimerTick?.Invoke(elapsed, durationSeconds);
                yield return null;
            }
        }

        private IEnumerator AskAnxiety(int index, ExposureStepDefinition<TState> step, string phase)
        {
            SetState(SessionState.AwaitingAnxiety);
            _lastAnswer = null;

            if (_prompt == null)
            {
                // No UI prompt assigned (e.g. blockout/editor testing) -> skip.
                _lastAnswer = -1;
            }
            else
            {
                string label = phase == "start" ? "How anxious do you feel? (Start)" : "How anxious do you feel? (End)";
                _prompt.Ask(label, v => _lastAnswer = Mathf.Clamp(v, 0, 100));
                while (_lastAnswer == null)
                {
                    if (ShouldAbort()) { Abort($"Heart rate >= {scenario.maxHeartRateAbort} bpm"); yield break; }
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
            Debug.LogWarning($"[Exposure] Aborted: {reason}");
        }

        private void SetState(SessionState s)
        {
            State = s;
            OnStateChanged?.Invoke(s);
        }
    }
}
