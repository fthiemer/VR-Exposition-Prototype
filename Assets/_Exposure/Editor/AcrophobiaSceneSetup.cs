#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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
        private const string XriAudioFolder =
            "Assets/Samples/XR Interaction Toolkit/3.4.1/Hands Interaction Demo/DemoAssets/Audio";
        private const string XriHoverClip = XriAudioFolder + "/ButtonHover.wav";
        private const string XriClickClip = XriAudioFolder + "/ButtonClick.wav";
        private const string ConfettiPath = "Assets/Samples/XR Interaction Toolkit/3.4.1/Starter Assets/" +
                                            "DemoAssets/Prefabs/Interactables/Confetti.prefab";
        private const string ExposureAudioFolder = "Assets/_Exposure/Audio";
        private const string ElevatorClip = ExposureAudioFolder + "/ElevatorRide.ogg";
        private const string CityAmbienceClip = ExposureAudioFolder + "/CityAmbience.ogg";

        [MenuItem("Exposure/Setup Acrophobia Scene")]
        public static void Setup()
        {
            if (RefuseDuringPlayMode()) return;

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
            // --- plank level geometry ---
            // The plank used to replace the whole platform, so "crossing" it was not a
            // traversal at all. Instead: the rear half of the play area stays solid ground and
            // the front half becomes a narrow plank with a drop either side. That is roughly a
            // metre of real walking, which is all a 2x2 m room-scale space allows, and the
            // participant returns by stepping back rather than being teleported.
            // Note: the surfaces live directly under AcrophobiaEnvironment, NOT under
            // PlatformRig -- PlatformRig is the surrounding world, which is what moves during
            // the elevator ride. Anything the participant stands on has to stay put.
            {
                var plank = envRoot.transform.Find("SurfacePlank");
                if (plank != null)
                {
                    plank.localPosition = new Vector3(0f, -0.1f, 2f);
                    plank.localScale = new Vector3(0.6f, 0.2f, 1f);
                }

                // Solid ground behind the plank, so there is somewhere to start from and
                // step back to. Only shown for the plank level.
                var apron = envRoot.transform.Find("PlankApron");
                if (apron == null)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = "PlankApron";
                    Undo.RegisterCreatedObjectUndo(go, "Create PlankApron");
                    go.transform.SetParent(envRoot.transform, false);
                    apron = go.transform;
                }
                apron.localPosition = new Vector3(0f, -0.1f, 1f);
                apron.localScale = new Vector3(2f, 0.2f, 1f);

                var envSo = new SerializedObject(env);
                envSo.FindProperty("plankApron").objectReferenceValue = apron.gameObject;
                envSo.ApplyModifiedProperties();
            }

            BuildPlatformBoundary(envRoot.transform, env);
            ApplyGlassMaterials(envRoot.transform);
            BuildEnvironmentAudio(envRoot.transform, env);
            BuildPostProcessing();

            // A longer ride. Three seconds barely registered as travel; the transition is what
            // sells having gone somewhere, and it is also the moment the participant has to
            // settle into the new height before being asked anything.
            var rideSo = new SerializedObject(env);
            rideSo.FindProperty("transitionSeconds").floatValue = 8f;
            rideSo.ApplyModifiedProperties();

            // --- task feedback: target marker, sound, particle burst ---
            var feedbackRoot = envRoot.transform.Find("TaskFeedback");
            if (feedbackRoot == null)
            {
                var go = new GameObject("TaskFeedback");
                Undo.RegisterCreatedObjectUndo(go, "Create TaskFeedback");
                go.transform.SetParent(envRoot.transform, false);
                feedbackRoot = go.transform;
            }

            var marker = feedbackRoot.Find("TargetMarker");
            if (marker == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = "TargetMarker";
                Undo.RegisterCreatedObjectUndo(go, "Create TargetMarker");
                go.transform.SetParent(feedbackRoot, false);
                // Flat on the floor at the edge, just above it so it does not z-fight.
                go.transform.localPosition = new Vector3(0f, 0.01f, 2.5f);
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                go.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
                Object.DestroyImmediate(go.GetComponent<Collider>()); // must not block the participant
                marker = go.transform;
            }

            // A glowing, mostly transparent marker. Primitives ship with an opaque material, so
            // tinting alpha through a property block would have had no visible effect at all.
            var markerRenderer = marker.GetComponent<Renderer>();
            if (markerRenderer != null) markerRenderer.sharedMaterial = GlowMaterial("Cue_TargetMarker");

            var markerFeedback = feedbackRoot.GetComponent<TargetMarkerFeedback>()
                                 ?? Undo.AddComponent<TargetMarkerFeedback>(feedbackRoot.gameObject);
            var markerSo = new SerializedObject(markerFeedback);
            markerSo.FindProperty("marker").objectReferenceValue = marker.gameObject;
            markerSo.FindProperty("markerRenderer").objectReferenceValue = markerRenderer;
            markerSo.ApplyModifiedProperties();

            var audioFeedback = feedbackRoot.GetComponent<AudioTaskFeedback>()
                                ?? Undo.AddComponent<AudioTaskFeedback>(feedbackRoot.gameObject);

            // Stand-in clips from the XRI samples, so something is audible before real sound
            // design exists. The hum has no equivalent there and stays procedural.
            var audioSo = new SerializedObject(audioFeedback);
            AssignClipIfEmpty(audioSo, "taskStartClip", XriHoverClip);
            AssignClipIfEmpty(audioSo, "completedClip", XriClickClip);
            audioSo.ApplyModifiedProperties();

            var particleFeedback = feedbackRoot.GetComponent<ParticleBurstFeedback>()
                                   ?? Undo.AddComponent<ParticleBurstFeedback>(feedbackRoot.gameObject);
            // Confetti burst from XRI's Starter Assets, instantiated once into the scene so
            // the feedback component has a ParticleSystem to play.
            var burst = feedbackRoot.Find("CompletionBurst");
            if (burst == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ConfettiPath);
                if (prefab != null)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, feedbackRoot);
                    go.name = "CompletionBurst";
                    Undo.RegisterCreatedObjectUndo(go, "Create CompletionBurst");
                    go.transform.localPosition = new Vector3(0f, 0.2f, 2.5f);
                    // The prefab ships rotated so it emits along -Y; aim it upwards instead,
                    // otherwise the confetti fires down through the platform.
                    go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                    burst = go.transform;
                }
                else
                {
                    Debug.LogWarning($"[Exposure] Confetti prefab not found at {ConfettiPath} -- " +
                                     "completion burst left unassigned.");
                }
            }

            if (burst != null) ScaleBurstToRoomScale(burst.gameObject);

            var particleSo = new SerializedObject(particleFeedback);
            particleSo.FindProperty("playAt").objectReferenceValue = marker;
            if (burst != null)
                particleSo.FindProperty("burst").objectReferenceValue =
                    burst.GetComponentInChildren<ParticleSystem>();
            particleSo.ApplyModifiedProperties();

            var trackerSo = new SerializedObject(tracker);
            trackerSo.FindProperty("edge").objectReferenceValue = edge;

            var list = trackerSo.FindProperty("feedbackBehaviours");
            list.ClearArray();
            AppendTo(list, markerFeedback);
            AppendTo(list, audioFeedback);
            AppendTo(list, particleFeedback);

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

            Debug.Log("[Exposure] Acrophobia scene wired: session, prompt UI, task tracker, " +
                      "edge marker and task feedback are connected.");
        }

