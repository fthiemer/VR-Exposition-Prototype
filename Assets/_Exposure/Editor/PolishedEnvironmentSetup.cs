#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Exposure.EditorTools
{
    /// <summary>
    /// Builds the polished dressing for the acrophobia scene: a distant city skyline, a ground
    /// plane far below, and depth fog.
    ///
    /// Deliberately a generator rather than saved scene content. The city prefabs come from a
    /// commercial pack under Assets/3rd Party Assets/, which is gitignored -- baking those
    /// references into the committed scene would leave anyone cloning the repository with a
    /// scene full of missing objects. Generating on demand keeps the committed scene the clean
    /// blockout, and the polished build is one menu click away for whoever has the pack.
    ///
    /// Run "Build City Backdrop" before the polished build, "Clear City Backdrop" before the
    /// blockout build or before committing.
    /// </summary>
    public static class PolishedEnvironmentSetup
    {
        private const string BackdropName = "CityBackdrop";
        private const string BuildingsPath =
            "Assets/3rd Party Assets/POLYBOX/hazelwoodloft/CITY_DATA_NEW/new_prefabs/" +
            "prefabs_day_buildings_skyscrapers";

        [MenuItem("Exposure/Polish/Build City Backdrop")]
        public static void BuildBackdrop()
        {
            var rig = GameObject.Find("PlatformRig");
            if (rig == null)
            {
                Debug.LogError("[Exposure] PlatformRig not found -- open Exposure_Acrophobia.unity first.");
                return;
            }

            ClearBackdrop();

            var prefabs = LoadBuildingPrefabs();
            if (prefabs.Count == 0)
            {
                Debug.LogError($"[Exposure] No building prefabs found under {BuildingsPath}. " +
                               "The POLYBOX pack is gitignored -- import it via the Asset Store " +
                               "(Package Manager > My Assets) before running this.");
                return;
            }

            // Parented to PlatformRig because that is what moves during the elevator ride: the
            // world travels down past the participant, so the city has to travel with it.
            var root = new GameObject(BackdropName);
            Undo.RegisterCreatedObjectUndo(root, "Build City Backdrop");
            root.transform.SetParent(rig.transform, false);

            BuildGroundPlane(root.transform);
            ScatterBuildings(root.transform, prefabs);
            ApplyDepthFog();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Exposure] City backdrop built from {prefabs.Count} building prefabs. " +
                      "Remember to clear it before committing or before the blockout build.");
        }

        [MenuItem("Exposure/Polish/Clear City Backdrop")]
        public static void ClearBackdrop()
        {
            var existing = GameObject.Find(BackdropName);
            while (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
                existing = GameObject.Find(BackdropName);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private static List<GameObject> LoadBuildingPrefabs()
        {
            var prefabs = new List<GameObject>();
            if (!Directory.Exists(BuildingsPath)) return prefabs;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { BuildingsPath }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab != null) prefabs.Add(prefab);
            }
            return prefabs;
        }

        /// <summary>
        /// A large plate far below, so looking down reads as "ground a long way away" rather
        /// than "void". The drop itself is what the exposure is about, so it needs a bottom.
        /// </summary>
/// <summary>
        /// The street far below, so looking down reads as "ground a long way away" rather than
        /// "void". Sits at PlatformRig's own origin: the rig is the world, and it starts level
        /// with the participant at the ground floor and travels down as they ascend, so y=0 here
        /// is street level by construction.
        /// </summary>
        private static void BuildGroundPlane(Transform parent)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "GroundPlane";
            ground.transform.SetParent(parent, false);
            ground.transform.localPosition = new Vector3(0f, -0.2f, 0f);
            ground.transform.localScale = new Vector3(60f, 1f, 60f); // 600 m across
            Object.DestroyImmediate(ground.GetComponent<Collider>());

            var renderer = ground.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = MakeMaterial("GroundFar", new Color(0.18f, 0.19f, 0.21f));
        }

        /// <summary>
        /// Buildings in a ring, kept clear of the platform footprint so nothing intersects the
        /// area the participant can physically walk in.
        /// </summary>
