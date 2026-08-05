namespace Exposure
{
    /// <summary>
    /// Records the course of a session for later review with the therapist.
    ///
    /// Two things are logged side by side: the behavioural experiment (predicted outcome,
    /// conviction before/after, what actually happened) and the anxiety course. The former
    /// drives progression, the latter stays clinically informative -- see README.
    /// </summary>
    public interface ISessionLogger
    {
        void BeginSession(string scenarioName);
        void LogStepStart(int index, string stepId, float heartRate);

        /// <summary>Prediction stated before the task.</summary>
        void LogPrediction(int index, string stepId, string outcomeId, int convictionPercent, float heartRate);

        /// <summary>Review after the task: did it happen, re-rated conviction, anxiety, behaviour.</summary>
        void LogOutcome(int index, string stepId, string outcomeId, bool occurred,
                        int convictionPercent, int anxiety0to100, float minDistanceToEdge, float heartRate);

        void LogStepEnd(int index, string stepId, float heartRate);
        void LogAbort(string reason, float heartRate);
        void EndSession();
    }
}
