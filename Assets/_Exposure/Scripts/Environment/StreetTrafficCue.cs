using UnityEngine;

namespace Exposure
{
    /// <summary>
    /// Drives an object along one axis of the street grid and wraps it at the far end.
    ///
    /// Replaces an earlier version that made the cars sway sideways on the spot. Sway was
    /// cheap motion, but from above it read as objects wobbling in a field rather than as
    /// traffic -- and traffic is the cue that carries the height. Movement along a road tells
    /// the eye how long that road is, and the eye converts that into how far away it is.
    ///
    /// Slow and constant on purpose: a depth cue, not something to watch.
    /// </summary>
    public class StreetTrafficCue : MonoBehaviour
    {
        [Tooltip("Direction of travel in the parent's local space. Normalised at start.")]
        [SerializeField] private Vector3 direction = Vector3.right;

        [Tooltip("Metres per second.")]
        [SerializeField, Min(0f)] private float speed = 3f;

        [Tooltip("Distance travelled before wrapping back to the start point.")]
        [SerializeField, Min(1f)] private float loopLength = 60f;

        [Tooltip("Where along the loop this vehicle starts, in metres. Set by the generator so " +
                 "a lane's vehicles are spaced out rather than randomly placed.")]
        [SerializeField, Min(0f)] private float startOffset;

        private Vector3 _start;
        private Vector3 _dir;
        private float _travelled;

        private void Awake()
        {
            _start = transform.localPosition;
            _dir = direction.sqrMagnitude < 0.0001f ? Vector3.right : direction.normalized;

            // Spacing comes from the generator, not from Random. Random offsets put two cars in
            // the same place often enough to be noticed, and vehicles occupying each other is
            // the one thing that makes traffic stop reading as traffic.
            _travelled = startOffset % loopLength;
        }

        private void Update()
        {
            _travelled += speed * Time.deltaTime;
            if (_travelled > loopLength) _travelled -= loopLength;

            transform.localPosition = _start + _dir * _travelled;
        }
    }
}
