using UnityEngine;

namespace Exposure
{
    /// <summary>
    /// Slowly drifts an object up through the atrium and wraps it back to the bottom.
    ///
    /// Objects rising past the participant are the single cheapest way to make a drop read as
    /// deep: they cross many depth planes, so the eye gets motion parallax at every level
    /// instead of only at the floor. Freeman et al. (2018) list exactly this among the height
    /// cues of their atrium ("balls in the air, people moving about"), where the cues are
    /// described as bringing on the symptoms people get at real heights.
    ///
    /// Movement is deliberately slow and steady -- this is a depth cue, not something to
    /// look at, and anything fast enough to draw attention would compete with the task.
    /// </summary>
    public class FloatingHeightCue : MonoBehaviour
    {
        [Tooltip("Metres per second. Slow on purpose: a depth cue, not a distraction.")]
        [SerializeField, Min(0f)] private float riseSpeed = 0.35f;

        [Tooltip("Local Y at which the object wraps back to the bottom.")]
        [SerializeField] private float topY = 34f;

        [Tooltip("Local Y the object wraps back to.")]
        [SerializeField] private float bottomY = -1f;

        [Tooltip("Horizontal sway amplitude in metres, so the rise does not look mechanical.")]
        [SerializeField, Min(0f)] private float swayAmplitude = 0.25f;

        [SerializeField, Min(0f)] private float swaySpeed = 0.6f;

        private float _phase;
        private float _baseX;
        private float _baseZ;

        private void Awake()
        {
            var p = transform.localPosition;
            _baseX = p.x;
            _baseZ = p.z;

            // Desynchronise instances so they do not rise as a visible block.
            _phase = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            var p = transform.localPosition;

            p.y += riseSpeed * Time.deltaTime;
            if (p.y > topY) p.y = bottomY;

            float t = Time.time * swaySpeed + _phase;
            p.x = _baseX + Mathf.Sin(t) * swayAmplitude;
            p.z = _baseZ + Mathf.Cos(t * 0.7f) * swayAmplitude;

            transform.localPosition = p;
        }
    }
}
