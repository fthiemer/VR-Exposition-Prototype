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
        SessionComplete,
        Completed,
        Aborted
    }

    /// <summary>
    /// Core of the prototype: drives an exposure scenario as a state machine
    /// (Onboarding -> paced breathing -> Levels 1..n -> completion), generic over the
    /// scenario-specific environment state <typeparamref name="TState"/>.
    ///
    /// - data-driven via an <see cref="ExposureScenarioDefinition{TState}"/>
    /// - seamless state transitions without removing the headset (via IEnvironmentController)
    /// - VAS anxiety prompt (via IAnxietyPrompt), either at step start/end (fixed-duration
    ///   levels) or repeatedly during a habituation-gated level (see RunHabituationGate)
    /// - heart-rate monitoring with abort at threshold (via IBiosignalSource)
    /// - full session logging (via ISessionLogger)
    /// - optional multi-session delivery: a scenario can span multiple separate sittings
    ///   (ExposureScenarioDefinition.maxSessions/maxSessionMinutes), resuming at the level
    ///   last reached -- single-sitting scenarios are unaffected (default maxSessions = 1)
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

        /// <summary>1-based index of the current sitting (relevant for multi-session scenarios).</summary>
        public int CurrentSessionNumber { get; private set; } = 1;

        private IEnvironmentController<TState> _env;
        private IAnxietyPrompt _prompt;
        private IBiosignalSource _bio;
        private ISessionLogger _logger;

        private int? _lastAnswer;
        private int _resumeStepIndex;
        private float _lastStepElapsedSeconds;

        /// <summary>Environment state to apply before the very first step (session 1 only).</summary>
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

        /// <summary>Starts the first sitting, or resumes the next one after SessionComplete.</summary>
        public void StartSession()
        {
            if (scenario == null) { Debug.LogError("[Exposure] No scenario assigned."); return; }
            bool canStart = State == SessionState.Idle || State == SessionState.Completed
                            || State == SessionState.Aborted || State == SessionState.SessionComplete;
            if (!canStart) return;
            StopAllCoroutines();
            StartCoroutine(RunSession());
        }

        /// <summary>Clears all progress so the next StartSession() begins at session 1, level 0.</summary>
        public void ResetProgress()
        {
            StopAllCoroutines();
            _resumeStepIndex = 0;
            CurrentSessionNumber = 1;
            CurrentStepIndex = -1;
            SetState(SessionState.Idle);
        }

        private IEnumerator RunSession()
        {
            bool firstSitting = _resumeStepIndex == 0 && CurrentSessionNumber == 1;

            if (firstSitting)
            {
                _logger?.BeginSession(scenario.scenarioName);
                SetState(SessionState.Onboarding);
                // Hard-set entry state (participant sees the correct scene immediately).
                _env?.Apply(DefaultState, instant: true);
                yield return null;

                // Optional introductory paced breathing (single-sitting scenarios only).
                if (scenario.pacedBreathingSeconds > 0f)
                {
                    SetState(SessionState.PacedBreathing);
                    yield return RunTimer(scenario.pacedBreathingSeconds);
                    if (State == SessionState.Aborted) yield break;
                }
            }
            else
            {
                _logger?.BeginSession($"{scenario.scenarioName} (session {CurrentSessionNumber})");
                SetState(SessionState.Onboarding);
                // Resume: hard-set to the level reached in the previous sitting.
                _env?.Apply(scenario.steps[_resumeStepIndex].state, instant: true);
                yield return null;
            }

            float sessionElapsed = 0f;

            for (int i = _resumeStepIndex; i < scenario.steps.Count; i++)
            {
                var step = scenario.steps[i];
                if (step == null) continue;

                CurrentStepIndex = i;
                _resumeStepIndex = i; // if aborted mid-level, resume here rather than re-doing passed levels
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

                SetState(SessionState.StepActive);
                if (step.habituationGated)
                {
                    yield return RunHabituationGate(i, step);
                }
                else
                {
                    yield return RunTimer(step.durationSeconds);
                    _lastStepElapsedSeconds = step.durationSeconds;
                }
                if (State == SessionState.Aborted) yield break;

                // VAS at end.
                if (step.askAnxietyAtEnd)
                {
                    yield return AskAnxiety(i, step, "end");
                    if (State == SessionState.Aborted) yield break;
                }

                _logger?.LogStepEnd(i, step.stepId, CurrentHeartRate());
                sessionElapsed += _lastStepElapsedSeconds;

                bool moreLevels = i + 1 < scenario.steps.Count;
                bool sessionBudgetReached = scenario.maxSessionMinutes > 0f && sessionElapsed >= scenario.maxSessionMinutes * 60f;
                bool moreSittingsAvailable = CurrentSessionNumber < scenario.maxSessions;

                if (moreLevels && sessionBudgetReached && moreSittingsAvailable)
                {
                    _resumeStepIndex = i + 1;
                    CurrentSessionNumber++;
                    _logger?.EndSession();
                    SetState(SessionState.SessionComplete);
                    yield break;
                }
            }

            _resumeStepIndex = scenario.steps.Count;
            SetState(SessionState.Completed);
            _logger?.EndSession();
        }

        /// <summary>
        /// Habituation-gated level progression (Freeman et al. 2018): repeats a task/VAS
        /// cycle every <see cref="ExposureStepDefinition{TState}.gateCheckIntervalSeconds"/>
        /// until the anxiety rating has fallen to <see cref="ExposureStepDefinition{TState}.vasGateThreshold"/>
        /// or below for <see cref="ExposureStepDefinition{TState}.consecutiveReadingsRequired"/>
        /// consecutive ratings. durationSeconds acts as a safety time cap that forces
        /// advancement even without full habituation.
        /// </summary>
        private IEnumerator RunHabituationGate(int index, ExposureStepDefinition<TState> step)
        {
            float elapsed = 0f;
            int consecutiveBelow = 0;
            float cap = step.durationSeconds > 0f ? step.durationSeconds : 480f;
            int required = Mathf.Max(1, step.consecutiveReadingsRequired);

            while (true)
            {
                if (ShouldAbort())
                {
                    Abort($"Heart rate >= {scenario.maxHeartRateAbort} bpm");
                    yield break;
                }

                yield return RunTimer(Mathf.Min(step.gateCheckIntervalSeconds, Mathf.Max(0f, cap - elapsed)));
                if (State == SessionState.Aborted) yield break;
                elapsed += step.gateCheckIntervalSeconds;

                yield return AskAnxiety(index, step, "gate");
                if (State == SessionState.Aborted) yield break;
                SetState(SessionState.StepActive);

                int val = _lastAnswer ?? 100;
                consecutiveBelow = val <= step.vasGateThreshold ? consecutiveBelow + 1 : 0;

                if (consecutiveBelow >= required || elapsed >= cap)
                    break;
            }

            _lastStepElapsedSeconds = elapsed;
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
                string label = phase == "start" ? "How anxious do you feel? (Start)"
                              : phase == "end" ? "How anxious do you feel? (End)"
                              : "How anxious do you feel right now?";
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
