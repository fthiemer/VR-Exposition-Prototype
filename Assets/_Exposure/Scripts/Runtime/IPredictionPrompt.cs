using System;

namespace Exposure
{
    /// <summary>Prediction the participant states before attempting a task.</summary>
    public struct Prediction
    {
        /// <summary>Id of the chosen entry from the FearedOutcomeCatalog.</summary>
        public string outcomeId;

        /// <summary>How convinced they are it will happen, 0-100 %.</summary>
        public int convictionPercent;
    }

    /// <summary>Result reported after the task.</summary>
    public struct OutcomeReport
    {
        /// <summary>Did the feared outcome actually occur?</summary>
        public bool occurred;

        /// <summary>Conviction re-rated after the experience, 0-100 %.</summary>
        public int convictionPercent;

        /// <summary>Anxiety rating for the task, 0-100.</summary>
        public int anxiety0to100;
    }

    /// <summary>
    /// In-VR behavioural-experiment prompts. Two interruptions per task by design
    /// (predict, then review) -- continuous polling would destroy the presence the
    /// effect depends on.
    ///
    /// Concrete implementation is a world-space UI operated by hand tracking.
    /// </summary>
    public interface IPredictionPrompt
    {
        /// <summary>
        /// Asks which feared outcome the participant expects and how convinced they are.
        /// </summary>
        void AskPrediction(FearedOutcomeCatalog catalog, Action<Prediction> onAnswered);

        /// <summary>
        /// Asks whether the predicted outcome occurred, re-rates conviction, and takes an
        /// anxiety rating. <paramref name="prediction"/> is passed back so the UI can show
        /// what was predicted.
        /// </summary>
        void AskOutcome(FearedOutcomeCatalog catalog, Prediction prediction, Action<OutcomeReport> onAnswered);
    }
}
