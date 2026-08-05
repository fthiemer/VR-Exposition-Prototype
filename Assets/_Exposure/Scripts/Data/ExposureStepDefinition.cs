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
        [Tooltip("Duration of the slot in seconds (study: 300 s = 5 min).")]
        public float durationSeconds = 300f;

        [Tooltip("Baseline slot with paced breathing (5 s in / 5 s out) instead of exposure.")]
        public bool isBaselineBreathing = false;

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
