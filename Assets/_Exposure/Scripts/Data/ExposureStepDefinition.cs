using UnityEngine;

namespace Exposure
{
    /// <summary>
    /// Definition einer einzelnen Expositions-Abstufung ("Slot") als ScriptableObject.
    /// Datengetrieben -> neue Abstufungen ohne Code-Änderung, schnelle Iteration.
    /// Bildet 1:1 die Slot-Struktur der Studie ab (Mies 2025, Kap. 2.3).
    /// </summary>
    [CreateAssetMenu(fileName = "Step_", menuName = "Exposure/Exposure Step")]
    public class ExposureStepDefinition : ScriptableObject
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

        [Tooltip("Baseline slot with paced breathing (5 s in / 5 s out) instead of room exposure.")]
        public bool isBaselineBreathing = false;

        [Header("Anxiety Prompt (VAS 0-100 %)")]
        public bool askAnxietyAtStart = true;
        public bool askAnxietyAtEnd = true;

        [Header("Room State For This Step")]
        public RoomState roomState = RoomState.Default;

        [Header("Guiding Question (optional)")]
        [TextArea(2, 3)]
        [Tooltip("e.g. 'What changed in the room compared to the previous slot?'")]
        public string guidingQuestion;
    }
}
