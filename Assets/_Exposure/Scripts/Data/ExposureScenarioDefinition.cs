using System.Collections.Generic;
using UnityEngine;

namespace Exposure
{
    /// <summary>
    /// A complete exposure scenario as an ordered list of steps, generic over the
    /// scenario-specific environment state. Swappable/extensible: acrophobia and
    /// claustrophobia both implement this same data schema via a closed concrete
    /// subclass, sharing the entire session flow, VAS prompting, biosignal monitoring,
    /// and logging without any changes to that shared code.
    /// </summary>
    public abstract class ExposureScenarioDefinition<TState> : ScriptableObject
    {
        [Header("Metadata")]
        public string scenarioName = "New Scenario";

        [TextArea(2, 4)]
        public string description;

        [Tooltip("Source/basis for traceability (scientific grounding).")]
        public string source;

        [Header("Timing")]
        [Tooltip("Safety abort criterion: heart rate in bpm (study: 200).")]
        public float maxHeartRateAbort = 200f;

        [Tooltip("Duration of the introductory paced breathing in seconds (study: 180 s).")]
        public float pacedBreathingSeconds = 180f;

        [Header("Steps (in order)")]
        public List<ExposureStepDefinition<TState>> steps = new List<ExposureStepDefinition<TState>>();
    }
}