/// <summary>
        /// Editor generators must not run in play mode. They modify the scene and then call
        /// MarkSceneDirty, which throws there -- so a run half-applies and then aborts, leaving
        /// a scene that looks set up but is not. Refusing up front is the honest behaviour.
        ///
        /// Shared by the other generators, which have the same problem.
        /// </summary>
        internal static bool RefuseDuringPlayMode()
        {
            if (!EditorApplication.isPlaying) return false;
            Debug.LogWarning("[Exposure] Editor setup does not run in play mode -- " +
                             "stop play mode and run it again.");
            return true;
        }


        private static void AppendTo(SerializedProperty list, Object value)
        {
            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = value;
        }

/// <summary>
        /// Fills a clip field only if it is still empty, so a real clip assigned by hand is
        /// never overwritten by re-running the setup.
        /// </summary>
        private static void AssignClipIfEmpty(SerializedObject so, string property, string assetPath)
        {
            var prop = so.FindProperty(property);
            if (prop == null || prop.objectReferenceValue != null) return;

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (clip == null)
            {
                Debug.LogWarning($"[Exposure] Audio clip not found at {assetPath} -- " +
                                 $"{property} stays empty and falls back to a generated tone.");
                return;
            }
            prop.objectReferenceValue = clip;
        }

        /// <summary>
        /// Transparent, emissive material for the target marker. Both properties matter: without
        /// transparency the marker hides the floor it is meant to point at, and without emission
        /// it disappears against a bright sky.
        /// </summary>
