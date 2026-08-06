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
        private string _pendingInstruction = "";

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
            _pendingInstruction = step != null ? step.instruction : "";
        }

private void HandleState(SessionState state)
        {
            switch (state)
            {
                case SessionState.AwaitingReady:
                    ui.ShowConfirm(UIText.Get("ready_screen", _pendingLevelTitle),
                                   UIText.Get("ready_confirm"),
                                   () => session.ConfirmReady());
                    break;

                case SessionState.TaskActive:
                    // Non-blocking: the task itself starts regardless of this panel, this
                    // just tells the participant what to physically do (feedback item 7 --
                    // "the task itself is never made clear").
                    if (!string.IsNullOrWhiteSpace(_pendingInstruction))
                        ui.ShowConfirm(_pendingInstruction, UIText.Get("task_dismiss"), () => { });
                    break;

                case SessionState.Completed:
                    ui.ShowMessage(BuildSummary(), UIText.Get("summary_done"), () => { });
                    break;

                case SessionState.Aborted:
                    ui.ShowMessage(UIText.Get("aborted_message"), UIText.Get("aborted_close"), () => { });
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
                return UIText.Get("summary_empty");

            var sb = new StringBuilder();
            sb.Append(UIText.Get("summary_title")).Append("\n\n");

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

            sb.Append(UIText.Get("summary_levels", records.Count)).Append('\n');
            sb.Append(UIText.Get("summary_disconfirmed", disconfirmed, records.Count)).Append('\n');

            if (counted > 0)
            {
                int avg = Mathf.RoundToInt((float)convictionDrop / counted);
                sb.Append(avg > 0
                    ? UIText.Get("summary_conviction_drop", avg)
                    : UIText.Get("summary_conviction_same"));
            }

            return sb.ToString();
        }
    }
}
