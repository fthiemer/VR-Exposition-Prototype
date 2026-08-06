#if UNITY_EDITOR
using System.Collections.Generic;
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
            scenario.scenarioName = "Akrophobie - Hochhaus-Dach & Glasaufzug";
            scenario.description = "Gestufte Höhenexposition als Verhaltensexperimente. Die Intensität wird " +
                                   "über Kantenschutz, Bodenbelag und ein sichtbares Sicherheitsnetz gesteuert, " +
                                   "nicht allein über die Stockwerkzahl.";
            scenario.source = "Levelgestaltung informiert durch Akrophobie-Literatur; die Progression folgt " +
                              "Prinzipien des Inhibitory Learning (Craske et al. 2014)";
            scenario.maxHeartRateAbort = 200f;

            scenario.steps.Add(Level("level1", "Stufe 1 - niedriges Stockwerk, Geländer, stehen",
                1, RailingMode.SolidRailing, SurfaceType.Solid, TaskType.Stand, true, 0.05f));
            scenario.steps.Add(Level("level2", "Stufe 2 - mittleres Stockwerk, Geländer, an die Kante treten",
                3, RailingMode.SolidRailing, SurfaceType.Solid, TaskType.ApproachEdge, true, 0.1f));
            scenario.steps.Add(Level("level3", "Stufe 3 - höheres Stockwerk, Glasbrüstung, nach unten schauen",
                5, RailingMode.GlassBarrier, SurfaceType.Solid, TaskType.LookDown, true, 0.2f));
            scenario.steps.Add(Level("level4", "Stufe 4 - höheres Stockwerk, Glasbrüstung, Gitterboden",
                6, RailingMode.GlassBarrier, SurfaceType.Grating, TaskType.LookDown, true, 0.3f));
            scenario.steps.Add(Level("level5", "Stufe 5 - nahe Dachterrasse, offene Kante, Glasboden",
                8, RailingMode.Open, SurfaceType.Glass, TaskType.Stand, false, 0.4f));
            scenario.steps.Add(Level("level6", "Stufe 6 - Dachterrasse, offene Kante, über den Steg gehen",
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
            step.taskPool = new List<TaskVariant<HeightState>>
            {
                new TaskVariant<HeightState>
                {
                    taskId = task.ToString(),
                    instruction = InstructionFor(task),
                    durationSeconds = 120f, // fallback only, used when no task detection is wired
                    difficultyRank = 0,
                    state = new HeightState
                    {
                        floorIndex = floorIndex,
                        railing = railing,
                        surface = surface,
                        task = task,
                        safetyNetVisible = safetyNetVisible,
                        windIntensity = wind
                    }
                }
            };
            return step;
        }

private static string InstructionFor(TaskType task)
        {
            // Phrased so no instruction depends on a place the participant cannot see. "Stand
            // here" left people looking for a marked spot that does not exist.
            switch (task)
            {
                case TaskType.ApproachEdge: return "Wenn du bereit bist, geh langsam zur Kante.";
                case TaskType.LookDown:     return "Tritt an die Kante und schau nach unten.";
                case TaskType.CrossPlank:   return "Geh in deinem eigenen Tempo über den Steg bis ans Ende.";
                default:                    return "Nimm dir einen Moment und sieh dich in Ruhe um.";
            }
        }
    }
}
#endif