/// <summary>
        /// Transparent unlit material for the target marker.
        ///
        /// Unlit rather than Lit-with-emission on purpose. Emission looked like the obvious
        /// route, but URP recomputes material keywords on import and kept switching _EMISSION
        /// back off, leaving the marker dull. Unlit sidesteps that entirely: it always renders
        /// at full colour regardless of lighting, which is exactly the "glowing" read a marker
        /// wants -- and it stays legible against both a bright sky and a dark floor, which a lit
        /// surface does not. It is also cheaper, which matters on a Quest 2.
        /// </summary>
        private static Material GlowMaterial(string name)
        {
            const string folder = "Assets/_Exposure/Materials";
            System.IO.Directory.CreateDirectory(folder);
            string path = $"{folder}/{name}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader) { name = name };

            var tint = new Color(0.25f, 0.7f, 1f, 0.18f);
            mat.color = tint;
            mat.SetColor("_BaseColor", tint);

            mat.SetFloat("_Surface", 1f); // transparent
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetShaderPassEnabled("ShadowCaster", false);

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

/// <summary>
        /// Resizes the confetti so it is actually visible here.
        ///
        /// The XRI prefab emits particles of 0.001 units -- one millimetre. In their demo the
        /// system sits on a scaled-up parent, but dropped into a room-scale scene at 1 unit =
        /// 1 metre the burst plays correctly and is simply too small to see, which is why it
        /// looked like nothing happened on completion.
        /// </summary>
        private static void ScaleBurstToRoomScale(GameObject burstRoot)
        {
            foreach (var ps in burstRoot.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 3.2f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.8f);
                main.gravityModifier = 0.55f; // confetti should fall, not hang
                main.maxParticles = 160;

                var emission = ps.emission;
                emission.SetBurst(0, new ParticleSystem.Burst(0f, 120));
            }
        }

/// <summary>
        /// Makes the glass surfaces actually see-through.
        ///
        /// They already carried a low alpha, but on an opaque material (render queue 2000) that
        /// value is simply ignored -- so the "glass floor", the whole point of that difficulty
        /// step, was a solid slab you could not look through. Transparency has to be switched on
        /// at the material, not just expressed as an alpha value.
        /// </summary>
        private static void ApplyGlassMaterials(Transform envRoot)
        {
            var glass = TransparentLit("Cue_Glass", new Color(0.68f, 0.80f, 0.86f, 0.22f), smoothness: 0.95f);

            foreach (var name in new[] { "SurfaceGlass", "RailingGlass" })
            {
                var t = envRoot.Find(name);
                if (t == null) continue;
                var r = t.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = glass;
            }
        }

