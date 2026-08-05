namespace Exposure
{
    /// <summary>
    /// Protokolliert den Ablauf (Slot-Start/-Ende, VAS-Werte, HF) für spätere Auswertung.
    /// Entspricht dem "HRV-Protokoll" der Studie in digitaler, automatisierter Form.
    /// </summary>
    public interface ISessionLogger
    {
        void BeginSession(string scenarioName);
        void LogStepStart(int index, string stepId, float heartRate);
        void LogAnxiety(int index, string stepId, string phase, int vas0to100, float heartRate);
        void LogStepEnd(int index, string stepId, float heartRate);
        void LogAbort(string reason, float heartRate);
        void EndSession();
    }
}
