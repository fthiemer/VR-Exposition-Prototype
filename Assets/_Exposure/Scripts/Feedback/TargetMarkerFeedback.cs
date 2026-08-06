using UnityEngine;

namespace Exposure
{
    /// <summary>
    /// Shows where to go before the task starts, and changes appearance while the condition
    /// is actually being held.
    ///
    /// This answers the "where am I supposed to stand?" problem directly: an instruction that
    /// names a place the participant cannot see is not an instruction. Making the target
    /// visible also means the sound and particle feedback have something to refer to.
    /// </summary>
    public class TargetMarkerFeedback : MonoBehaviour, ITaskFeedback
    {
        [Header("Marker")]
        [Tooltip("Object shown while a task is running. Usually a flat quad lying on the floor.")]
        [SerializeField] private GameObject marker;

        [Tooltip("Renderer tinted to show progress. Falls back to the marker's own renderer.")]
        [SerializeField] private Renderer markerRenderer;

        [Header("Colours")]
        [SerializeField] private Color idleColor = new Color(0.25f, 0.7f, 1f, 0.35f);
        [SerializeField] private Color heldColor = new Color(0.35f, 1f, 0.5f, 0.6f);

        [Header("Motion")]
        [Tooltip("Pulses per second while waiting to be stood on. Some movement reads as 'go here' " +
                 "much more strongly than a static patch of colour.")]
        [SerializeField, Min(0f)] private float idlePulsesPerSecond = 1f;

        [Tooltip("Extra scale at the peak of the pulse.")]
        [SerializeField, Min(0f)] private float pulseScale = 0.06f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private MaterialPropertyBlock _block;
        private Vector3 _baseScale;
        private bool _visible;

        private void Awake()
        {
            if (markerRenderer == null && marker != null)
                markerRenderer = marker.GetComponentInChildren<Renderer>();

            if (marker != null) _baseScale = marker.transform.localScale;
            _block = new MaterialPropertyBlock();

            SetVisible(false);
        }

        public void TaskStarted(TaskType task) => SetVisible(true);

        public void TaskProgress(float progress01, bool conditionHeld)
        {
            if (!_visible || marker == null) return;

            // Once the participant is standing correctly, stop pulsing and just fill in --
            // continuing to pulse would read as "keep moving" when the point is to stay put.
            Color target = conditionHeld
                ? Color.Lerp(heldColor, Color.white, progress01 * 0.35f)
                : idleColor;
            Tint(target);

            float pulse = conditionHeld || idlePulsesPerSecond <= 0f
                ? 0f
                : Mathf.Sin(Time.time * idlePulsesPerSecond * Mathf.PI * 2f) * 0.5f + 0.5f;

            marker.transform.localScale = _baseScale * (1f + pulse * pulseScale);
        }

        public void TaskCompleted() => SetVisible(false);

        public void TaskCancelled() => SetVisible(false);

        private void SetVisible(bool visible)
        {
            _visible = visible;
            if (marker == null) return;

            marker.transform.localScale = _baseScale;
            if (visible) Tint(idleColor);
            marker.SetActive(visible);
        }

        private void Tint(Color color)
        {
            if (markerRenderer == null) return;

            // Property block rather than material.color: avoids instantiating a material copy
            // per marker, which would also break batching.
            markerRenderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color); // URP
            _block.SetColor(ColorId, color);     // built-in / fallback
            markerRenderer.SetPropertyBlock(_block);
        }
    }
}
