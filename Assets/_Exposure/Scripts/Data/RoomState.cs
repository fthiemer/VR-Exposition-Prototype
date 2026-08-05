using System;
using UnityEngine;

namespace Exposure
{
    /// <summary>
    /// Beleuchtungsstufen der Expositions-Abstufungen.
    /// Direkt abgeleitet aus dem Studienmanual (Mies 2025): helle Deckenlampe,
    /// kleine Stehlampe, dunkler Raum.
    /// </summary>
    public enum LightingMode
    {
        CeilingLampBright = 0, // Deckenlampe (hell)
        SmallFloorLamp    = 1, // kleine Stehlampe
        Dark              = 2  // dunkler Raum
    }

    /// <summary>
    /// Zustand der Deckenluke – zentrale Manipulation der "Fluchtmöglichkeit"
    /// (offen = Blick nach draußen, geschlossen = Metallplatte).
    /// </summary>
    public enum HatchState
    {
        OpenWithView       = 0, // offene Luke (Fluchtmöglichkeit / Höhensicht)
        ClosedMetalPlate   = 1  // mit Metallplatte verschlossen
    }

    /// <summary>
    /// Vollständig serialisierbarer Zustand des Expositionsraums für eine Abstufung.
    /// Die Angstintensität wird – wie in der Arbeit belegt – über Fluchtmöglichkeit
    /// (Luke/Tür), Beleuchtung und Sicherheitssignale (Leiter) gesteuert, NICHT über
    /// die Raumgröße. Neue Parameter hier ergänzen -> automatisch datengetrieben.
    /// </summary>
    [Serializable]
    public struct RoomState
    {
        public LightingMode lighting;
        public HatchState hatch;

        [Tooltip("Ladder present under the hatch = safety signal 'escape route'.")]
        public bool ladderPresent;

        [Tooltip("Door closed (reduces escape possibility).")]
        public bool doorClosed;

        [Tooltip("Door additionally locked (strongest reduction of escape possibility).")]
        public bool doorLocked;

        public static RoomState Default => new RoomState
        {
            lighting = LightingMode.CeilingLampBright,
            hatch = HatchState.OpenWithView,
            ladderPresent = true,
            doorClosed = false,
            doorLocked = false
        };
    }
}
