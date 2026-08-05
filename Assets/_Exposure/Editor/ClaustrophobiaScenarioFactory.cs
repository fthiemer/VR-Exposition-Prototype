#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Exposure.EditorTools
{
    /// <summary>
    /// Erzeugt per Menüklick das Studien-getreue Klaustrophobie-Szenario (Mies 2025,
    /// Kap. 2.3) als ScriptableObject-Assets: 6 Abstufungen à 5 min, Baseline-Taktatmung
    /// 3 min, Abbruch bei 200 bpm. Demonstriert die schnelle, datengetriebene Iteration.
    ///
    /// VR-Slots der Arbeit:
    ///  2: hell (Deckenlampe) | Leiter | Luke offen
    ///  3: hell (Deckenlampe) | Leiter | Luke geschlossen
    ///  4: hell (Deckenlampe) | ohne Leiter | Luke offen
    ///  5: hell (Deckenlampe) | ohne Leiter | Luke geschlossen
    ///  6: dunkel | Leiter | Luke offen
    ///  7: kleine Stehlampe | Leiter | Luke geschlossen
    /// </summary>
    public static class ClaustrophobiaScenarioFactory
    {
        private const string Folder = "Assets/_Exposure/Scenarios";

        [MenuItem("Exposure/Generate Claustrophobia Study Scenario")]
        public static void CreateClaustrophobiaScenario()
        {
            Directory.CreateDirectory(Folder);

            var scenario = ScriptableObject.CreateInstance<ExposureScenarioDefinition>();
            scenario.scenarioName = "Claustrophobia - Basement Room (Study: Mies 2025)";
            scenario.description = "Study-faithful VR exposure: 6 steps of 5 min each, " +
                                   "introductory paced breathing 3 min. Anxiety is controlled via escape " +
                                   "possibility (hatch), lighting and safety signal (ladder).";
            scenario.source = "Mies (2025), University of Mainz";
            scenario.maxHeartRateAbort = 200f;
            scenario.pacedBreathingSeconds = 180f;

            scenario.steps.Add(Step("slot2", "Slot 2 - bright, ladder, hatch open",
                LightingMode.CeilingLampBright, HatchState.OpenWithView, true, false, false));
            scenario.steps.Add(Step("slot3", "Slot 3 - bright, ladder, hatch closed",
                LightingMode.CeilingLampBright, HatchState.ClosedMetalPlate, true, false, false));
            scenario.steps.Add(Step("slot4", "Slot 4 - bright, no ladder, hatch open",
                LightingMode.CeilingLampBright, HatchState.OpenWithView, false, false, false));
            scenario.steps.Add(Step("slot5", "Slot 5 - bright, no ladder, hatch closed",
                LightingMode.CeilingLampBright, HatchState.ClosedMetalPlate, false, false, false));
            scenario.steps.Add(Step("slot6", "Slot 6 - dark, ladder, hatch open",
                LightingMode.Dark, HatchState.OpenWithView, true, false, false));
            scenario.steps.Add(Step("slot7", "Slot 7 - small floor lamp, ladder, hatch closed",
                LightingMode.SmallFloorLamp, HatchState.ClosedMetalPlate, true, false, false));

            foreach (var s in scenario.steps)
                AssetDatabase.CreateAsset(s, $"{Folder}/Step_{s.stepId}.asset");

            AssetDatabase.CreateAsset(scenario, $"{Folder}/Scenario_Claustrophobia.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = scenario;
            EditorGUIUtility.PingObject(scenario);
            Debug.Log("[Exposure] Claustrophobia scenario generated under " + Folder);
        }

        private static ExposureStepDefinition Step(string id, string title, LightingMode light,
            HatchState hatch, bool ladder, bool doorClosed, bool doorLocked)
        {
            var step = ScriptableObject.CreateInstance<ExposureStepDefinition>();
            step.stepId = id;
            step.title = title;
            step.durationSeconds = 300f;
            step.askAnxietyAtStart = true;
            step.askAnxietyAtEnd = true;
            step.guidingQuestion = "What changed in the room compared to the previous slot?";
            step.roomState = new RoomState
            {
                lighting = light,
                hatch = hatch,
                ladderPresent = ladder,
                doorClosed = doorClosed,
                doorLocked = doorLocked
            };
            return step;
        }
    }
}
#endif
