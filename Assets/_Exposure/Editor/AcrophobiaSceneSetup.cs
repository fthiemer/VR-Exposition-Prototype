#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Exposure.UI;

namespace Exposure.EditorTools
{
    /// <summary>
    /// One-click wiring of the acrophobia scene: creates the missing runtime objects
    /// (prompt UI, task tracker, edge marker) and connects them to the session controller.
    ///
    /// Idempotent -- running it twice reuses what is already there instead of duplicating.
    /// Intended to be run on the open Exposure_Acrophobia scene after the scenario and
    /// feared-outcome assets have been generated.
    /// </summary>
    public static class AcrophobiaSceneSetup
    {
        private const string ScenarioPath = "Assets/_Exposure/Scenarios/Acrophobia/Scenario_Acrophobia.asset";
        private const string CatalogPath  = "Assets/_Exposure/Scenarios/Acrophobia/FearedOutcomes_Acrophobia.asset";

        [MenuItem("Exposure/Setup Acrophobia Scene")]
        public static void Setup()
        {
            var envRoot = GameObject.Find("AcrophobiaEnvironment");
            if (envRoot == null)
            {
                Debug.LogError("[Exposure] AcrophobiaEnvironment not found -- open Exposure_Acrophobia.unity first.");
                return;
            }

            var env = envRoot.GetComponent<HeightEnvironmentController>();
            if (env == null)
            {
                Debug.LogError("[Exposure] HeightEnvironmentController missing on AcrophobiaEnvironment.");
                return;
            }

            var scenario = AssetDatabase.LoadAssetAtPath<HeightScenarioDefinition>(ScenarioPath);
            var catalog  = AssetDatabase.LoadAssetAtPath<FearedOutcomeCatalog>(CatalogPath);
            if (scenario == null) Debug.LogWarning($"[Exposure] Scenario asset not found at {ScenarioPath} -- run the generator menu first.");
            if (catalog == null)  Debug.LogWarning($"[Exposure] Feared-outcome catalog not found at {CatalogPath} -- run the generator menu first.");

            // --- edge marker: the line the participant approaches ---
            var edge = envRoot.transform.Find("EdgeMarker");
            if (edge == null)
            {
                var go = new GameObject("EdgeMarker");
                Undo.RegisterCreatedObjectUndo(go, "Create EdgeMarker");
                go.transform.SetParent(envRoot.transform, false);
                // Front edge of the ~2 x 2 m platform. Local to AcrophobiaEnvironment, which is
                // itself offset so the platform centre sits on the world origin -- the spawn
                // point is the scene's origin rather than something a script moves.
                go.transform.localPosition = new Vector3(0f, 0f, 2.5f);
                go.transform.localRotation = Quaternion.identity; // forward = +z = out over the drop
                edge = go.transform;
            }

            // --- task tracker ---
            var tracker = envRoot.GetComponent<HeightTaskTracker>();
            if (tracker == null)
            {
                tracker = Undo.AddComponent<HeightTaskTracker>(envRoot);
            }
            var trackerSo = new SerializedObject(tracker);
            trackerSo.FindProperty("edge").objectReferenceValue = edge;
            trackerSo.ApplyModifiedProperties();

            // --- prompt UI ---
            var uiGo = GameObject.Find("ExposurePromptUI");
            if (uiGo == null)
            {
                uiGo = new GameObject("ExposurePromptUI");
                Undo.RegisterCreatedObjectUndo(uiGo, "Create ExposurePromptUI");
            }
            var promptUi = uiGo.GetComponent<WorldSpacePromptUI>() ?? Undo.AddComponent<WorldSpacePromptUI>(uiGo);

            // --- session controller ---
            var sessionGo = GameObject.Find("AcrophobiaSession");
            if (sessionGo == null)
            {
                sessionGo = new GameObject("AcrophobiaSession");
                Undo.RegisterCreatedObjectUndo(sessionGo, "Create AcrophobiaSession");
            }

            var session = sessionGo.GetComponent<HeightExposureSessionController>()
                          ?? Undo.AddComponent<HeightExposureSessionController>(sessionGo);
            var bio = sessionGo.GetComponent<SimulatedHeartRateSource>()
                      ?? Undo.AddComponent<SimulatedHeartRateSource>(sessionGo);
            var logger = sessionGo.GetComponent<CsvSessionLogger>()
                         ?? Undo.AddComponent<CsvSessionLogger>(sessionGo);

            var sessionSo = new SerializedObject(session);
            sessionSo.FindProperty("scenario").objectReferenceValue = scenario;
            sessionSo.FindProperty("fearedOutcomes").objectReferenceValue = catalog;
            sessionSo.FindProperty("startOnPlay").boolValue = true;
            sessionSo.FindProperty("environmentControllerBehaviour").objectReferenceValue = env;
            sessionSo.FindProperty("predictionPromptBehaviour").objectReferenceValue = promptUi;
            sessionSo.FindProperty("taskSourceBehaviour").objectReferenceValue = tracker;
            sessionSo.FindProperty("biosignalSourceBehaviour").objectReferenceValue = bio;
            sessionSo.FindProperty("sessionLoggerBehaviour").objectReferenceValue = logger;
            sessionSo.ApplyModifiedProperties();

            // --- UI bridge ---
            var bridge = uiGo.GetComponent<HeightSessionUIBridge>() ?? Undo.AddComponent<HeightSessionUIBridge>(uiGo);
            var bridgeSo = new SerializedObject(bridge);
            bridgeSo.FindProperty("session").objectReferenceValue = session;
            bridgeSo.FindProperty("ui").objectReferenceValue = promptUi;
            bridgeSo.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("[Exposure] Acrophobia scene wired: session, prompt UI, task tracker and edge marker are connected.");
        }
    }
}
#endif
