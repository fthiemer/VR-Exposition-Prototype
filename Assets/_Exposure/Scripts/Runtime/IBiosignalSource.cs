using System;
using UnityEngine;

namespace Exposure
{
    /// <summary>
    /// Quelle für Biosignale (Herzfrequenz / später HRV). Entkoppelt die Ablaufsteuerung
    /// von der konkreten Hardware (Polar-Gurt via BLE, Simulation, Datei-Replay).
    /// Andockpunkt für späteres Biofeedback – die Arbeit zeigt VR≈in vivo bei HF/HRV.
    /// </summary>
    public interface IBiosignalSource
    {
        /// <summary>Aktuelle Herzfrequenz in bpm (0, wenn kein Signal).</summary>
        float CurrentHeartRate { get; }

        bool HasSignal { get; }

        /// <summary>Feuert bei jedem neuen Sample (bpm).</summary>
        event Action<float> OnHeartRateSample;
    }

    /// <summary>
    /// Platzhalter-Quelle: erzeugt ein plausibles HF-Signal, damit der komplette
    /// Ablauf (inkl. Abbruchlogik) ohne Hardware testbar ist.
    /// </summary>
    public class SimulatedHeartRateSource : MonoBehaviour, IBiosignalSource
    {
        [SerializeField] private float baseline = 80f;
        [SerializeField] private float amplitude = 8f;
        [SerializeField] private float sampleIntervalSeconds = 1f;

        public float CurrentHeartRate { get; private set; }
        public bool HasSignal => true;
        public event Action<float> OnHeartRateSample;

        private float _timer;

        private void OnEnable() => CurrentHeartRate = baseline;

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < sampleIntervalSeconds) return;
            _timer = 0f;
            CurrentHeartRate = baseline + Mathf.Sin(Time.time * 0.5f) * amplitude
                               + UnityEngine.Random.Range(-2f, 2f);
            OnHeartRateSample?.Invoke(CurrentHeartRate);
        }
    }
}
