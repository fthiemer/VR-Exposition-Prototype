#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Exposure.EditorTools
{
    /// <summary>
    /// Generates the acrophobia scenario as ScriptableObject assets, modelled on the
    /// gold-standard protocol of Freeman et al. (2018, Lancet Psychiatry): up to 5
    /// separate sittings, ~24 min soft budget each, habituation-gated level progression
    /// (advance after 2 consecutive VAS ratings <= 30) instead of fixed durations. Level
    /// content (floor/railing/surface/task) follows Francová et al. (2025, JBTEP). See
    /// 06_Akrophobie_Goldstandard.md for the full rationale and source citations.
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
            scenario.description = "Gold-standard VR exposure (Freeman et al. 2018): up to 5 sittings, " +
                                   "habituation-gated ascent through height levels. Anxiety is controlled " +
                                   "via railing/edge protection, surface exposure and a visible safety net, " +
                                   "not via floor count alone.";
            scenario.source = "Freeman et al. (2018), Lancet Psychiatry; Francova et al. (2025), JBTEP";
            scenario.maxHeartRateAbort = 200f;
            scenario.pacedBreathingSeconds = 0f; // not part of the Freeman protocol
            scenario.maxSessions = 5;
            scenario.maxSessionMinutes = 24f;

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
            step.habituationGated = true;
            step.vasGateThreshold = 30f;
            step.consecutiveReadingsRequired = 2;
            step.gateCheckIntervalSeconds = 45f;
            step.durationSeconds = 480f; // safety time cap per level, not from the source paper
            step.askAnxietyAtStart = true;
            step.askAnxietyAtEnd = false; // redundant: the gate's final passing reading already is the end reading
            step.guidingQuestion = "What changed compared to the previous level?";
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
