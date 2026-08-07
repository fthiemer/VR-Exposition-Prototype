using System;

namespace Exposure
{
    /// <summary>Expectancy stated once per session, before the first task (Pittig's E1).</summary>
    public struct Prediction
    {
        /// <summary>Id of the chosen entry from the FearedOutcomeCatalog.</summary>
        public string outcomeId;

        /// <summary>How convinced they are it will happen, 0-10 (Freeman scale, 1 point = 10 %).</summary>
        public int expectancy0to10;
    }

    /// <summary>Result reported once per session, after the last task (Pittig's O and E2).</summary>
    public struct OutcomeReport
    {
        /// <summary>How strongly the feared outcome occurred across the session, 0-10.</summary>
        public int occurred0to10;

        /// <summary>Expectancy re-rated for next time, 0-10.</summary>
        public int expectancy0to10;
    }

    /// <summary>
    /// One option on a choice panel.
    ///
    /// Locked options are shown rather than hidden: a list that grows as you progress reads as
    /// a ladder you are climbing, while a list that shows only what you already have gives no
    /// sense of where it leads -- and a floor menu with a single entry does not look like a
    /// choice at all.
    /// </summary>
    public struct ChoiceOption
    {
        public string label;
        public bool enabled;

        /// <summary>Shown under the label when locked, e.g. why it is not available yet.</summary>
        public string lockedHint;

        public static ChoiceOption Available(string label)
            => new ChoiceOption { label = label, enabled = true };

        public static ChoiceOption Locked(string label, string hint)
            => new ChoiceOption { label = label, enabled = false, lockedHint = hint };
    }

    /// <summary>
    /// In-VR behavioural-experiment prompts, asked once per session -- expectancy on the
    /// ground before the first task, outcome and re-rated expectancy back on the ground
    /// after the last one -- not per task, so the height exposure itself stays uninterrupted
    /// (Freeman et al. 2018; Pittig et al. 2023).
    ///
    /// Concrete implementation is a world-space UI operated by hand tracking.
    /// </summary>
    public interface IPredictionPrompt
    {
        /// <summary>Asks which feared outcome the participant expects and how convinced they are (E1).</summary>
        void AskExpectancyBefore(FearedOutcomeCatalog catalog, Action<Prediction> onAnswered);

        /// <summary>Asks how strongly it occurred (O) and re-rates expectancy for next time (E2).</summary>
        void AskOutcome(FearedOutcomeCatalog catalog, Prediction prediction, Action<OutcomeReport> onAnswered);

        /// <summary>
        /// Generic labelled-button choice, used for floor selection and the post-task menu.
        /// Locked options are drawn greyed and cannot be picked; the callback only ever
        /// reports the index of an enabled one.
        /// </summary>
        void ShowChoice(string message, ChoiceOption[] options, Action<int> onChosen);
    }
}
