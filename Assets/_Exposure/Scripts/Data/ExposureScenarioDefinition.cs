using System.Collections.Generic;
using UnityEngine;

namespace Exposure
{
    /// <summary>
    /// A complete exposure scenario as an ordered list of levels, generic over the
    /// scenario-specific environment state. Acrophobia and claustrophobia both implement
    /// this same schema via a closed concrete subclass, sharing the session flow,
    /// behavioural-experiment prompting, biosignal monitoring and logging unchanged.
    ///
    /// Progress across sittings is held by the session controller (highest unlocked level),
    /// so a scenario definition stays purely declarative.
    /// </summary>
    public abstract class ExposureScenarioDefinition<TState> : ScriptableObject
    {
        [Header("Metadata")]
        public string scenarioName = "New Scenario";

        [TextArea(2, 4)]
        public string description;

        [Tooltip("Source/basis for traceability (scientific grounding).")]
        public string source;

        [Header("Safety")]
        [Tooltip("Abort criterion: heart rate in bpm.")]
        public float maxHeartRateAbort = 200f;

        [Header("Levels (in order of difficulty)")]
        public List<ExposureStepDefinition<TState>> steps = new List<ExposureStepDefinition<TState>>();
    }
}
