using UnityEngine;

namespace Exposure
{
    /// <summary>
    /// Definition of a single exposure level as a ScriptableObject, generic over the
    /// scenario-specific environment state. Data-driven -> new levels or entirely new
    /// scenarios without touching the session flow; only a concrete closed subclass
    /// (RoomStepDefinition, HeightStepDefinition) plus a matching
    /// IEnvironmentController implementation are needed to extend the system.
    /// </summary>
    public abstract class ExposureStepDefinition<TState> : ScriptableObject
    {
        [Header("Identity")]
        public string stepId = "level";
        public string title = "New Level";

        [TextArea(2, 4)]
        [Tooltip("Task instruction shown to the participant when the level starts.")]
        public string instruction;

        [Header("Environment State For This Level")]
        public TState state;

        [Header("Fallback Timing")]
        [Tooltip("Used only when no task detection is wired (blockout/editor testing). " +
                 "Normally the level ends when the task is carried out, not on a timer.")]
        public float durationSeconds = 120f;

        [Header("Review (optional)")]
        [TextArea(2, 3)]
        [Tooltip("Reflection question offered after the level, e.g. 'What did you expect, and what happened?'")]
        public string guidingQuestion;
    }
}