/// <summary>
        /// Buildings in a ring, kept clear of the platform footprint so nothing intersects the
        /// area the participant can physically walk in.
        ///
        /// Every renderer gets one of a few flat URP materials rather than the pack's own. Two
        /// reasons: the pack ships built-in-pipeline materials, which render magenta under URP,
        /// and at this distance the buildings are silhouettes anyway -- lighting them per-pixel
        /// would cost Quest 2 frame time for detail nobody can resolve through the fog.
        /// </summary>
/// <summary>
        /// Buildings in a ring around the tower, standing on the street plane.
        ///
        /// The pack is authored at city scale -- the prefabs measure 100-270 units tall, so at
        /// their native size a single one would swallow a ten-storey building. Scaled down to
        /// roughly 25-70 m and pushed out to 40-180 m, they read as an ordinary skyline seen
        /// from a rooftop, which is the whole point: the height has to feel like a real place.
        ///
        /// Every renderer gets one of a few flat URP materials rather than the pack's own. Two
        /// reasons: the pack ships built-in-pipeline materials, which render magenta under URP,
        /// and at this distance the buildings are silhouettes anyway -- lighting them per-pixel
        /// would cost Quest 2 frame time for detail nobody can resolve through the fog.
        /// </summary>
/// <summary>
        /// Buildings in a ring around the tower, standing on the street plane.
        ///
        /// The pack is authored at city scale -- the prefabs measure 100-270 units tall, so at
        /// their native size a single one would swallow a ten-storey building. Scaled down to
        /// roughly 25-70 m and pushed out to 40-180 m, they read as an ordinary skyline seen
        /// from a rooftop, which is the whole point: the height has to feel like a real place.
        ///
        /// Materials are left exactly as the pack ships them. They are built-in-pipeline
        /// materials and will render magenta until they are converted to URP -- that conversion
        /// is a deliberate manual step (Window > Rendering > Render Pipeline Converter >
        /// "Built-in to URP" > Material Upgrade), not something this generator should force.
        /// </summary>
        private static void ScatterBuildings(Transform parent, List<GameObject> prefabs)
        {
            const int count = 40;
            const float minRadius = 40f;
            const float maxRadius = 180f;

            Random.InitState(20260806); // stable layout across runs -- the video should not change

            for (int i = 0; i < count; i++)
            {
                var prefab = prefabs[Random.Range(0, prefabs.Count)];
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                if (instance == null) continue;

                float angle = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.06f, 0.06f);
                float radius = Random.Range(minRadius, maxRadius);

                // Prefab pivots sit at the building's base, so y = 0 puts them on the street.
                instance.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius);
                instance.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                float footprint = Random.Range(0.16f, 0.30f);
                instance.transform.localScale = new Vector3(footprint, Random.Range(0.14f, 0.32f), footprint);

                // Distant silhouettes are never touched, and colliders on 40 buildings cost
                // physics time for nothing.
                foreach (var collider in instance.GetComponentsInChildren<Collider>())
                    Object.DestroyImmediate(collider);
            }
        }

        /// <summary>
        /// Depth fog does three jobs at once here: it sells scale, hides how little detail the
        /// far buildings carry, and lets the far clip plane come in closer, which is what keeps
        /// this affordable on a Quest 2.
        /// </summary>
/// <summary>
        /// Depth fog does three jobs at once here: it sells scale, hides how little detail the
        /// far buildings carry, and lets the far clip plane come in closer, which is what keeps
        /// this affordable on a Quest 2. Tuned to still show the nearest ring of buildings
        /// clearly -- fog that swallows them would remove the very cue that conveys height.
        /// </summary>
        private static void ApplyDepthFog()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.62f, 0.68f, 0.75f);
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 230f;
        }

        private static Material MakeMaterial(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = name };
            material.color = color;

            const string folder = "Assets/_Exposure/Materials";
            Directory.CreateDirectory(folder);
            string path = $"{folder}/{name}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
#endif
