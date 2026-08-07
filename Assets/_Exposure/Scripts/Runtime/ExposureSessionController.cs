using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Exposure
{
    public enum SessionState
    {
        Idle,
        Intro,
        FloorSelect,

        /// <summary>On the ground, naming the floor and its conditions. Confirming rides the lift.</summary>
        AwaitingReady,

        /// <summary>Arrived. The task is named here, and only starts once it is acknowledged.</summary>
        TaskBriefing,

        TaskActive,
        TaskChoice,
        Closing,
        Completed,
        Aborted
    }

    /// <summary>Which post-task option the participant picked. Never stored, purely flow control.</summary>
    internal enum TaskChoiceOption { Repeat, OtherTask, NextFloor, EndSession }

    /// <summary>
    /// The session's expectancy triple (Pittig et al. 2023): E1 stated at the start, O and E2
    /// re-rated at the end, both on the ground. Measured once per session, not per task -- see
    /// 11_Spezifikation_Erwartungspruefung.md for why.
    /// </summary>
    public struct SessionOutcomeRecord
    {
        public string outcomeId;
        public int expectancyBefore;   // E1, 0-10
        public int occurred;           // O, 0-10
        public int expectancyAfter;    // E2, 0-10

        public int ExpectancyChange => expectancyBefore - expectancyAfter;
        public int ExpectancyViolation => expectancyBefore - occurred;

        /// <summary>Fraction of the violation that turned into actual belief change. Pittig's second outcome predictor.</summary>
        public float LearningRate => ExpectancyViolation == 0 ? 0f : (float)ExpectancyChange / ExpectancyViolation;
    }

    /// <summary>
    /// Core of the prototype: drives an exposure scenario as a state machine, generic over
    /// the scenario-specific environment state <typeparamref name="TState"/>.
    ///
    /// A session runs: state expectancy on the ground (E1) -> choose a floor -> repeatedly
    /// carry out a task and choose what's next (repeat / different task / one floor up / end)
    /// -> back on the ground, rate what happened and re-rate expectancy (O, E2).
    ///
    /// Progression unlocks the next floor permanently once a task is completed -- not gated on
    /// expectancy violation, which Pittig et al. (2023) found does not itself predict outcome;
    /// only expectancy *change* and learning rate do, and those are session-level metrics for
    /// the therapist, decoupled from the floor-to-floor decision (which stays the participant's,
    /// after a qualitative coach question, matching Freeman et al. 2018).
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

        [Tooltip("Skip the expectancy questions (intro E1, closing O/E2). Testing convenience; " +
                 "the floor-to-floor flow and progression are unaffected.")]
        [SerializeField] private bool skipIntro = false;

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
        public event Action<TaskVariant<TState>> OnTaskVariantChanged;
        public event Action<string> OnCoachMessage;

        public SessionState State { get; private set; } = SessionState.Idle;
        public int CurrentStepIndex { get; private set; } = -1;
        public int CurrentSessionNumber { get; private set; } = 1;
        public TaskVariant<TState> CurrentVariant { get; private set; }

        /// <summary>Highest level index unlocked so far -- all of these stay selectable.</summary>
        public int HighestUnlockedStepIndex { get; private set; }

        /// <summary>Expectancy triple from the most recently completed session, if any.</summary>
        public SessionOutcomeRecord? LastSessionOutcome { get; private set; }

        private IEnvironmentController<TState> _env;
        private IPredictionPrompt _prompt;
        private ITaskCompletionSource _tasks;
        private IBiosignalSource _bio;
        private ISessionLogger _logger;

        private Prediction? _prediction;
        private OutcomeReport? _outcome;
        private bool _taskDone;
        private bool _taskCompletedByDetection;
        private bool _readyConfirmed;
        private bool _conditionAcknowledged;

        /// <summary>
        /// Index of the step whose state is currently applied, or -1 while the participant is
        /// still on the ground. Deliberately the *step* index and not a floor number: TState is
        /// generic, so this class cannot read a floor out of it. -1 rather than 0 matters --
        /// with 0 the first step compares equal to the starting value and the lift teleports
        /// instead of travelling.
        /// </summary>
        private int _appliedStepIndex = -1;

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

        /// <summary>Starts a sitting, offering the highest unlocked floor as the default choice.</summary>
        public void StartSession() => StartSessionAt(HighestUnlockedStepIndex);

        /// <summary>
        /// Starts a sitting with the given floor pre-selected for the floor-choice screen. Any
        /// already-unlocked floor may be chosen there instead; jumping past the unlocked front
        /// is not possible.
        /// </summary>
        public void StartSessionAt(int stepIndex)
        {
            if (scenario == null) { Debug.LogError("[Exposure] No scenario assigned."); return; }
            bool canStart = State == SessionState.Idle || State == SessionState.Completed
                            || State == SessionState.Aborted;
            if (!canStart) return;

            stepIndex = Mathf.Clamp(stepIndex, 0, Mathf.Min(HighestUnlockedStepIndex, scenario.steps.Count - 1));
            StopAllCoroutines();
            StartCoroutine(RunSession(stepIndex));
        }

        /// <summary>Called by the ready screen once the participant confirms.</summary>
        public void ConfirmReady() => _readyConfirmed = true;

        /// <summary>
        /// Called once the participant has been told what changes on the next floor and
        /// confirms it. Splitting this from ConfirmReady means the environment never changes
        /// under someone who has not just agreed to that specific change.
        /// </summary>
        public void ConfirmCondition() => _conditionAcknowledged = true;

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
            LastSessionOutcome = null;
            CurrentVariant = null;
            HighestUnlockedStepIndex = 0;
            CurrentSessionNumber = 1;
            CurrentStepIndex = -1;
            _appliedStepIndex = -1;
            SetState(SessionState.Idle);
        }

        private IEnumerator RunSession(int startIndex)
        {
            _logger?.BeginSession($"{scenario.scenarioName} (session {CurrentSessionNumber})");

            // Ground floor first -- never drop someone straight into height.
            _env?.Apply(DefaultState, instant: true);
            _appliedStepIndex = -1;
            yield return null;

            // --- 1. Einführung: choose the feared outcome, state E1 (ground, skippable) ---
            SetState(SessionState.Intro);
            _prediction = null;
            if (_prompt != null && fearedOutcomes != null && !skipIntro)
            {
                _prompt.AskExpectancyBefore(fearedOutcomes, p => _prediction = p);
                while (_prediction == null)
                {
                    if (ShouldAbort()) { Abort(HeartRateReason()); yield break; }
                    yield return null;
                }
            }
            else
            {
                _prediction = new Prediction { outcomeId = "none", expectancy0to10 = -1 };
            }
            _logger?.LogExpectancyBefore(_prediction.Value.outcomeId, _prediction.Value.expectancy0to10, CurrentHeartRate());

            // --- 2. Höhenauswahl: any unlocked floor, once per session ---
            SetState(SessionState.FloorSelect);
            int floorIndex = Mathf.Clamp(startIndex, 0, HighestUnlockedStepIndex);
            if (_prompt != null)
            {
                int chosenFloor = -1;

                // Every floor is listed, locked ones greyed out. Showing only what is unlocked
                // makes a one-entry menu that does not read as a choice, and hides the fact that
                // there is anything above to climb towards.
                var floorOptions = new ChoiceOption[scenario.steps.Count];
                for (int f = 0; f < scenario.steps.Count; f++)
                {
                    string title = scenario.steps[f] != null ? scenario.steps[f].title : $"Etage {f + 1}";
                    floorOptions[f] = f <= HighestUnlockedStepIndex
                        ? ChoiceOption.Available(title)
                        : ChoiceOption.Locked(title, UIText.Get("floor_locked_hint"));
                }

                _prompt.ShowChoice(UIText.Get("floor_select_question"), floorOptions, i => chosenFloor = i);
                while (chosenFloor < 0)
                {
                    if (ShouldAbort()) { Abort(HeartRateReason()); yield break; }
                    yield return null;
                }
                floorIndex = chosenFloor;
            }

            TaskVariant<TState> variant = null;
            bool sessionEnding = false;

            // --- 3./4. Aufgabe + Entscheidung, wiederholt bis Sitzungsende ---
            while (!sessionEnding)
            {
                var step = scenario.steps[floorIndex];
                if (step == null || step.taskPool == null || step.taskPool.Count == 0)
                {
                    Debug.LogError($"[Exposure] Level at index {floorIndex} has no task pool.");
                    Abort("Empty task pool");
                    yield break;
                }

                CurrentStepIndex = floorIndex;
                OnStepChanged?.Invoke(floorIndex, step);

                if (variant == null) variant = EasiestVariant(step);
                CurrentVariant = variant;
                OnTaskVariantChanged?.Invoke(variant);

                // --- one gate on the ground: what is up there, and confirming rides the lift ---
                // There used to be a second confirmation between "ready" and "go up". It asked
                // the same question twice in a row from the same spot, so it read as a misclick
                // rather than as a considered second decision.
                SetState(SessionState.AwaitingReady);
                _readyConfirmed = false;
                while (!_readyConfirmed)
                {
                    if (ShouldAbort()) { Abort(HeartRateReason()); yield break; }
                    yield return null;
                }

                // --- move to the level (ride it out whenever the floor actually changes) ---
                bool isFloorChange = floorIndex != _appliedStepIndex;
                _env?.Apply(variant.state, instant: !isFloorChange);
                _appliedStepIndex = floorIndex;
                _logger?.LogStepStart(floorIndex, step.stepId, CurrentHeartRate());

                // --- wait out the ride before saying anything about the task ---
                // Briefing someone mid-ride means they are reading while the floor moves, and the
                // task could even complete during the ride because the geometry already matches.
                while (_env != null && _env.IsTransitioning)
                {
                    if (ShouldAbort()) { Abort(HeartRateReason()); yield break; }
                    yield return null;
                }

                // --- arrived: name the task, and start it only once it is acknowledged ---
                SetState(SessionState.TaskBriefing);
                _conditionAcknowledged = false;
                while (!_conditionAcknowledged)
                {
                    if (ShouldAbort()) { Abort(HeartRateReason()); yield break; }
                    yield return null;
                }

                // --- carry out the task ---
                SetState(SessionState.TaskActive);
                yield return RunTask(variant);
                if (State == SessionState.Aborted) yield break;

                _logger?.LogStepEnd(floorIndex, step.stepId, _tasks?.MinDistanceToEdge ?? -1f, CurrentHeartRate());

                if (_taskCompletedByDetection && floorIndex + 1 < scenario.steps.Count)
                    HighestUnlockedStepIndex = Mathf.Max(HighestUnlockedStepIndex, floorIndex + 1);

                if (_taskCompletedByDetection)
                    OnCoachMessage?.Invoke(UIText.Get("safer_than_before_coach"));

                // --- Entscheidung: repeat / different task / one floor up / end session ---
                SetState(SessionState.TaskChoice);
                var options = new List<TaskChoiceOption> { TaskChoiceOption.Repeat };
                var choices = new List<ChoiceOption> { ChoiceOption.Available(UIText.Get("choice_repeat")) };

                if (step.taskPool.Count > 1)
                {
                    options.Add(TaskChoiceOption.OtherTask);
                    choices.Add(ChoiceOption.Available(UIText.Get("choice_other_task")));
                }

                // "One floor up" always appears, greyed until this floor has actually been
                // completed -- so the way onwards is visible before it is open.
                bool canGoUp = floorIndex + 1 <= HighestUnlockedStepIndex
                               && floorIndex + 1 < scenario.steps.Count;
                options.Add(TaskChoiceOption.NextFloor);
                choices.Add(canGoUp
                    ? ChoiceOption.Available(UIText.Get("choice_next_floor"))
                    : ChoiceOption.Locked(UIText.Get("choice_next_floor"),
                                          UIText.Get("choice_next_floor_locked")));

                options.Add(TaskChoiceOption.EndSession);
                choices.Add(ChoiceOption.Available(UIText.Get("choice_end_session")));

                int chosenOption = _prompt == null ? 0 : -1;
                if (_prompt != null)
                {
                    _prompt.ShowChoice(UIText.Get("task_choice_question"), choices.ToArray(), i => chosenOption = i);
                    while (chosenOption < 0)
                    {
                        if (ShouldAbort()) { Abort(HeartRateReason()); yield break; }
                        yield return null;
                    }
                }

                switch (options[chosenOption])
                {
                    case TaskChoiceOption.OtherTask:
                        variant = NextVariant(step, variant);
                        break;
                    case TaskChoiceOption.NextFloor:
                        floorIndex++;
                        variant = null;
                        break;
                    case TaskChoiceOption.EndSession:
                        sessionEnding = true;
                        break;
                    // Repeat: keep floorIndex and variant as they are.
                }
            }

            // --- 5. Abschluss: ride back down first, then ask ---
            // The closing questions belong on the ground, which means waiting out the descent --
            // asking during it puts the questions in mid-air, the one place the whole design is
            // built to keep them out of.
            _env?.Apply(DefaultState, instant: false);
            while (_env != null && _env.IsTransitioning)
            {
                if (ShouldAbort()) { Abort(HeartRateReason()); yield break; }
                yield return null;
            }

            SetState(SessionState.Closing);
            _outcome = null;
            if (_prompt != null && fearedOutcomes != null && _prediction.Value.outcomeId != "none")
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
                _outcome = new OutcomeReport { occurred0to10 = -1, expectancy0to10 = -1 };
            }

            RecordSessionOutcome();
            SetState(SessionState.Completed);
            _logger?.EndSession();
        }

        /// <summary>Lowest difficultyRank in the pool -- offered first on a level's first visit.</summary>
        private static TaskVariant<TState> EasiestVariant(ExposureStepDefinition<TState> step)
        {
            TaskVariant<TState> best = null;
            foreach (var v in step.taskPool)
                if (best == null || v.difficultyRank < best.difficultyRank) best = v;
            return best;
        }

        /// <summary>Next variant after <paramref name="current"/> in difficulty order, wrapping around.</summary>
        private static TaskVariant<TState> NextVariant(ExposureStepDefinition<TState> step, TaskVariant<TState> current)
        {
            if (step.taskPool.Count <= 1) return current;
            var sorted = new List<TaskVariant<TState>>(step.taskPool);
            sorted.Sort((a, b) => a.difficultyRank.CompareTo(b.difficultyRank));
            int idx = sorted.IndexOf(current);
            if (idx < 0) return sorted[0];
            return sorted[(idx + 1) % sorted.Count];
        }

        /// <summary>
        /// Runs the task, ending on completion or on a safety timeout. Heart rate is monitored
        /// throughout; there is no other polling during the task.
        /// </summary>
        private IEnumerator RunTask(TaskVariant<TState> variant)
        {
            _taskDone = false;
            _taskCompletedByDetection = false;

            if (_tasks == null)
            {
                // No task detection wired -> fall back to the variant's nominal duration, and
                // count that as completion so blockout/editor testing can still progress.
                yield return WaitWithHeartRate(variant.durationSeconds);
                _taskCompletedByDetection = true;
                yield break;
            }

            _tasks.BeginTask(variant.state is HeightState hs ? hs.task : TaskType.Stand, () =>
            {
                _taskDone = true;
                _taskCompletedByDetection = true;
            });

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

        private void RecordSessionOutcome()
        {
            var rec = new SessionOutcomeRecord
            {
                outcomeId = _prediction?.outcomeId ?? "none",
                expectancyBefore = _prediction?.expectancy0to10 ?? -1,
                occurred = _outcome?.occurred0to10 ?? -1,
                expectancyAfter = _outcome?.expectancy0to10 ?? -1
            };
            LastSessionOutcome = rec;

            _logger?.LogOutcome(rec.outcomeId, rec.expectancyBefore, rec.occurred, rec.expectancyAfter, CurrentHeartRate());
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
