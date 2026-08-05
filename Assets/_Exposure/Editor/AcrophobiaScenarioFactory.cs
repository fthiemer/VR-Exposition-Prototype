#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Exposure.EditorTools
{
    /// <summary>
    /// Generates the acrophobia scenario as ScriptableObject assets, modelled on the
    /// gold-standard literature (Freeman et al. 2018, Lancet Psychiatry; Francová et al.
    /// 2025, JBTEP): ascending floor levels, decreasing edge protection and increasing
    /// task demand, safety-net removal as the top step -- see
    /// 06_Akrophobie_Goldstandard.md for the full rationale.
    /// </summary>
    public static class AcrophobiaScenarioFactory
    {
        private const string Folder = "Assets/_Exposure/Scenarios/Acrophobia";

        [MenuItem("Exposure/Generate Acrophobia Study Scenario")]
        public static void CreateAcrophobiaScenario()
        {
            Directory.CreateDirectory(Folder);

            var scenario = ScriptableObject.CreateInstance<HeightScenarioDefinition>();
            scenario.scenarioName = "Acrophobia - High-Rise Rooftop & Glass Elevator";
            scenario.description = "Gold-standard-inspired VR exposure: ascending floor levels with " +
                                   "decreasing edge protection and increasing task demand. Anxiety is " +
                                   "controlled via railing/edge protection, surface exposure and a visible " +
                                   "safety net, not via floor count alone.";
            scenario.source = "Freeman et al. (2018), Lancet Psychiatry; Francova et al. (2025), JBTEP";
            scenario.maxHeartRateAbort = 200f;
            scenario.pacedBreathingSeconds = 180f;

            scenario.steps.Add(Step("slot1", "Slot 1 - low floor, railing, stand",
                1, RailingMode.SolidRailing, SurfaceType.Solid, TaskType.Stand, true, 0.05f));
            scenario.steps.Add(Step("slot2", "Slot 2 - mid floor, railing, approach edge",
                3, RailingMode.SolidRailing, SurfaceType.Solid, TaskType.ApproachEdge, true, 0.1f));
            scenario.steps.Add(Step("slot3", "Slot 3 - higher floor, glass barrier, look down",
                5, RailingMode.GlassBarrier, SurfaceType.Solid, TaskType.LookDown, true, 0.2f));
            scenario.steps.Add(Step("slot4", "Slot 4 - higher floor, glass barrier, grating floor",
                6, RailingMode.GlassBarrier, SurfaceType.Grating, TaskType.LookDown, true, 0.3f));
            scenario.steps.Add(Step("slot5", "Slot 5 - near rooftop, open edge, glass floor",
                8, RailingMode.Open, SurfaceType.Glass, TaskType.Stand, false, 0.4f));
            scenario.steps.Add(Step("slot6", "Slot 6 - rooftop, open edge, cross plank",
                10, RailingMode.Open, SurfaceType.Plank, TaskType.CrossPlank, false, 0.5f));

            foreach (var s in scenario.steps)
                AssetDatabase.CreateAsset(s, $"{Folder}/Step_{s.stepId}.asset");

            AssetDatabase.CreateAsset(scenario, $"{Folder}/Scenario_Acrophobia.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = scenario;
            EditorGUIUtility.PingObject(scenario);
            Debug.Log("[Exposure] Acrophobia scenario generated under " + Folder);
        }

        private static HeightStepDefinition Step(string id, string title, int floorIndex,
            RailingMode railing, SurfaceType surface, TaskType task, bool safetyNetVisible, float wind)
        {
            var step = ScriptableObject.CreateInstance<HeightStepDefinition>();
            step.stepId = id;
            step.title = title;
            step.durationSeconds = 300f;
            step.askAnxietyAtStart = true;
            step.askAnxietyAtEnd = true;
            step.guidingQuestion = "What changed compared to the previous slot?";
            step.state = new HeightState
            {
                floorIndex = floorIndex,
                railing = railing,
                surface = surface,
                task = task,
                safetyNetVisible = safetyNetVisible,
                windIntensity = wind
            };
            return step;
        }
    }
}
#endif
