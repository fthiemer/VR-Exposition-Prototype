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

        [MenuItem("Exposure/Studien-Szenario 'Klaustrophobie' erzeugen")]
        public static void CreateClaustrophobiaScenario()
        {
            Directory.CreateDirectory(Folder);

            var scenario = ScriptableObject.CreateInstance<ExposureScenarioDefinition>();
            scenario.scenarioName = "Klaustrophobie – Kellerraum (Studie Mies 2025)";
            scenario.description = "Studien-getreue VR-Exposition: 6 Abstufungen à 5 min, " +
                                   "einleitende Taktatmung 3 min. Angststeuerung über Fluchtmöglichkeit " +
                                   "(Luke), Beleuchtung und Sicherheitssignal (Leiter).";
            scenario.source = "Mies (2025), Uni Mainz";
            scenario.maxHeartRateAbort = 200f;
            scenario.pacedBreathingSeconds = 180f;

            scenario.steps.Add(Step("slot2", "Slot 2 – hell, Leiter, Luke offen",
                LightingMode.CeilingLampBright, HatchState.OpenWithView, true, false, false));
            scenario.steps.Add(Step("slot3", "Slot 3 – hell, Leiter, Luke geschlossen",
                LightingMode.CeilingLampBright, HatchState.ClosedMetalPlate, true, false, false));
            scenario.steps.Add(Step("slot4", "Slot 4 – hell, ohne Leiter, Luke offen",
                LightingMode.CeilingLampBright, HatchState.OpenWithView, false, false, false));
            scenario.steps.Add(Step("slot5", "Slot 5 – hell, ohne Leiter, Luke geschlossen",
                LightingMode.CeilingLampBright, HatchState.ClosedMetalPlate, false, false, false));
            scenario.steps.Add(Step("slot6", "Slot 6 – dunkel, Leiter, Luke offen",
                LightingMode.Dark, HatchState.OpenWithView, true, false, false));
            scenario.steps.Add(Step("slot7", "Slot 7 – kleine Stehlampe, Leiter, Luke geschlossen",
                LightingMode.SmallFloorLamp, HatchState.ClosedMetalPlate, true, false, false));

            foreach (var s in scenario.steps)
                AssetDatabase.CreateAsset(s, $"{Folder}/Step_{s.stepId}.asset");

            AssetDatabase.CreateAsset(scenario, $"{Folder}/Scenario_Klaustrophobie.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = scenario;
            EditorGUIUtility.PingObject(scenario);
            Debug.Log("[Exposure] Klaustrophobie-Szenario erzeugt unter " + Folder);
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
            step.guidingQuestion = "Was hat sich im Raum gegenüber dem vorherigen Slot verändert?";
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
