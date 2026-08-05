using System.Collections;
using UnityEngine;

namespace Exposure
{
    /// <summary>
    /// Konkrete Raumsteuerung des Kellerraum-Szenarios. Bindet die RoomState-Parameter
    /// an echte Szenenobjekte (Lichter, Luke, Metallplatte, Leiter, Tür) und blendet
    /// Zustandswechsel weich über. Referenzen werden im Editor / via Unity MCP verdrahtet.
    ///
    /// Raummaße gemäß Studie: 1,70 m x 1,79 m Grundfläche, 1,71 m Höhe.
    /// </summary>
    public class ClaustrophobiaRoomController : MonoBehaviour, IEnvironmentController
    {
        [Header("Beleuchtung")]
        [SerializeField] private Light ceilingLamp;
        [SerializeField] private Light smallFloorLamp;
        [SerializeField, Min(0f)] private float ceilingIntensity = 1.2f;
        [SerializeField, Min(0f)] private float floorLampIntensity = 0.4f;
        [SerializeField, Min(0f)] private float transitionSeconds = 0.75f;

        [Header("Luke & Metallplatte")]
        [SerializeField] private GameObject hatchOpenVisual;      // Blick nach draußen
        [SerializeField] private GameObject hatchClosedMetalPlate; // Metallplatte

        [Header("Weitere Objekte")]
        [SerializeField] private GameObject ladder;
        [SerializeField] private GameObject door;      // geschlossen/offen als Rotation/Visual
        [SerializeField] private GameObject doorLockIndicator;

        private Coroutine _lightRoutine;

        public void Apply(RoomState state, bool instant)
        {
            // --- diskrete Objektzustände: sofort ---
            if (hatchOpenVisual != null)
                hatchOpenVisual.SetActive(state.hatch == HatchState.OpenWithView);
            if (hatchClosedMetalPlate != null)
                hatchClosedMetalPlate.SetActive(state.hatch == HatchState.ClosedMetalPlate);
            if (ladder != null)
                ladder.SetActive(state.ladderPresent);
            if (door != null)
                door.SetActive(true);
            if (doorLockIndicator != null)
                doorLockIndicator.SetActive(state.doorLocked);

            // --- Beleuchtung: weich überblenden ---
            float targetCeiling = state.lighting == LightingMode.CeilingLampBright ? ceilingIntensity : 0f;
            float targetFloor   = state.lighting == LightingMode.SmallFloorLamp   ? floorLampIntensity : 0f;
            // Dark => beide 0.

            if (_lightRoutine != null) StopCoroutine(_lightRoutine);
            if (instant || transitionSeconds <= 0f)
            {
                SetLight(ceilingLamp, targetCeiling);
                SetLight(smallFloorLamp, targetFloor);
            }
            else
            {
                _lightRoutine = StartCoroutine(FadeLights(targetCeiling, targetFloor));
            }
        }

        private IEnumerator FadeLights(float targetCeiling, float targetFloor)
        {
            float t = 0f;
            float startCeiling = ceilingLamp != null ? ceilingLamp.intensity : 0f;
            float startFloor   = smallFloorLamp != null ? smallFloorLamp.intensity : 0f;
            while (t < transitionSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / transitionSeconds);
                SetLight(ceilingLamp, Mathf.Lerp(startCeiling, targetCeiling, k));
                SetLight(smallFloorLamp, Mathf.Lerp(startFloor, targetFloor, k));
                yield return null;
            }
            SetLight(ceilingLamp, targetCeiling);
            SetLight(smallFloorLamp, targetFloor);
            _lightRoutine = null;
        }

        private static void SetLight(Light l, float intensity)
        {
            if (l == null) return;
            l.intensity = intensity;
            l.enabled = intensity > 0.001f;
        }
    }
}
