using System.Collections;
using UnityEngine;

namespace Exposure
{
    /// <summary>
    /// Concrete environment controller for the acrophobia high-rise/glass-elevator
    /// scenario. Binds HeightState parameters to real scene objects (railing, surface,
    /// safety net, wind) and moves the platform rig to simulate riding the elevator
    /// between floors -- the vertical ride itself becomes the seamless transition
    /// (no headset removal between steps, per the design consequences in
    /// 01_Wissenschaftliche_Grundlage_Zusammenfassung.md). References are wired in the
    /// editor / via Unity MCP.
    ///
    /// Blockout note: uses simple primitives/toggled GameObjects for the first testable
    /// pass; visual dressing (Megascans surfaces, city backdrop) swaps in later without
    /// touching this script.
    /// </summary>
    public class HeightEnvironmentController : MonoBehaviour, IEnvironmentController<HeightState>
    {
        [Header("Platform Rig")]
        [Tooltip("Parent transform of the building/ground/city -- moved to simulate the elevator ride.")]
        [SerializeField] private Transform platformRig;
        [SerializeField, Min(0.1f)] private float floorHeightMeters = 3f;
        [SerializeField, Min(0f)] private float transitionSeconds = 3f;

        [Header("Railing / Edge Protection")]
        [SerializeField] private GameObject railingSolid;
        [SerializeField] private GameObject railingGlass;
        // RailingMode.Open -> both inactive, edge fully open.

        [Header("Underfoot Surface")]
        [SerializeField] private GameObject surfaceSolid;
        [SerializeField] private GameObject surfaceGrating;
        [SerializeField] private GameObject surfaceGlass;
        [SerializeField] private GameObject surfacePlank;

        [Header("Other Objects")]
        [SerializeField] private GameObject safetyNet;
        [SerializeField] private AudioSource windAudio;
        [SerializeField, Range(0f, 1f)] private float maxWindVolume = 0.5f;

        private Coroutine _moveRoutine;

        public void Apply(HeightState state, bool instant)
        {
            // --- discrete object states: immediate ---
            SetActive(railingSolid, state.railing == RailingMode.SolidRailing);
            SetActive(railingGlass, state.railing == RailingMode.GlassBarrier);

            SetActive(surfaceSolid, state.surface == SurfaceType.Solid);
            SetActive(surfaceGrating, state.surface == SurfaceType.Grating);
            SetActive(surfaceGlass, state.surface == SurfaceType.Glass);
            SetActive(surfacePlank, state.surface == SurfaceType.Plank);

            SetActive(safetyNet, state.safetyNetVisible);

            if (windAudio != null)
                windAudio.volume = state.windIntensity * maxWindVolume;

            // --- vertical ride: soft blend ---
            float targetY = -state.floorIndex * floorHeightMeters;

            if (_moveRoutine != null) StopCoroutine(_moveRoutine);
            if (platformRig == null) return;

            if (instant || transitionSeconds <= 0f)
                SetY(targetY);
            else
                _moveRoutine = StartCoroutine(MoveTo(targetY));
        }

        private IEnumerator MoveTo(float targetY)
        {
            float t = 0f;
            float startY = platformRig.localPosition.y;
            while (t < transitionSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / transitionSeconds);
                SetY(Mathf.Lerp(startY, targetY, k));
                yield return null;
            }
            SetY(targetY);
            _moveRoutine = null;
        }

        private void SetY(float y)
        {
            var p = platformRig.localPosition;
            p.y = y;
            platformRig.localPosition = p;
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null) go.SetActive(active);
        }
    }
}
