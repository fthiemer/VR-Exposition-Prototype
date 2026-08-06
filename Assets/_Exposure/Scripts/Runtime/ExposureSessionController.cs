using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Exposure
{
    public enum SessionState
    {
        Idle,
        Onboarding,
        AwaitingReady,
        AwaitingPrediction,
        TaskActive,
        AwaitingOutcome,
        SessionComplete,
        Completed,
        Aborted
    }

    /// <summary>One completed behavioural experiment, kept for the session summary.</summary>
    public struct ExperimentRecord
    {
        public int stepIndex;
        public string stepId;
        public string outcomeId;
        public int convictionBefore;
        public int convictionAfter;
        public bool occurred;
        public int anxiety0to100;
        public float minDistanceToEdge;
    }

    /// <summary>
    /// Core of the prototype: drives an exposure scenario as a state machine, generic over
    /// the scenario-specific environment state <typeparamref name="TState"/>.
    ///
    /// Each level is run as a behavioural experiment:
    ///   ready -> state a prediction -> carry out the task -> review what happened.
    ///
    /// Progression is gated on expectancy violation (was the feared outcome disconfirmed?),
    /// not on within-session habituation. Habituation is still recorded -- it remains
    /// clinically informative -- but gating on it would end each level exactly when no
    /// expectation is left to violate, i.e. when no further learning can occur
    /// (Craske et al. 2014; Hamlett et al. 2023; see README).
    ///
    /// Dependencies are decoupled via interfaces. Adding a scenario needs only an
    /// IEnvironmentController&lt;TNewState&gt; plus a closed subclass of this controller.
    /// </summary>
    public abstract class ExposureSessionController<TState> : MonoBehaviour
    {
        [Header("Scenario")]
        [SerializeField] private ExposureScenarioDefinition<TState> scenario;
        [SerializeField] private FearedOutcomeCatalog fearedOutcomes;
        [SerializeField] private bool startOnPlay = false;

        [Header("Dependencies (must implement the respective interfaces)")]
        [SerializeField] private MonoBehaviour environmentControllerBehaviour; // IEnvironmentController<TState>
        [SerializeField] private MonoBehaviour predictionPromptBehaviour;      // IPredictionPrompt
        [SerializeField] private MonoBehaviour taskSourceBehaviour;            // ITaskCompletionSource
        [SerializeField] private MonoBehaviour biosignalSourceBehaviour;       // IBiosignalSource
        [SerializeField] private MonoBehaviour sessionLoggerBehaviour;         // ISessionLogger

        [Header("Safety")]
        [Tooltip("Force the task to end after this many seconds even if it is never completed.")]
        [SerializeField, Min(30f)] private float taskTimeoutSeconds = 480f;

        // --- Events for UI/audio ---
        public event Action<SessionState> OnStateChanged;
        public event Action<int, ExposureStepDefinition<TState>> OnStepChanged;
        public event Action<string> OnCoachMessage;

        public SessionState State { get; private set; } = SessionState.Idle;
        public int CurrentStepIndex { get; private set; } = -1;
        public int CurrentSessionNumber { get; private set; } = 1;

        /// <summary>Highest level index unlocked so far -- all of these stay selectable.</summary>
        public int HighestUnlockedStepIndex { get; private set; }

        public IReadOnlyList<ExperimentRecord> Experiments => _experiments;

        private readonly List<ExperimentRecord> _experiments = new List<ExperimentRecord>();

        private IEnvironmentController<TState> _env;
        private IPredictionPrompt _prompt;
        private ITaskCompletionSource _tasks;
        private IBiosignalSource _bio;
        private ISessionLogger _logger;

        private Prediction? _prediction;
        private OutcomeReport? _outcome;
        private bool _taskDone;
        private bool _readyConfirmed;

        /// <summary>Environment state to apply before the first level (ground floor).</summary>
        protected abstract TState DefaultState { get; }

        private void Awake()
        {
            _env    = environmentControllerBehaviour as IEnvironmentController<TState>;
            _prompt = predictionPromptBehaviour as IPredictionPrompt;
            _tasks  = taskSourceBehaviour as ITaskCompletionSource;
            _bio    = biosignalSourceBehaviour as IBiosignalSource;
            _logger = sessionLoggerBehaviour as ISessionLogger;

            if (environmentControllerBehaviour != null && _env == null)
                Debug.LogError("[Exposure] Assigned environment object does not implement IEnvironmentController.");
            if (predictionPromptBehaviour != null && _prompt == null)
                Debug.LogError("[Exposure] Assigned prompt object does not implement IPredictionPrompt.");
            if (taskSourceBehaviour != null && _tasks == null)
                Debug.LogError("[Exposure] Assigned task object does not implement ITaskCompletionSource.");
        }

        private void OnEnable()
        {
            if (_tasks != null) _tasks.OnAvoidanceDetected += HandleAvoidance;
        }

        private void OnDisable()
        {
            if (_tasks != null) _tasks.OnAvoidanceDetected -= HandleAvoidance;
        }

        private void Start()
        {
            if (startOnPlay) StartSession();
        }

        /// <summary>Starts a sitting at the level last reached.</summary>
        public void StartSession() => StartSessionAt(HighestUnlockedStepIndex);

        /// <summary>
        /// Starts a sitting at a chosen level. Any already-unlocked level may be revisited;
        /// jumping past the unlocked front is not allowed.
        /// </summary>
        public void StartSessionAt(int stepIndex)
        {
            if (scenario == null) { Debug.LogError("[Exposure] No scenario assigned."); return; }
            bool canStart = State == SessionState.Idle || State == SessionState.Completed
                            || State == SessionState.Aborted || State == SessionState.SessionComplete;
            if (!canStart) return;

            stepIndex = Mathf.Clamp(stepIndex, 0, Mathf.Min(HighestUnlockedStepIndex, scenario.steps.Count - 1));
            StopAllCoroutines();
            StartCoroutine(RunSession(stepIndex));
        }

        /// <summary>Called by the ready screen once the participant confirms.</summary>
        public void ConfirmReady() => _readyConfirmed = true;

        /// <summary>Manual stop by participant or therapist. Always permitted.</summary>
        public void StopSession(string reason = "Stopped on request")
        {
            StopAllCoroutines();
            if (_tasks != null) _tasks.CancelTask();
            Abort(reason);
        }

        public void ResetProgress()
        {
            StopAllCoroutines();
            _experiments.Clear();
            HighestUnlockedStepIndex = 0;
            CurrentSessionNumber = 1;
            CurrentStepIndex = -1;
            SetState(SessionState.Idle);
        }

        private IEnumerator RunSession(int startIndex)
        {
            _logger?.BeginSession($"{scenario.scenarioName} (session {CurrentSessionNumber})");

            SetState(SessionState.Onboarding);
            // Ground floor first -- never drop someone straight into height.
            _env?.Apply(DefaultState, instant: true);
            yield return null;

            for (int i = startIndex; i < scenario.steps.Count; i++)
            {
                var step = scenario.steps[i];
                if (step == null) continue;

                CurrentStepIndex = i;
                OnStepChanged?.Invoke(i, step);

                // --- ready screen: no automatic level change ---
                SetState(SessionState.AwaitingReady);
                _readyConfirmed = false;
                while (!_readyConfirmed)
                {
                    if (ShouldAbort()) { Abort(HeartRateReason()); yield break; }
                    yield return null;
                }

                // --- move to the level (elevator ride doubles as the transition) ---
                _env?.Apply(step.state, instant: false);
                _logger?.LogStepStart(i, step.stepId, CurrentHeartRate());

                // --- predict ---
                SetState(SessionState.AwaitingPrediction);
                _prediction = null;
                if (_prompt != null && fearedOutcomes != null)
                {
                    _prompt.AskPrediction(fearedOutcomes, p => _prediction = p);
                    while (_prediction == null)
                    {
                        if (ShouldAbort()) { Abort(HeartRateReason()); yield break; }
                        yield return null;
                    }
                }
                else
                {
                    // No UI wired (blockout/editor testing) -> skip with a neutral record.
                    _prediction = new Prediction { outcomeId = "none", convictionPercent = -1 };
                }
                _logger?.LogPrediction(i, step.stepId, _prediction.Value.outcomeId,
                                       _prediction.Value.convictionPercent, CurrentHeartRate());

                // --- carry out the task ---
                SetState(SessionState.TaskActive);
                yield return RunTask(step);
                if (State == SessionState.Aborted) yield break;

                // --- review ---
                SetState(SessionState.AwaitingOutcome);
                _outcome = null;
                if (_prompt != null && fearedOutcomes != null)
                {
                    _prompt.AskOutcome(fearedOutcomes, _prediction.Value, o => _outcome = o);
                    while (_outcome == null)
                    {
                        if (ShouldAbort()) { Abort(HeartRateReason()); yield break; }
                        yield return null;
                    }
                }
                else
                {
                    _outcome = new OutcomeReport { occurred = false, convictionPercent = -1, anxiety0to100 = -1 };
                }

                RecordExperiment(i, step);
                _logger?.LogStepEnd(i, step.stepId, CurrentHeartRate());

                // Disconfirmation unlocks the next level; if it did occur, the same level
                // stays available for another attempt rather than forcing progress.
                bool disconfirmed = !_outcome.Value.occurred;
                if (disconfirmed && i + 1 < scenario.steps.Count)
                    HighestUnlockedStepIndex = Mathf.Max(HighestUnlockedStepIndex, i + 1);

                if (!disconfirmed)
                {
                    OnCoachMessage?.Invoke(UIText.Get("repeat_level_coach"));
                    i--; // repeat the same level
                }
            }

            HighestUnlockedStepIndex = scenario.steps.Count - 1;
            SetState(SessionState.Completed);
            _logger?.EndSession();
        }

        /// <summary>
        /// Runs the level's task, ending on completion or on a safety timeout. Heart rate is
        /// monitored throughout; there is no anxiety polling during the task.
        /// </summary>
        private IEnumerator RunTask(ExposureStepDefinition<TState> step)
        {
            _taskDone = false;

            if (_tasks == null)
            {
                // No task detection wired -> fall back to the step's nominal duration.
                yield return WaitWithHeartRate(step.durationSeconds);
                yield break;
            }

            _tasks.BeginTask(step.state is HeightState hs ? hs.task : TaskType.Stand, () => _taskDone = true);

            float elapsed = 0f;
            while (!_taskDone && elapsed < taskTimeoutSeconds)
            {
                if (ShouldAbort()) { _tasks.CancelTask(); Abort(HeartRateReason()); yield break; }
                elapsed += Time.deltaTime;
                yield return null;
            }

            _tasks.CancelTask();
        }

        private IEnumerator WaitWithHeartRate(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if (ShouldAbort()) { Abort(HeartRateReason()); yield break; }
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private void RecordExperiment(int index, ExposureStepDefinition<TState> step)
        {
            var rec = new ExperimentRecord
            {
                stepIndex = index,
                stepId = step.stepId,
                outcomeId = _prediction?.outcomeId ?? "none",
                convictionBefore = _prediction?.convictionPercent ?? -1,
                convictionAfter = _outcome?.convictionPercent ?? -1,
                occurred = _outcome?.occurred ?? false,
                anxiety0to100 = _outcome?.anxiety0to100 ?? -1,
                minDistanceToEdge = _tasks?.MinDistanceToEdge ?? -1f
            };
            _experiments.Add(rec);

            _logger?.LogOutcome(index, step.stepId, rec.outcomeId, rec.occurred,
                                rec.convictionAfter, rec.anxiety0to100,
                                rec.minDistanceToEdge, CurrentHeartRate());
        }

        private void HandleAvoidance(string cue) => OnCoachMessage?.Invoke(cue);

        private bool ShouldAbort()
        {
            if (_bio == null || !_bio.HasSignal) return false;
            return _bio.CurrentHeartRate >= scenario.maxHeartRateAbort;
        }

        private string HeartRateReason() => $"Heart rate >= {scenario.maxHeartRateAbort} bpm";

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
