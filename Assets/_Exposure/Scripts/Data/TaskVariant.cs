using UnityEngine;

namespace Exposure
{
    /// <summary>
    /// One task offered on a level, generic over the scenario-specific environment state.
    /// A level now holds a pool of these instead of a single fixed task, so "stay on this
    /// floor" means trying a different task, not repeating the same one.
    /// </summary>
    [System.Serializable]
    public class TaskVariant<TState>
    {
        public string taskId = "task";

        [TextArea(2, 4)]
        [Tooltip("Task instruction shown to the participant when this variant is chosen.")]
        public string instruction;

        [Tooltip("Environment state applied for this variant. Fields unrelated to the task " +
                 "itself (railing, surface, safety net, wind, floor) should match the other " +
                 "variants on the same level.")]
        public TState state;

        [Tooltip("Used only when no task detection is wired (blockout/editor testing).")]
        public float durationSeconds = 120f;

        [Tooltip("Lower = offered first on a level's first visit (Freeman: easiest-first weighting).")]
        public int difficultyRank = 0;
    }
}
