#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Exposure.EditorTools
{
    /// <summary>
    /// Builds layered depth cues into the drop below the platform.
    ///
    /// The point is not decoration. A drop reads as deep only when the eye has *staged*
    /// references between the viewer and the ground: a single distant floor gives almost no
    /// depth impression, whereas a facade that keeps counting storeys, roofs at intermediate
    /// heights and haze drifting below make the same distance feel enormous. Freeman et al.
    /// (2018) describe their atrium as deliberately featuring "many height cues (eg, balls in
    /// the air, people moving about)" and treat those cues as what brings on the symptoms
    /// people get at real heights.
    ///
    /// Everything is built from primitives on purpose: it has to survive in the blockout build,
    /// which contains no commercial assets, so this is committable and works for anyone who
    /// clones the repository.
    ///
    /// Parented under PlatformRig, because that is the world that travels down as the
    /// participant ascends -- local y = 0 is street level by construction.
    /// </summary>
    public static class HeightCuesSetup
    {
        private const string RootName = "HeightCues";
        private const string MaterialFolder = "Assets/_Exposure/Materials";

        private const float FloorHeight = 3f;   // matches HeightEnvironmentController
        private const int Storeys = 12;

        [MenuItem("Exposure/Setup Height Cues")]
        public static void Build()
        {
            var rig = GameObject.Find("PlatformRig");
            if (rig == null)
            {
                Debug.LogError("[Exposure] PlatformRig not found -- open Exposure_Acrophobia.unity first.");
                return;
            }

            Clear();

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Setup Height Cues");
            root.transform.SetParent(rig.transform, false);

            BuildOwnFacade(root.transform);
            BuildNeighbouringRoofs(root.transform);
            BuildHazeLayers(root.transform);
            BuildRisingCues(root.transform);
            BuildStreetActivity(root.transform);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Exposure] Height cues built: facade, neighbouring roofs, haze layers, " +
                      "rising cues, street activity.");
        }

        [MenuItem("Exposure/Clear Height Cues")]
        public static void Clear()
        {
            var existing = GameObject.Find(RootName);
            while (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
                existing = GameObject.Find(RootName);
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        /// <summary>
        /// The wall of the building the platform is attached to, banded once per storey.
        ///
        /// Sits at the platform's *rear* edge, not its centre. Getting this wrong put the wall
        /// through the middle of the standing area, so the participant was inside it. The wall
        /// keeps its collider for the same reason: walking into the building has to be blocked,
        /// unlike the purely decorative scenery further out.
        ///
        /// The bands are the useful part -- they keep counting storeys all the way down to the
        /// street, which is what gives the drop a continuous scale instead of a flat backdrop.
        /// </summary>
        private static void BuildOwnFacade(Transform parent)
        {
            var facade = new GameObject("OwnFacade");
            facade.transform.SetParent(parent, false);

            float height = Storeys * FloorHeight;
            const float thickness = 0.4f;

            // HeightCues sits at world z = -1.5, the platform spans world z -1..1, so its rear
            // edge is world z = -1 -> local z = 0.5. Offset by half the thickness so the wall's
            // face lands on the edge rather than straddling it.
            float wallZ = 0.5f - thickness * 0.5f;

            var wall = MakeBox("Wall", facade.transform,
                new Vector3(0f, height * 0.5f, wallZ),
                new Vector3(9f, height, thickness),
                Mat("Cue_Facade", new Color(0.55f, 0.56f, 0.58f)));
            wall.AddComponent<BoxCollider>(); // solid: you cannot walk into the building

            var windowMat = Mat("Cue_Window", new Color(0.16f, 0.20f, 0.26f));
            for (int i = 0; i < Storeys; i++)
            {
                MakeBox($"WindowBand_{i}", facade.transform,
                    new Vector3(0f, i * FloorHeight + FloorHeight * 0.62f, wallZ + thickness * 0.55f),
                    new Vector3(8.4f, 0.55f, 0.12f),
                    windowMat);
            }
        }

        /// <summary>
        /// Neighbouring buildings whose roofs sit at several different heights. These are the
        /// intermediate planes: without them the eye jumps straight from the edge to the ground
        /// and loses any sense of how far that is.
        /// </summary>
        private static void BuildNeighbouringRoofs(Transform parent)
        {
            var roofs = new GameObject("NeighbouringRoofs");
            roofs.transform.SetParent(parent, false);

            // x, z, roof height, footprint
            var layout = new[]
            {
                new Vector4(-14f,  10f,  7f,  9f),
                new Vector4( 16f,   6f, 13f, 11f),
                new Vector4(-11f,  22f, 19f,  8f),
                new Vector4( 13f,  24f,  4f, 12f),
                new Vector4(-20f,  -6f, 10f, 10f),
                new Vector4( 21f, -12f, 16f,  9f),
                new Vector4( -6f,  32f, 23f,  9f),
                new Vector4(  8f,  38f, 11f, 13f),
            };

            var body = Mat("Cue_Neighbour", new Color(0.42f, 0.44f, 0.47f));
            var roofTop = Mat("Cue_RoofTop", new Color(0.28f, 0.29f, 0.31f));

            for (int i = 0; i < layout.Length; i++)
            {
                float x = layout[i].x, z = layout[i].y, h = layout[i].z, w = layout[i].w;

                MakeBox($"Building_{i}", roofs.transform,
                    new Vector3(x, h * 0.5f, z),
                    new Vector3(w, h, w),
                    body);

                // A contrasting roof slab, so each roof reads as a surface at its own height
                // rather than as the top of an untextured block.
                MakeBox($"Roof_{i}", roofs.transform,
                    new Vector3(x, h + 0.15f, z),
                    new Vector3(w * 1.04f, 0.3f, w * 1.04f),
                    roofTop);
            }
        }

        /// <summary>
        /// Thin haze planes at a few heights below the platform. Seeing cloud or dust *below*
        /// you is a cue with no everyday equivalent at ground level, and it separates the drop
        /// into readable slabs of distance. Cheap: a handful of transparent quads, no volumetrics.
        /// </summary>
        private static void BuildHazeLayers(Transform parent)
        {
            var haze = new GameObject("HazeLayers");
            haze.transform.SetParent(parent, false);

            var mat = Mat("Cue_Haze", new Color(0.82f, 0.86f, 0.90f, 0.16f), transparent: true);
            float[] heights = { 5f, 11f, 18f, 26f };

            for (int i = 0; i < heights.Length; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = $"Haze_{i}";
                go.transform.SetParent(haze.transform, false);
                go.transform.localPosition = new Vector3(i % 2 == 0 ? -4f : 5f, heights[i], 6f + i * 3f);
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                go.transform.localScale = new Vector3(46f, 46f, 1f);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.GetComponent<Renderer>().sharedMaterial = mat;

                // Drift sideways only -- haze that climbs would read as smoke.
                var drift = go.AddComponent<FloatingHeightCue>();
                Configure(drift, riseSpeed: 0f, topY: 999f, bottomY: -999f,
                          swayAmplitude: 3.5f, swaySpeed: 0.05f);
            }
        }

        /// <summary>
        /// Objects rising slowly past the participant. They cross every depth plane on the way
        /// up, so the eye gets parallax at all levels rather than only at the floor -- this is
        /// the "balls in the air" cue from Freeman et al. (2018).
        /// </summary>
        private static void BuildRisingCues(Transform parent)
        {
            var cues = new GameObject("RisingCues");
            cues.transform.SetParent(parent, false);

            var mat = Mat("Cue_Balloon", new Color(0.90f, 0.45f, 0.30f));
            Random.InitState(20260806); // stable layout, so the demo video does not change

            for (int i = 0; i < 14; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = $"RisingCue_{i}";
                go.transform.SetParent(cues.transform, false);
                go.transform.localPosition = new Vector3(
                    Random.Range(-16f, 16f),
                    Random.Range(-1f, 34f),
                    Random.Range(3f, 26f));
                go.transform.localScale = Vector3.one * Random.Range(0.35f, 0.7f);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.GetComponent<Renderer>().sharedMaterial = mat;

                var cue = go.AddComponent<FloatingHeightCue>();
                Configure(cue, riseSpeed: Random.Range(0.25f, 0.5f), topY: 36f, bottomY: -2f,
                          swayAmplitude: Random.Range(0.15f, 0.4f), swaySpeed: Random.Range(0.4f, 0.8f));
            }
        }

        /// <summary>
        /// Small moving objects at street level. Something has to be alive down there: motion
        /// at a size the eye can compare against is what turns "a surface far away" into "a
        /// street, and I am a long way above it".
        /// </summary>
        private static void BuildStreetActivity(Transform parent)
        {
            var street = new GameObject("StreetActivity");
            street.transform.SetParent(parent, false);

            var mat = Mat("Cue_Traffic", new Color(0.75f, 0.76f, 0.78f));
            Random.InitState(4711);

            for (int i = 0; i < 10; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"Traffic_{i}";
                go.transform.SetParent(street.transform, false);
                go.transform.localPosition = new Vector3(
                    Random.Range(-22f, 22f), 0.35f, Random.Range(4f, 30f));
                go.transform.localScale = new Vector3(1.8f, 0.7f, 0.9f);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.GetComponent<Renderer>().sharedMaterial = mat;

                var cue = go.AddComponent<FloatingHeightCue>();
                Configure(cue, riseSpeed: 0f, topY: 999f, bottomY: -999f,
                          swayAmplitude: Random.Range(4f, 9f), swaySpeed: Random.Range(0.08f, 0.16f));
            }
        }

        /// <summary>
        /// Writes the private serialized fields of a FloatingHeightCue, so the generator can
        /// configure it without widening the component's public surface.
        /// </summary>
        private static void Configure(FloatingHeightCue cue, float riseSpeed, float topY,
                                      float bottomY, float swayAmplitude, float swaySpeed)
        {
            var so = new SerializedObject(cue);
            so.FindProperty("riseSpeed").floatValue = riseSpeed;
            so.FindProperty("topY").floatValue = topY;
            so.FindProperty("bottomY").floatValue = bottomY;
            so.FindProperty("swayAmplitude").floatValue = swayAmplitude;
            so.FindProperty("swaySpeed").floatValue = swaySpeed;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject MakeBox(string name, Transform parent, Vector3 pos,
                                          Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // scenery, never walked on
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static Material Mat(string name, Color color, bool transparent = false)
        {
            Directory.CreateDirectory(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { name = name };
            mat.color = color;

            if (transparent)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.SetShaderPassEnabled("ShadowCaster", false);
            }

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
#endif
