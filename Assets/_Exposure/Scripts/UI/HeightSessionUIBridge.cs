using System.Text;
using UnityEngine;

namespace Exposure.UI
{
    /// <summary>
    /// Connects the acrophobia session flow to the world-space UI: the ready screen shown
    /// before each level, short coach cues, and the end-of-session summary.
    ///
    /// The ready screen exists so no level change ever happens automatically -- the
    /// participant confirms each step themselves.
    /// </summary>
    [RequireComponent(typeof(WorldSpacePromptUI))]
    public class HeightSessionUIBridge : MonoBehaviour
    {
        [SerializeField] private HeightExposureSessionController session;
        [SerializeField] private WorldSpacePromptUI ui;

        private string _pendingLevelTitle = "";

        private void Reset() => ui = GetComponent<WorldSpacePromptUI>();

        private void Awake()
        {
            if (ui == null) ui = GetComponent<WorldSpacePromptUI>();
        }

        private void OnEnable()
        {
            if (session == null) return;
            session.OnStateChanged += HandleState;
            session.OnStepChanged += HandleStep;
            session.OnCoachMessage += HandleCoachMessage;
        }

        private void OnDisable()
        {
            if (session == null) return;
            session.OnStateChanged -= HandleState;
            session.OnStepChanged -= HandleStep;
            session.OnCoachMessage -= HandleCoachMessage;
        }

        private void HandleStep(int index, ExposureStepDefinition<HeightState> step)
        {
            _pendingLevelTitle = step != null ? step.title : $"Level {index + 1}";
        }

        private void HandleState(SessionState state)
        {
            switch (state)
            {
                case SessionState.AwaitingReady:
                    ui.ShowConfirm($"Next: {_pendingLevelTitle}\n\nReady to go up?",
                                   "I'm ready",
                                   () => session.ConfirmReady());
                    break;

                case SessionState.Completed:
                    ui.ShowMessage(BuildSummary(), "Done", () => { });
                    break;

                case SessionState.Aborted:
                    ui.ShowMessage("We stopped here. That was the right call, not a setback.",
                                   "Close", () => { });
                    break;
            }
        }

        private void HandleCoachMessage(string message)
        {
            // Cues during a task must not steal focus -- log for now; a non-blocking
            // in-world label is a later step.
            Debug.Log($"[Coach] {message}");
        }

        /// <summary>
        /// Summary framed around what was predicted versus what happened, since that
        /// difference -- not the height reached -- is what the session was about.
        ///
        /// Public so it can also be shown outside VR or exported for the therapist,
        /// without having to replay the session.
        /// </summary>
        public string BuildSummary()
        {
            var records = session.Experiments;
            if (records == null || records.Count == 0)
                return "Session finished.";

            var sb = new StringBuilder();
            sb.Append("What you tested today\n\n");

            int disconfirmed = 0;
            int convictionDrop = 0;
            int counted = 0;

            foreach (var r in records)
            {
                if (!r.occurred) disconfirmed++;
                if (r.convictionBefore >= 0 && r.convictionAfter >= 0)
                {
                    convictionDrop += r.convictionBefore - r.convictionAfter;
                    counted++;
                }
            }

            sb.Append($"Levels worked through: {records.Count}\n");
            sb.Append($"Fears that did not come true: {disconfirmed} of {records.Count}\n");

            if (counted > 0)
            {
                int avg = Mathf.RoundToInt((float)convictionDrop / counted);
                sb.Append(avg > 0
                    ? $"On average you were {avg} % less convinced afterwards."
                    : "Your conviction stayed about the same -- worth talking through.");
            }

            return sb.ToString();
        }
    }
}
