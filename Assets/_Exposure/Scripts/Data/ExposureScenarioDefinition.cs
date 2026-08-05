using System.Collections.Generic;
using UnityEngine;

namespace Exposure
{
    /// <summary>
    /// Ein vollständiges Expositions-Szenario als geordnete Liste von Abstufungen.
    /// Austauschbar/erweiterbar: Klaustrophobie ist das erste Szenario, weitere
    /// (z. B. Höhe, Flugangst) implementieren dasselbe Datenschema.
    /// </summary>
    [CreateAssetMenu(fileName = "Scenario_", menuName = "Exposure/Expositions-Szenario (Scenario)")]
    public class ExposureScenarioDefinition : ScriptableObject
    {
        [Header("Metadaten")]
        public string scenarioName = "Klaustrophobie – Kellerraum";

        [TextArea(2, 4)]
        public string description;

        [Tooltip("Quelle/Grundlage für Nachvollziehbarkeit (wiss. Fundierung).")]
        public string source = "Mies (2025), Doktorarbeit Uni Mainz";

        [Header("Ablauf")]
        [Tooltip("Sicherheits-Abbruchkriterium: Herzfrequenz in bpm (Studie: 200).")]
        public float maxHeartRateAbort = 200f;

        [Tooltip("Dauer der einleitenden Taktatmung in Sekunden (Studie: 180 s).")]
        public float pacedBreathingSeconds = 180f;

        [Header("Abstufungen (in Reihenfolge)")]
        public List<ExposureStepDefinition> steps = new List<ExposureStepDefinition>();
    }
}
