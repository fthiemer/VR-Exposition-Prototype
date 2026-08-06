using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Exposure
{
    /// <summary>
    /// Writes the session record as CSV to Application.persistentDataPath.
    /// On Quest that resolves to /sdcard/Android/data/&lt;package&gt;/files.
    ///
    /// One row per event; columns cover both the behavioural experiment and the
    /// anxiety/behaviour measures so a session can be reviewed in a spreadsheet without
    /// further processing.
    /// </summary>
    public class CsvSessionLogger : MonoBehaviour, ISessionLogger
    {
        private const string Header =
            "timestamp_iso;event;index;step_id;outcome_id;conviction_pct;occurred;anxiety_0_100;min_dist_edge_m;heart_rate_bpm";

        private StringBuilder _sb;
        private string _path;

        public void BeginSession(string scenarioName)
        {
            _sb = new StringBuilder();
            _sb.Append(Header).Append('\n');
            string safe = string.IsNullOrEmpty(scenarioName) ? "session" : Sanitize(scenarioName);
            _path = Path.Combine(Application.persistentDataPath,
                $"exposure_{safe}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            // Scenario name goes in outcome_id rather than step_id -- step_id must stay a
            // level identifier so the column can be filtered on when reviewing.
            Row("session_start", -1, "", Sanitize(scenarioName), "", "", "", "", 0f);
        }

        public void LogStepStart(int index, string stepId, float hr)
            => Row("step_start", index, stepId, "", "", "", "", "", hr);

        public void LogPrediction(int index, string stepId, string outcomeId, int conviction, float hr)
            => Row("prediction", index, stepId, outcomeId, Num(conviction), "", "", "", hr);

        public void LogOutcome(int index, string stepId, string outcomeId, bool occurred,
                               int conviction, int anxiety, float minDistanceToEdge, float hr)
            => Row("outcome", index, stepId, outcomeId, Num(conviction),
                   occurred ? "1" : "0", Num(anxiety), Num(minDistanceToEdge), hr);

        public void LogStepEnd(int index, string stepId, float hr)
            => Row("step_end", index, stepId, "", "", "", "", "", hr);

        public void LogAbort(string reason, float hr)
            => Row("abort", -1, "", Sanitize(reason), "", "", "", "", hr);

        public void EndSession()
        {
            Row("session_end", -1, "", "", "", "", "", "", 0f);
            Flush();
        }

        private void Row(string ev, int index, string stepId, string outcomeId, string conviction,
                         string occurred, string anxiety, string minDist, float hr)
        {
            if (_sb == null) return;
            _sb.Append(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)).Append(';')
               .Append(ev).Append(';')
               .Append(index.ToString(CultureInfo.InvariantCulture)).Append(';')
               .Append(stepId).Append(';')
               .Append(outcomeId).Append(';')
               .Append(conviction).Append(';')
               .Append(occurred).Append(';')
               .Append(anxiety).Append(';')
               .Append(minDist).Append(';')
               .Append(hr.ToString("F1", CultureInfo.InvariantCulture))
               .Append('\n');
        }

        private static string Num(int v) => v < 0 ? "" : v.ToString(CultureInfo.InvariantCulture);

        private static string Num(float v) => v < 0f ? "" : v.ToString("F2", CultureInfo.InvariantCulture);

        /// <summary>Strips separators and newlines so a value can never break the CSV.</summary>
        private static string Sanitize(string s)
            => string.IsNullOrEmpty(s) ? "" : s.Replace(';', ',').Replace('\n', ' ').Replace('\r', ' ').Replace(' ', '_');

        private void Flush()
        {
            if (_sb == null || string.IsNullOrEmpty(_path)) return;
            try
            {
                File.WriteAllText(_path, _sb.ToString());
                Debug.Log($"[Exposure] Session log written: {_path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Exposure] Could not write session log: {e.Message}");
            }
        }

        private void OnDisable() => Flush();
    }
}
