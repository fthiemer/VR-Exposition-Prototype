using UnityEngine;

namespace Exposure
{
    /// <summary>
    /// Definition of a single exposure step ("slot") as a ScriptableObject, generic over
    /// the scenario-specific environment state. Data-driven -> new steps or entirely new
    /// scenarios without touching the session flow logic; only a concrete closed subclass
    /// (e.g. RoomStepDefinition, HeightStepDefinition) plus a matching
    /// IEnvironmentController implementation are needed to extend the system.
    /// </summary>
    public abstract class ExposureStepDefinition<TState> : ScriptableObject
    {
        [Header("Identity")]
        public string stepId = "slot";
        public string title = "New Step";

        [TextArea(2, 4)]
        [Tooltip("Instruction shown/read to the participant at the start of the slot.")]
        public string instruction;

        [Header("Timing")]
        [Tooltip("Fixed-duration mode: duration of the level in seconds. Habituation-gated mode: safety time cap before forced advancement.")]
        public float durationSeconds = 300f;

        [Tooltip("Baseline slot with paced breathing (5 s in / 5 s out) instead of exposure.")]
        public bool isBaselineBreathing = false;

        [Header("Progression")]
        [Tooltip("If true, advance based on habituation (Freeman et al. 2018) instead of a fixed duration.")]
        public bool habituationGated = false;

        [Tooltip("VAS (0-100) at/under which a reading counts as habituated.")]
        public float vasGateThreshold = 30f;

        [Tooltip("Consecutive habituated readings required to advance to the next level.")]
        public int consecutiveReadingsRequired = 2;

        [Tooltip("Seconds between task attempts / VAS ratings while gated.")]
        public float gateCheckIntervalSeconds = 45f;

        [Header("Anxiety Prompt (VAS 0-100 %)")]
        public bool askAnxietyAtStart = true;
        public bool askAnxietyAtEnd = true;

        [Header("Environment State For This Step")]
        public TState state;

        [Header("Guiding Question (optional)")]
        [TextArea(2, 3)]
        [Tooltip("e.g. 'What changed compared to the previous slot?'")]
        public string guidingQuestion;
    }
}
