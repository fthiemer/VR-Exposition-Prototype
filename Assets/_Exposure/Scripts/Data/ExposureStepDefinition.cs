using System.Collections.Generic;
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

        [Header("Task Pool")]
        [Tooltip("Tasks offered on this level. On first visit the easiest (lowest " +
                 "difficultyRank) is offered; afterwards the participant may repeat it, try " +
                 "another from the pool, or move up once at least one has been completed.")]
        public List<TaskVariant<TState>> taskPool = new List<TaskVariant<TState>>();
    }
}
