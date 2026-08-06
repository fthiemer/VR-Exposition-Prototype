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

        /// <summary>Generic labelled-button choice, used for floor selection and the post-task menu.</summary>
        void ShowChoice(string message, string[] labels, Action<int> onChosen);
    }
}
