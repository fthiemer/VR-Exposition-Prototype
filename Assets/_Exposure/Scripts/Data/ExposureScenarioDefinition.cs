using System.Collections.Generic;
using UnityEngine;

namespace Exposure
{
    /// <summary>
    /// Ein vollständiges Expositions-Szenario als geordnete Liste von Abstufungen.
    /// Austauschbar/erweiterbar: Klaustrophobie ist das erste Szenario, weitere
    /// (z. B. Höhe, Flugangst) implementieren dasselbe Datenschema.
    /// </summary>
    [CreateAssetMenu(fileName = "Scenario_", menuName = "Exposure/Exposure Scenario")]
    public class ExposureScenarioDefinition : ScriptableObject
    {
        [Header("Metadata")]
        public string scenarioName = "Claustrophobia - Basement Room";

        [TextArea(2, 4)]
        public string description;

        [Tooltip("Source/basis for traceability (scientific grounding).")]
        public string source = "Mies (2025), PhD thesis, University of Mainz";

        [Header("Timing")]
        [Tooltip("Safety abort criterion: heart rate in bpm (study: 200).")]
        public float maxHeartRateAbort = 200f;

        [Tooltip("Duration of the introductory paced breathing in seconds (study: 180 s).")]
        public float pacedBreathingSeconds = 180f;

        [Header("Steps (in order)")]
        public List<ExposureStepDefinition> steps = new List<ExposureStepDefinition>();
    }
}
