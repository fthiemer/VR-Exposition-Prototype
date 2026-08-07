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

        [Tooltip("Solid ground behind the plank -- somewhere to start from and step back to. " +
                 "Shown only for the plank surface.")]
        [SerializeField] private GameObject plankApron;

        [Tooltip("Low kerb marking where the walkable area ends. Hidden for the plank, whose " +
                 "whole point is an unguarded edge on both sides.")]
        [SerializeField] private GameObject platformBoundary;

        [Header("Other Objects")]
        [SerializeField] private GameObject safetyNet;
        [SerializeField] private AudioSource windAudio;
        [SerializeField, Range(0f, 1f)] private float maxWindVolume = 0.5f;

        [Header("Audio")]
        [Tooltip("Plays for the duration of the ride between floors, so the transition is heard " +
                 "as well as seen.")]
        [SerializeField] private AudioSource elevatorAudio;

        [Tooltip("City ambience from the ground far below. Gets quieter with height, which is " +
                 "itself a height cue.")]
        [SerializeField] private AudioSource cityAmbienceAudio;
        [SerializeField, Range(0f, 1f)] private float maxCityVolume = 0.35f;

        [Tooltip("Floor index at which city ambience has faded to its quietest.")]
        [SerializeField, Min(1)] private int cityFadeOutFloor = 10;

        private Coroutine _moveRoutine;

        /// <summary>
        /// Tracks the ride itself, not the coroutine: the routine outlives the arrival by the
        /// length of the audio fade, and the task should start when the lift stops, not when the
        /// sound has finished.
        /// </summary>
        private bool _riding;

        /// <summary>
        /// The elevator's own full volume, captured once.
        ///
        /// The fade used to read the current volume as its starting point and restore that at the
        /// end. If a new ride interrupted a fade -- which happens whenever someone repeats a task
        /// on the same floor -- the restore never ran, and the source stayed at whatever level the
        /// fade had reached. Usually zero. Every later ride was then silent, with nothing in the
        /// scene looking wrong.
        /// </summary>
        private float _elevatorVolume = 1f;

        public bool IsTransitioning => _riding;

        private void Awake()
        {
            if (elevatorAudio != null) _elevatorVolume = elevatorAudio.volume;
        }

        public void Apply(HeightState state, bool instant)
        {
            // Ambient audio follows the height, so it changes with the movement, not after it.
            if (windAudio != null)
                windAudio.volume = state.windIntensity * maxWindVolume;

            if (cityAmbienceAudio != null)
            {
                float height01 = Mathf.Clamp01((float)state.floorIndex / cityFadeOutFloor);
                cityAmbienceAudio.volume = Mathf.Lerp(maxCityVolume, maxCityVolume * 0.15f, height01);
            }

            // --- vertical ride: soft blend ---
            float targetY = -state.floorIndex * floorHeightMeters;

            if (_moveRoutine != null) StopCoroutine(_moveRoutine);
            if (platformRig == null) return;

            if (instant || transitionSeconds <= 0f)
            {
                _riding = false;
                ApplyDiscreteState(state);
                SetY(targetY);
            }
            else
            {
                // The railing and floor change on arrival, not on departure. Swapping them at
                // the moment the lift starts meant the platform visibly rebuilt itself under
                // someone who was still travelling -- the glass barrier appearing mid-ride reads
                // as a glitch, and it also gives away the next step's conditions before they
                // have arrived to face them.
                _riding = true;
                _moveRoutine = StartCoroutine(MoveTo(targetY, state));
            }
        }

        /// <summary>Swaps the railing, floor and boundary objects for a state.</summary>
        private void ApplyDiscreteState(HeightState state)
        {
            SetActive(railingSolid, state.railing == RailingMode.SolidRailing);
            SetActive(railingGlass, state.railing == RailingMode.GlassBarrier);

            SetActive(surfaceSolid, state.surface == SurfaceType.Solid);
            SetActive(surfaceGrating, state.surface == SurfaceType.Grating);
            SetActive(surfaceGlass, state.surface == SurfaceType.Glass);
            SetActive(surfacePlank, state.surface == SurfaceType.Plank);
            SetActive(plankApron, state.surface == SurfaceType.Plank);

            // The boundary tells the participant where the floor stops while they are looking
            // down or ahead rather than at their feet. It is deliberately absent on the plank:
            // there the exposed edge *is* the exercise.
            SetActive(platformBoundary, state.surface != SurfaceType.Plank);

            SetActive(safetyNet, state.safetyNetVisible);
        }

        private IEnumerator MoveTo(float targetY, HeightState arrivalState)
        {
            if (elevatorAudio != null && !Mathf.Approximately(platformRig.localPosition.y, targetY))
            {
                elevatorAudio.volume = _elevatorVolume; // in case a previous fade was cut short
                elevatorAudio.Play();
            }

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

            // Arrived: now the platform takes the shape of the level being entered, and only
            // then does the session consider the ride finished.
            ApplyDiscreteState(arrivalState);
            _riding = false;

            // Fade rather than cut: the clip is longer than the ride, and a hard stop on
            // arrival reads as a glitch rather than as the lift settling.
            if (elevatorAudio != null && elevatorAudio.isPlaying)
                yield return FadeOutElevator();

            _moveRoutine = null;
        }

        private IEnumerator FadeOutElevator()
        {
            float t = 0f;
            const float fadeSeconds = 0.6f;
            while (t < fadeSeconds)
            {
                t += Time.deltaTime;
                elevatorAudio.volume = Mathf.Lerp(_elevatorVolume, 0f, t / fadeSeconds);
                yield return null;
            }
            elevatorAudio.Stop();
            elevatorAudio.volume = _elevatorVolume;
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
