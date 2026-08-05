#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Exposure.EditorTools
{
    /// <summary>
    /// Generates the acrophobia scenario as ScriptableObject assets: a ladder of height
    /// levels that vary edge protection, underfoot surface, safety signal and task.
    ///
    /// Progression is driven by expectancy violation at runtime (see
    /// ExposureSessionController), not encoded here -- this factory only declares the
    /// levels. Level content is own scenario design informed by the acrophobia
    /// literature; see README for what is sourced and what is design.
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
            scenario.description = "Graded height exposure run as behavioural experiments. Intensity is " +
                                   "controlled via edge protection, underfoot surface and a visible safety " +
                                   "net, not via floor count alone.";
            scenario.source = "Level design informed by acrophobia literature; progression follows " +
                              "inhibitory-learning principles (Craske et al. 2014)";
            scenario.maxHeartRateAbort = 200f;

            scenario.steps.Add(Level("level1", "Level 1 - low floor, railing, stand",
                1, RailingMode.SolidRailing, SurfaceType.Solid, TaskType.Stand, true, 0.05f));
            scenario.steps.Add(Level("level2", "Level 2 - mid floor, railing, approach edge",
                3, RailingMode.SolidRailing, SurfaceType.Solid, TaskType.ApproachEdge, true, 0.1f));
            scenario.steps.Add(Level("level3", "Level 3 - higher floor, glass barrier, look down",
                5, RailingMode.GlassBarrier, SurfaceType.Solid, TaskType.LookDown, true, 0.2f));
            scenario.steps.Add(Level("level4", "Level 4 - higher floor, glass barrier, grating floor",
                6, RailingMode.GlassBarrier, SurfaceType.Grating, TaskType.LookDown, true, 0.3f));
            scenario.steps.Add(Level("level5", "Level 5 - near rooftop, open edge, glass floor",
                8, RailingMode.Open, SurfaceType.Glass, TaskType.Stand, false, 0.4f));
            scenario.steps.Add(Level("level6", "Level 6 - rooftop, open edge, cross plank",
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

        private static HeightStepDefinition Level(string id, string title, int floorIndex,
            RailingMode railing, SurfaceType surface, TaskType task, bool safetyNetVisible, float wind)
        {
            var step = ScriptableObject.CreateInstance<HeightStepDefinition>();
            step.stepId = id;
            step.title = title;
            step.instruction = InstructionFor(task);
            step.durationSeconds = 120f; // fallback only, used when no task detection is wired
            step.guidingQuestion = "What did you expect would happen, and what actually happened?";
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

        private static string InstructionFor(TaskType task)
        {
            switch (task)
            {
                case TaskType.ApproachEdge: return "When you are ready, walk slowly towards the edge.";
                case TaskType.LookDown:     return "Step to the edge and look down.";
                case TaskType.CrossPlank:   return "Cross the plank at your own pace.";
                default:                    return "Stand here and take in the space around you.";
            }
        }
    }
}
#endif
