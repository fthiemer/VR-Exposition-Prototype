#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Exposure.EditorTools
{
    /// <summary>
    /// Generates the study-faithful claustrophobia scenario (Mies 2025, Ch. 2.3) as
    /// ScriptableObject assets: 6 steps of 5 min each, 3 min baseline paced breathing,
    /// abort at 200 bpm. Demonstrates fast, data-driven iteration.
    ///
    /// Study VR slots:
    ///  2: bright (ceiling lamp) | ladder | hatch open
    ///  3: bright (ceiling lamp) | ladder | hatch closed
    ///  4: bright (ceiling lamp) | no ladder | hatch open
    ///  5: bright (ceiling lamp) | no ladder | hatch closed
    ///  6: dark | ladder | hatch open
    ///  7: small floor lamp | ladder | hatch closed
    /// </summary>
    public static class ClaustrophobiaScenarioFactory
    {
        private const string Folder = "Assets/_Exposure/Scenarios/Claustrophobia";

        [MenuItem("Exposure/Generate Claustrophobia Study Scenario")]
        public static void CreateClaustrophobiaScenario()
        {
            Directory.CreateDirectory(Folder);

            var scenario = ScriptableObject.CreateInstance<RoomScenarioDefinition>();
            scenario.scenarioName = "Claustrophobia - Basement Room (Study: Mies 2025)";
            scenario.description = "Study-faithful VR exposure: 6 steps of 5 min each, " +
                                   "introductory paced breathing 3 min. Anxiety is controlled via escape " +
                                   "possibility (hatch), lighting and safety signal (ladder).";
            scenario.source = "Mies (2025), University of Mainz";
            scenario.maxHeartRateAbort = 200f;

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

        private static RoomStepDefinition Step(string id, string title, LightingMode light,
            HatchState hatch, bool ladder, bool doorClosed, bool doorLocked)
        {
            var step = ScriptableObject.CreateInstance<RoomStepDefinition>();
            step.stepId = id;
            step.title = title;
            step.taskPool = new List<TaskVariant<RoomState>>
            {
                new TaskVariant<RoomState>
                {
                    taskId = id,
                    instruction = "What changed in the room compared to the previous slot?",
                    durationSeconds = 300f,
                    difficultyRank = 0,
                    state = new RoomState
                    {
                        lighting = light,
                        hatch = hatch,
                        ladderPresent = ladder,
                        doorClosed = doorClosed,
                        doorLocked = doorLocked
                    }
                }
            };
            return step;
        }
    }
}
#endif