/// <summary>
        /// A low kerb along the two open sides of the platform.
        ///
        /// Room-scale means the participant walks for real, and while looking down or straight
        /// ahead they cannot see where the floor ends. A kerb they can feel underfoot and catch
        /// in peripheral vision is the cheapest way to make the walkable area legible without
        /// putting up a wall that would remove the exposure.
        ///
        /// Deliberately low: it marks the boundary, it does not protect from the edge. The front
        /// stays open -- that is the drop the exercise is about -- and the rear is the building.
        /// </summary>
        private static void BuildPlatformBoundary(Transform envRoot, HeightEnvironmentController env)
        {
            var boundary = envRoot.Find("PlatformBoundary");
            if (boundary == null)
            {
                var go = new GameObject("PlatformBoundary");
                Undo.RegisterCreatedObjectUndo(go, "Create PlatformBoundary");
                go.transform.SetParent(envRoot, false);
                boundary = go.transform;
            }

            // Rebuild the kerbs so repeated runs cannot stack duplicates.
            for (int i = boundary.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(boundary.GetChild(i).gameObject);

            var mat = TransparentLit("Cue_Boundary", new Color(0.95f, 0.78f, 0.25f, 0.75f), smoothness: 0.2f);

            // Platform spans local z 0.5..2.5 and x -1..1. Kerbs run along both open sides.
            foreach (var side in new[] { -1f, 1f })
            {
                var kerb = GameObject.CreatePrimitive(PrimitiveType.Cube);
                kerb.name = side < 0 ? "Kerb_Left" : "Kerb_Right";
                kerb.transform.SetParent(boundary, false);
                kerb.transform.localPosition = new Vector3(side * 1.02f, 0.04f, 1.5f);
                kerb.transform.localScale = new Vector3(0.06f, 0.09f, 2f);
                Object.DestroyImmediate(kerb.GetComponent<Collider>()); // must not trip anyone
                kerb.GetComponent<Renderer>().sharedMaterial = mat;
            }

            var so = new SerializedObject(env);
            so.FindProperty("platformBoundary").objectReferenceValue = boundary.gameObject;
            so.ApplyModifiedProperties();
        }


        /// <summary>
        /// Global post-processing volume: bloom, tonemapping and a light vignette.
        ///
        /// Kept deliberately restrained. On a Quest 2 every post effect is paid for twice, once
        /// per eye, so this is the subtle end of each effect rather than the cinematic one —
        /// bloom in fast mode with a high threshold, so only the sky and bright edges pick it up
        /// and the scene does not turn milky. Tonemapping is the one that does the most work for
        /// the least cost: without it the bright sky against the dark drop clips to flat white.
        ///
        /// Also switches post-processing on for the main camera and warns if the URP asset has
        /// it disabled globally, which silently defeats the whole volume.
        /// </summary>
        private static void BuildPostProcessing()
        {
            const string folder = "Assets/_Exposure/Settings";
            System.IO.Directory.CreateDirectory(folder);
            const string profilePath = folder + "/ExposurePostProcessing.asset";

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }

            var bloom = EnsureEffect<Bloom>(profile, profilePath);
            bloom.active = true;
            // Threshold has to sit below 1.0 while the URP asset has HDR off: without HDR the
            // colour buffer clamps at 1.0, so a higher threshold is never crossed and the whole
            // effect silently does nothing.
            bool hdr = UniversalRenderPipeline.asset != null && UniversalRenderPipeline.asset.supportsHDR;
            bloom.threshold.overrideState = true; bloom.threshold.value = hdr ? 1.05f : 0.85f;
            bloom.intensity.overrideState = true; bloom.intensity.value = 0.55f;
            bloom.scatter.overrideState = true;   bloom.scatter.value = 0.6f;
            bloom.highQualityFiltering.overrideState = true;
            bloom.highQualityFiltering.value = false; // fast mode: this is a standalone headset

            var tonemapping = EnsureEffect<Tonemapping>(profile, profilePath);
            tonemapping.active = true;
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = TonemappingMode.Neutral; // ACES shifts colour too hard here

            var vignette = EnsureEffect<Vignette>(profile, profilePath);
            vignette.active = true;
            vignette.intensity.overrideState = true; vignette.intensity.value = 0.18f;
            vignette.smoothness.overrideState = true; vignette.smoothness.value = 0.6f;

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var volumeGo = GameObject.Find("ExposurePostProcessing");
            if (volumeGo == null)
            {
                volumeGo = new GameObject("ExposurePostProcessing");
                Undo.RegisterCreatedObjectUndo(volumeGo, "Create ExposurePostProcessing");
            }
            if (volumeGo.GetComponent<Volume>() == null) Undo.AddComponent<Volume>(volumeGo);

            var volume = volumeGo.GetComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = profile;

            // A volume alone does nothing if the camera does not render post-processing.
            var cam = Camera.main;
            if (cam != null)
            {
                var data = cam.GetComponent<UniversalAdditionalCameraData>();
                if (data != null) data.renderPostProcessing = true;
                else Debug.LogWarning("[Exposure] Main camera has no URP camera data -- " +
                                      "post-processing could not be switched on.");
            }
            else
            {
                Debug.LogWarning("[Exposure] No MainCamera found -- post-processing left off on the camera.");
            }

            var urp = UniversalRenderPipeline.asset;
            if (urp != null && !urp.supportsHDR)
                Debug.Log("[Exposure] URP asset has HDR off. Bloom still works, but its " +
                          "threshold above 1.0 has less to catch -- worth checking on device.");
        }

        /// <summary>
        /// Adds a volume effect to a profile *and persists it*.
        ///
        /// VolumeProfile.Add only builds the component in memory -- each effect is a separate
        /// object that has to be attached to the asset file explicitly, or the profile
        /// serialises with an empty component list. It looks correct until the next domain
        /// reload, at which point every effect is silently gone.
        /// </summary>
        private static T EnsureEffect<T>(VolumeProfile profile, string profilePath)
            where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var existing)) return existing;

            var effect = profile.Add<T>(true);
            effect.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(effect, profilePath);
            return effect;
        }

        /// <summary>
        /// Creates the two ambient audio sources and hands them to the environment controller.
        ///
        /// Both are 2D (spatialBlend 0): they are meant to surround the participant rather than
        /// come from a point, and a positional source would swing around the head on every turn.
        /// The city loop is what actually sells the height -- traffic heard faintly from far
        /// below places the drop in a way a silent void never does.
        /// </summary>
        private static void BuildEnvironmentAudio(Transform envRoot, HeightEnvironmentController env)
        {
            var elevator = EnsureLoopingSource(envRoot, "ElevatorAudio", ElevatorClip,
                                               playOnAwake: false, volume: 0.7f, loop: false);
            var city = EnsureLoopingSource(envRoot, "CityAmbienceAudio", CityAmbienceClip,
                                           playOnAwake: true, volume: 0.35f, loop: true);

            var so = new SerializedObject(env);
            if (elevator != null) so.FindProperty("elevatorAudio").objectReferenceValue = elevator;
            if (city != null) so.FindProperty("cityAmbienceAudio").objectReferenceValue = city;
            so.ApplyModifiedProperties();
        }

        private static AudioSource EnsureLoopingSource(Transform parent, string name, string clipPath,
                                                       bool playOnAwake, float volume, bool loop)
        {
            var t = parent.Find(name);
            if (t == null)
            {
                var go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "Create " + name);
                go.transform.SetParent(parent, false);
                t = go.transform;
            }

            // Re-query rather than using what AddComponent returns: on a GameObject created in
            // the same run, that reference does not resolve to the live component yet and every
            // property assignment throws MissingComponentException.
            if (t.GetComponent<AudioSource>() == null) Undo.AddComponent<AudioSource>(t.gameObject);
            var source = t.GetComponent<AudioSource>();

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            if (clip == null)
            {
                Debug.LogWarning($"[Exposure] Audio clip not found at {clipPath} -- {name} stays silent.");
                return source;
            }

            source.clip = clip;
            source.loop = loop;
            source.playOnAwake = playOnAwake;
            source.volume = volume;
            source.spatialBlend = 0f;
            return source;
        }

        private static Material TransparentLit(string name, Color color, float smoothness)
        {
            const string folder = "Assets/_Exposure/Materials";
            System.IO.Directory.CreateDirectory(folder);
            string path = $"{folder}/{name}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { name = name };
            mat.color = color;
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);

            mat.SetFloat("_Surface", 1f); // transparent
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }



    }
}
#endif
