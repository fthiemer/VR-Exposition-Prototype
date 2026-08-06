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

        private Vector3 _start;
        private Vector3 _dir;
        private float _travelled;

        private void Awake()
        {
            _start = transform.localPosition;
            _dir = direction.sqrMagnitude < 0.0001f ? Vector3.right : direction.normalized;

            // Spread the vehicles along the road instead of releasing them from one point.
            _travelled = Random.Range(0f, loopLength);
        }

        private void Update()
        {
            _travelled += speed * Time.deltaTime;
            if (_travelled > loopLength) _travelled -= loopLength;

            transform.localPosition = _start + _dir * _travelled;
        }
    }
}
