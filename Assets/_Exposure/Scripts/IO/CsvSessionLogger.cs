using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Exposure
{
    /// <summary>
    /// Schreibt das Sitzungsprotokoll als CSV nach Application.persistentDataPath.
    /// Auf Quest liegt das unter /sdcard/Android/data/&lt;package&gt;/files.
    /// </summary>
    public class CsvSessionLogger : MonoBehaviour, ISessionLogger
    {
        private StringBuilder _sb;
        private string _path;

        public void BeginSession(string scenarioName)
        {
            _sb = new StringBuilder();
            _sb.AppendLine("timestamp_iso;event;index;step_id;phase;vas_0_100;heart_rate_bpm");
            string safe = string.IsNullOrEmpty(scenarioName) ? "session" : scenarioName.Replace(" ", "_");
            // Feste Namenskomponente + Realtime, um Kollisionen zu vermeiden.
            _path = Path.Combine(Application.persistentDataPath,
                $"exposure_{safe}_{(int)(Time.realtimeSinceStartup * 1000)}.csv");
            Row("session_start", -1, scenarioName, "", "", 0f);
        }

        public void LogStepStart(int index, string stepId, float hr) => Row("step_start", index, stepId, "", "", hr);
        public void LogStepEnd(int index, string stepId, float hr) => Row("step_end", index, stepId, "", "", hr);
        public void LogAbort(string reason, float hr) => Row("abort", -1, reason, "", "", hr);

        public void LogAnxiety(int index, string stepId, string phase, int vas, float hr)
            => Row("anxiety_vas", index, stepId, phase, vas.ToString(CultureInfo.InvariantCulture), hr);

        public void EndSession()
        {
            Row("session_end", -1, "", "", "", 0f);
            Flush();
        }

        private void Row(string ev, int index, string stepId, string phase, string vas, float hr)
        {
            if (_sb == null) return;
            _sb.Append(DateTime.UtcNow.ToString("o")).Append(';')
               .Append(ev).Append(';')
               .Append(index).Append(';')
               .Append(stepId).Append(';')
               .Append(phase).Append(';')
               .Append(vas).Append(';')
               .Append(hr.ToString("F1", CultureInfo.InvariantCulture))
               .Append('\n');
        }

        private void Flush()
        {
            if (_sb == null || string.IsNullOrEmpty(_path)) return;
            try { File.WriteAllText(_path, _sb.ToString()); Debug.Log($"[Exposure] Protokoll gespeichert: {_path}"); }
            catch (Exception e) { Debug.LogError($"[Exposure] Protokoll-Fehler: {e.Message}"); }
        }

        private void OnDisable() => Flush();
    }
}
