using UnityEngine;

namespace Exposure
{
    /// <summary>
    /// Audio for the task phase: a cue when the task starts, a hum that runs only while the
    /// condition is actually being held, and a chime on completion.
    ///
    /// The hum is the important one. It stops the moment the participant stops meeting the
    /// condition and resumes when they meet it again, so it reports the state of the task
    /// continuously without anyone having to look at a UI element -- which matters when the
    /// task is "look down over the edge".
    ///
    /// All three clips are optional. If no hum clip is assigned, a placeholder tone is
    /// generated at runtime so the whole flow is testable before any sound design exists;
    /// assigning a clip replaces it with no code change.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioTaskFeedback : MonoBehaviour, ITaskFeedback
    {
        [Header("Clips (all optional)")]
        [Tooltip("Played once when the task becomes active.")]
        [SerializeField] private AudioClip taskStartClip;

        [Tooltip("Looped while the condition is held. Leave empty to use a generated placeholder tone.")]
        [SerializeField] private AudioClip holdLoopClip;

        [Tooltip("Played once when the task is completed.")]
        [SerializeField] private AudioClip completedClip;

        [Header("Hold loop")]
        [SerializeField, Range(0f, 1f)] private float holdVolume = 0.35f;

        [Tooltip("Pitch at the start of the hold versus at completion. A slight rise conveys " +
                 "progress without needing a countdown.")]
        [SerializeField] private float startPitch = 0.95f;
        [SerializeField] private float endPitch = 1.15f;

        [Header("Generated placeholder tone")]
        [Tooltip("Base frequency in Hz. Integer values keep the generated clip loop seamless.")]
        [SerializeField, Min(20f)] private float placeholderFrequencyHz = 110f;

        private AudioSource _oneShotSource;
        private AudioSource _loopSource;
        private AudioClip _runtimeHum;
        private AudioClip _runtimeComplete;
        private bool _humming;

        private void Awake()
        {
            _oneShotSource = GetComponent<AudioSource>();
            _oneShotSource.playOnAwake = false;

            // A second source, because the hum has to loop underneath one-shots without
            // being cut off by them.
            _loopSource = gameObject.AddComponent<AudioSource>();
            _loopSource.playOnAwake = false;
            _loopSource.loop = true;
            _loopSource.volume = holdVolume;

            _loopSource.clip = holdLoopClip != null ? holdLoopClip : GetOrCreatePlaceholderHum();
        }

public void TaskStarted(TaskType task)
        {
            StopHum();

            // No generated fallback here. The placeholder sine landed at the same moment as the
            // lift, and a synthetic beep next to a recorded elevator does not read as one
            // experience -- it read as the app making a noise at you. Silence is better than a
            // stand-in until a real cue exists; assigning a clip brings it straight back.
            if (taskStartClip != null) _oneShotSource.PlayOneShot(taskStartClip);
        }

        public void TaskProgress(float progress01, bool conditionHeld)
        {
            if (conditionHeld)
            {
                if (!_humming)
                {
                    _loopSource.Play();
                    _humming = true;
                }
                _loopSource.pitch = Mathf.Lerp(startPitch, endPitch, progress01);
            }
            else if (_humming)
            {
                StopHum();
            }
        }

public void TaskCompleted()
        {
            StopHum();
            _oneShotSource.PlayOneShot(completedClip != null ? completedClip : CompletionChime());
        }

        public void TaskCancelled() => StopHum();

        private void StopHum()
        {
            if (!_humming) return;
            _loopSource.Stop();
            _humming = false;
        }

        /// <summary>
        /// Builds a one-second tone. One second at an integer frequency contains a whole
        /// number of cycles, so the loop point lands exactly on a zero crossing and does not
        /// click. A second partial above the fundamental keeps it from sounding like a test
        /// tone -- this is a stand-in for a designed sound, not a substitute for one.
        /// </summary>
        private AudioClip GetOrCreatePlaceholderHum()
        {
            if (_runtimeHum != null) return _runtimeHum;

            const int sampleRate = 44100;
            var data = new float[sampleRate];
            float f = Mathf.Round(placeholderFrequencyHz);

            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / sampleRate;
                data[i] = (Mathf.Sin(2f * Mathf.PI * f * t) * 0.6f +
                           Mathf.Sin(2f * Mathf.PI * f * 2f * t) * 0.25f) * 0.5f;
            }

            _runtimeHum = AudioClip.Create("PlaceholderHum", data.Length, 1, sampleRate, false);
            _runtimeHum.SetData(data, 0);
            return _runtimeHum;
        }

/// <summary>
        /// Placeholder completion chime: a rising two-note interval, which is what makes a sound
        /// read as "done" rather than "stopped". Replaced the moment a real clip is assigned.
        /// </summary>
        private AudioClip CompletionChime()
        {
            if (_runtimeComplete == null)
                _runtimeComplete = BuildTone("PlaceholderChime", 0.55f,
                                             new[] { 784f, 1175f }, new[] { 0.45f, 0.55f });
            return _runtimeComplete;
        }

        /// <summary>
        /// Builds a short clip from a sequence of notes, each with an exponential decay so it
        /// sounds struck rather than switched on. Deliberately plain -- this is a stand-in that
        /// keeps the flow testable, not a substitute for designed audio.
        /// </summary>
        private static AudioClip BuildTone(string name, float seconds, float[] frequencies,
                                           float[] noteLengths)
        {
            const int sampleRate = 44100;
            int total = Mathf.RoundToInt(seconds * sampleRate);
            var data = new float[total];

            int written = 0;
            for (int n = 0; n < frequencies.Length && written < total; n++)
            {
                int noteSamples = Mathf.Min(Mathf.RoundToInt(noteLengths[n] * seconds * sampleRate),
                                            total - written);
                for (int i = 0; i < noteSamples; i++)
                {
                    float t = (float)i / sampleRate;
                    float envelope = Mathf.Exp(-6f * i / noteSamples);
                    data[written + i] = Mathf.Sin(2f * Mathf.PI * frequencies[n] * t) * envelope * 0.35f;
                }
                written += noteSamples;
            }

            var clip = AudioClip.Create(name, total, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

    }
}
