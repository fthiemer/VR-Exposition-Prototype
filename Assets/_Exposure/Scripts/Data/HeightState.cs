using System;
using UnityEngine;

namespace Exposure
{
    /// <summary>
    /// How exposed the platform edge is -- the primary anxiety-controlling axis for
    /// acrophobia, analogous to the hatch state in the claustrophobia scenario.
    /// </summary>
    public enum RailingMode
    {
        SolidRailing = 0,  // waist-high solid railing, edge not visible underfoot
        GlassBarrier = 1,  // transparent barrier, edge fully visible
        Open         = 2   // no barrier at all
    }

    /// <summary>Underfoot surface -- secondary sensory intensity axis.</summary>
    public enum SurfaceType
    {
        Solid   = 0,  // opaque floor, no view of the drop
        Grating = 1,  // metal grating, partial view of the drop
        Glass   = 2,  // glass floor, full view of the drop
        Plank   = 3   // narrow plank/beam crossing a gap
    }

    /// <summary>Active task at this step -- drives engagement beyond passive standing.</summary>
    public enum TaskType
    {
        Stand        = 0,
        ApproachEdge = 1,
        LookDown     = 2,
        CrossPlank   = 3
    }

    /// <summary>
    /// Fully serializable state of the height platform for one exposure step. As with
    /// RoomState for claustrophobia, anxiety is controlled via escape/exposure feel
    /// (railing, surface) and a safety signal (visible safety net), not via raw height
    /// alone -- new axes can be added here without touching the session flow.
    /// </summary>
    [Serializable]
    public struct HeightState
    {
        [Tooltip("Floor index (0 = ground). Drives the vertical position of the platform rig.")]
        public int floorIndex;

        public RailingMode railing;
        public SurfaceType surface;
        public TaskType task;

        [Tooltip("Safety net visible far below = safety signal, akin to the ladder in the claustrophobia room.")]
        public bool safetyNetVisible;

        [Range(0f, 1f)]
        [Tooltip("Wind sound/visual sway intensity as an additional sensory cue.")]
        public float windIntensity;

        /// <summary>
        /// Ground floor: the state the participant starts in, before any level. Deliberately
        /// floor 0 -- level 1 is the first floor, so confirming the first level is what
        /// produces the first elevator ride.
        /// </summary>
        public static HeightState Default => new HeightState
        {
            floorIndex = 0,
            railing = RailingMode.SolidRailing,
            surface = SurfaceType.Solid,
            task = TaskType.Stand,
            safetyNetVisible = true,
            windIntensity = 0f
        };
    }
}
