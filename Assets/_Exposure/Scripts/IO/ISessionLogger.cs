namespace Exposure
{
    /// <summary>
    /// Records the course of a session for later review with the therapist.
    ///
    /// The expectancy triple (E1/O/E2, Pittig et al. 2023) is logged once per session, not per
    /// task -- see 11_Spezifikation_Erwartungspruefung.md. Per-task telemetry (distance to the
    /// edge) stays attached to the step-end row alongside it.
    /// </summary>
    public interface ISessionLogger
    {
        void BeginSession(string scenarioName);
        void LogStepStart(int index, string stepId, float heartRate);
        void LogStepEnd(int index, string stepId, float minDistanceToEdge, float heartRate);

        /// <summary>Expectancy stated once at session start, on the ground (E1).</summary>
        void LogExpectancyBefore(string outcomeId, int expectancy0to10, float heartRate);

        /// <summary>Occurrence and re-rated expectancy, once at session end, on the ground (O, E2).</summary>
        void LogOutcome(string outcomeId, int expectancyBefore, int occurred0to10, int expectancyAfter, float heartRate);

        void LogAbort(string reason, float heartRate);
        void EndSession();
    }
}
