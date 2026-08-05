using UnityEngine;

namespace Exposure
{
    /// <summary>
    /// Definition einer einzelnen Expositions-Abstufung ("Slot") als ScriptableObject.
    /// Datengetrieben -> neue Abstufungen ohne Code-Änderung, schnelle Iteration.
    /// Bildet 1:1 die Slot-Struktur der Studie ab (Mies 2025, Kap. 2.3).
    /// </summary>
    [CreateAssetMenu(fileName = "Step_", menuName = "Exposure/Expositions-Abstufung (Step)")]
    public class ExposureStepDefinition : ScriptableObject
    {
        [Header("Identität")]
        public string stepId = "slot";
        public string title = "Neue Abstufung";

        [TextArea(2, 4)]
        [Tooltip("Instruktion/Leittext, der der Person zu Beginn des Slots angezeigt/vorgelesen wird.")]
        public string instruction;

        [Header("Ablauf")]
        [Tooltip("Dauer des Slots in Sekunden (Studie: 300 s = 5 min).")]
        public float durationSeconds = 300f;

        [Tooltip("Baseline-Slot mit Taktatmung (5 s ein / 5 s aus) statt Raum-Exposition.")]
        public bool isBaselineBreathing = false;

        [Header("Angstabfrage (VAS 0-100 %)")]
        public bool askAnxietyAtStart = true;
        public bool askAnxietyAtEnd = true;

        [Header("Raumzustand dieser Abstufung")]
        public RoomState roomState = RoomState.Default;

        [Header("Leitfrage (optional)")]
        [TextArea(2, 3)]
        [Tooltip("z. B. 'Was hat sich im Raum gegenüber dem vorherigen Slot verändert?'")]
        public string guidingQuestion;
    }
}
