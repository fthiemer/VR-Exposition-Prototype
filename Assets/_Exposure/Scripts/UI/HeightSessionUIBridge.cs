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
            session.OnTaskVariantChanged += HandleVariant;
            session.OnCoachMessage += HandleCoachMessage;
        }

        private void OnDisable()
        {
            if (session == null) return;
            session.OnStateChanged -= HandleState;
            session.OnStepChanged -= HandleStep;
            session.OnTaskVariantChanged -= HandleVariant;
            session.OnCoachMessage -= HandleCoachMessage;
        }

private void HandleStep(int index, ExposureStepDefinition<HeightState> step)
        {
            _pendingLevelTitle = step != null ? step.title : $"Level {index + 1}";
        }

        private void HandleVariant(TaskVariant<HeightState> variant)
        {
            _pendingInstruction = variant != null ? variant.instruction : "";
        }

private void HandleState(SessionState state)
        {
            switch (state)
            {
                case SessionState.AwaitingReady:
                    // One screen on the ground: what is waiting up there, and the button that
                    // takes you there. Naming the conditions and confirming the ride were two
                    // separate panels before, which just read as the same question twice.
                    ui.ShowConfirm(UIText.Get("ready_screen", _pendingLevelTitle),
                                   UIText.Get("ready_confirm"),
                                   () => session.ConfirmReady());
                    break;

                case SessionState.TaskBriefing:
                    // Shown on arrival, not during the ride. The task only begins -- and the
                    // target marker only appears -- once this is acknowledged.
                    ui.ShowConfirm(UIText.Get("task_briefing", _pendingInstruction),
                                   UIText.Get("task_dismiss"),
                                   () => session.ConfirmCondition());
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
        /// Summary framed around the expectancy change (E1 vs. E2), since Pittig et al. (2023)
        /// found that -- not whether the feared outcome occurred -- is what predicts outcome.
        ///
        /// Public so it can also be shown outside VR or exported for the therapist, without
        /// having to replay the session.
        /// </summary>
public string BuildSummary()
        {
            var rec = session.LastSessionOutcome;
            if (rec == null) return UIText.Get("summary_empty");

            var sb = new StringBuilder();
            sb.Append(UIText.Get("summary_title")).Append("\n\n");

            if (rec.Value.expectancyBefore >= 0 && rec.Value.expectancyAfter >= 0)
            {
                int change = rec.Value.ExpectancyChange;
                sb.Append(change > 0
                    ? UIText.Get("summary_expectancy_drop", change)
                    : UIText.Get("summary_expectancy_same"));
            }
            else
            {
                sb.Append(UIText.Get("summary_done_no_rating"));
            }

            return sb.ToString();
        }
    }
}
