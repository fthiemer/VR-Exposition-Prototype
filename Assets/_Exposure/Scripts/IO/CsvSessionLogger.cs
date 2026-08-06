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
    /// One row per event. The expectancy triple (E1/O/E2) appears once, on the
    /// expectancy_before/outcome rows; step rows carry per-task telemetry instead.
    /// </summary>
    public class CsvSessionLogger : MonoBehaviour, ISessionLogger
    {
        private const string Header =
            "timestamp_iso;event;index;step_id;outcome_id;expectancy_before;occurred;expectancy_after;" +
            "expectancy_change;learning_rate;min_dist_edge_m;heart_rate_bpm";

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
            Row("session_start", -1, "", Sanitize(scenarioName), "", "", "", "", "", "", 0f);
        }

        public void LogStepStart(int index, string stepId, float hr)
            => Row("step_start", index, stepId, "", "", "", "", "", "", "", hr);

        public void LogStepEnd(int index, string stepId, float minDistanceToEdge, float hr)
            => Row("step_end", index, stepId, "", "", "", "", "", "", Num(minDistanceToEdge), hr);

        public void LogExpectancyBefore(string outcomeId, int expectancy0to10, float hr)
            => Row("expectancy_before", -1, "", outcomeId, Num(expectancy0to10), "", "", "", "", "", hr);

        public void LogOutcome(string outcomeId, int expectancyBefore, int occurred0to10, int expectancyAfter, float hr)
        {
            string change = "";
            string learningRate = "";
            if (expectancyBefore >= 0 && expectancyAfter >= 0)
                change = (expectancyBefore - expectancyAfter).ToString(CultureInfo.InvariantCulture);

            if (expectancyBefore >= 0 && occurred0to10 >= 0 && expectancyAfter >= 0)
            {
                int violation = expectancyBefore - occurred0to10;
                learningRate = violation == 0
                    ? "0"
                    : ((float)(expectancyBefore - expectancyAfter) / violation).ToString("F2", CultureInfo.InvariantCulture);
            }

            Row("outcome", -1, "", outcomeId, Num(expectancyBefore), Num(occurred0to10), Num(expectancyAfter),
                change, learningRate, "", hr);
        }

        public void LogAbort(string reason, float hr)
            => Row("abort", -1, "", Sanitize(reason), "", "", "", "", "", "", hr);

        public void EndSession()
        {
            Row("session_end", -1, "", "", "", "", "", "", "", "", 0f);
            Flush();
        }

        private void Row(string ev, int index, string stepId, string outcomeId, string expectancyBefore,
                         string occurred, string expectancyAfter, string expectancyChange, string learningRate,
                         string minDist, float hr)
        {
            if (_sb == null) return;
            _sb.Append(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)).Append(';')
               .Append(ev).Append(';')
               .Append(index.ToString(CultureInfo.InvariantCulture)).Append(';')
               .Append(stepId).Append(';')
               .Append(outcomeId).Append(';')
               .Append(expectancyBefore).Append(';')
               .Append(occurred).Append(';')
               .Append(expectancyAfter).Append(';')
               .Append(expectancyChange).Append(';')
               .Append(learningRate).Append(';')
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
