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

        // --- street grid -------------------------------------------------------------
        // One shared layout, so roads, blocks, buildings, the park and the traffic all agree
        // on where a street is. Previously the buildings and cars were placed independently,
        // which is why the cars were not on anything.
        private const float RoadWidth = 7f;
        private const float GroundMinX = -46f, GroundMaxX = 46f;
        private const float GroundMinZ = -16f, GroundMaxZ = 62f;

        /// <summary>Centre lines of the roads running left-right (along x), given as z values.</summary>
        private static readonly float[] RoadsAlongX = { 6f, 38f };

        /// <summary>Centre lines of the roads running away from the platform (along z), given as x values.</summary>
        private static readonly float[] RoadsAlongZ = { -20f, 20f };

        [MenuItem("Exposure/Setup Height Cues")]
        public static void Build()
        {
            if (AcrophobiaSceneSetup.RefuseDuringPlayMode()) return;

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
            BuildGroundPlan(root.transform);
            BuildNeighbouringRoofs(root.transform);
            BuildHazeLayers(root.transform);
            BuildRisingCues(root.transform);
            BuildStreetActivity(root.transform);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Exposure] Height cues built: facade, street grid with park, " +
                      "neighbouring roofs, haze layers, rising cues, traffic.");
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
            // The balcony's rear edge, now that it is 3 m deep: its front edge sits at world
            // z = 1, so the back is at world z = -2, and HeightCues itself sits at z = -1.5.
            float wallZ = -0.5f - thickness * 0.5f;

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
        /// The ground itself: pavement, road surfaces on the grid, lane markings and a park in
        /// the block directly in front of the platform.
        ///
        /// Roads exist mainly so the traffic has somewhere to be. A car crossing open ground
        /// reads as an object; a car following a road reads as a car, and the length of the road
        /// is what tells the eye how far down it is. The park does the same job by contrast --
        /// a patch of green among grey blocks gives the scale a second, differently-coloured
        /// reference.
        /// </summary>
        private static void BuildGroundPlan(Transform parent)
        {
            var ground = new GameObject("GroundPlan");
            ground.transform.SetParent(parent, false);

            var pavement = Mat("Cue_Pavement", new Color(0.46f, 0.46f, 0.47f));
            var asphalt = Mat("Cue_Asphalt", new Color(0.17f, 0.17f, 0.19f));
            var marking = Mat("Cue_LaneMarking", new Color(0.85f, 0.84f, 0.70f));

            float width = GroundMaxX - GroundMinX;
            float depth = GroundMaxZ - GroundMinZ;
            float midX = (GroundMinX + GroundMaxX) * 0.5f;
            float midZ = (GroundMinZ + GroundMaxZ) * 0.5f;

            // The pavement is the raised surface and the carriageway sits below it, as on a real
            // street. It was the other way round, which read as roads laid on top of the
            // pavement -- a small thing from up here, but it is exactly the kind of detail that
            // makes a place look built rather than assembled.
            const float kerbHeight = 0.16f;

            MakeBox("Pavement", ground.transform,
                new Vector3(midX, -0.15f + kerbHeight, midZ),
                new Vector3(width, 0.3f, depth),
                pavement);

            foreach (float z in RoadsAlongX)
            {
                MakeBox($"Road_X_{z}", ground.transform,
                    new Vector3(midX, -0.03f, z),
                    new Vector3(width, 0.12f, RoadWidth),
                    asphalt);

                // Dashes rather than a solid line: the repetition is itself a ruler for the eye.
                for (float x = GroundMinX + 3f; x < GroundMaxX; x += 6f)
                    MakeBox($"Mark_X_{z}_{x}", ground.transform,
                        new Vector3(x, 0.02f, z),
                        new Vector3(2.4f, 0.03f, 0.22f),
                        marking);
            }

            foreach (float x in RoadsAlongZ)
            {
                MakeBox($"Road_Z_{x}", ground.transform,
                    new Vector3(x, -0.03f, midZ),
                    new Vector3(RoadWidth, 0.12f, depth),
                    asphalt);

                for (float z = GroundMinZ + 3f; z < GroundMaxZ; z += 6f)
                    MakeBox($"Mark_Z_{x}_{z}", ground.transform,
                        new Vector3(x, 0.02f, z),
                        new Vector3(0.22f, 0.03f, 2.4f),
                        marking);
            }

            BuildPark(ground.transform);
        }

        /// <summary>
        /// A green block with trees and bushes, in the block directly in front of the platform
        /// so it is what the participant looks down onto.
        ///
        /// Trees are a cylinder plus two spheres -- deliberately crude. They are placeholders
        /// meant to be swapped for real models later, and keeping them as primitives means the
        /// blockout build stays free of commercial assets. Each tree is its own child object, so
        /// replacing them is a per-object swap rather than a rebuild.
        /// </summary>
        private static void BuildPark(Transform parent)
        {
            var park = new GameObject("Park");
            park.transform.SetParent(parent, false);

            // The block bounded by the two road pairs, inset so it does not touch the asphalt.
            float minX = RoadsAlongZ[0] + RoadWidth * 0.5f + 1.5f;
            float maxX = RoadsAlongZ[1] - RoadWidth * 0.5f - 1.5f;
            float minZ = RoadsAlongX[0] + RoadWidth * 0.5f + 1.5f;
            float maxZ = RoadsAlongX[1] - RoadWidth * 0.5f - 1.5f;

            var grass = Mat("Cue_Grass", new Color(0.30f, 0.44f, 0.24f));
            var path = Mat("Cue_ParkPath", new Color(0.62f, 0.57f, 0.46f));
            var trunk = Mat("Cue_Trunk", new Color(0.30f, 0.23f, 0.17f));
            var foliage = Mat("Cue_Foliage", new Color(0.22f, 0.40f, 0.19f));
            var bushMat = Mat("Cue_Bush", new Color(0.26f, 0.46f, 0.22f));

            MakeBox("Lawn", park.transform,
                new Vector3((minX + maxX) * 0.5f, 0.02f, (minZ + maxZ) * 0.5f),
                new Vector3(maxX - minX, 0.08f, maxZ - minZ),
                grass);

            // A diagonal path, so the green block is not a plain rectangle from above.
            var walk = MakeBox("ParkPath", park.transform,
                new Vector3((minX + maxX) * 0.5f, 0.06f, (minZ + maxZ) * 0.5f),
                new Vector3(2.2f, 0.04f, maxZ - minZ + 6f),
                path);
            walk.transform.localRotation = Quaternion.Euler(0f, 28f, 0f);

            Random.InitState(90210); // stable layout, so the demo video does not change

            // Sparse on purpose. The park now spans a whole city block, and the green *surface*
            // is what reads from thirty metres up -- packing it with trees turns it back into a
            // texture and loses the openness that made it worth having.
            for (int i = 0; i < 22; i++)
            {
                float x = Random.Range(minX + 1f, maxX - 1f);
                float z = Random.Range(minZ + 1f, maxZ - 1f);
                float height = Random.Range(4f, 7f);
                MakeTree(park.transform, $"Tree_{i}", new Vector3(x, 0f, z), height, trunk, foliage);
            }

            for (int i = 0; i < 26; i++)
            {
                float x = Random.Range(minX + 0.5f, maxX - 0.5f);
                float z = Random.Range(minZ + 0.5f, maxZ - 0.5f);
                float r = Random.Range(0.6f, 1.2f);

                var bush = MakeSphere(park.transform, $"Bush_{i}",
                    new Vector3(x, r * 0.45f, z), r, bushMat);
                bush.transform.localScale = new Vector3(r * 1.3f, r * 0.9f, r * 1.2f);
            }
        }

        /// <summary>
        /// Placeholder tree: trunk plus two offset canopy spheres, which reads as a crown rather
        /// than a lollipop from above -- and above is the only angle this is ever seen from.
        /// </summary>
        private static void MakeTree(Transform parent, string name, Vector3 basePos, float height,
                                     Material trunkMat, Material foliageMat)
        {
            var tree = new GameObject(name);
            tree.transform.SetParent(parent, false);
            tree.transform.localPosition = basePos;

            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(tree.transform, false);
            trunk.transform.localPosition = new Vector3(0f, height * 0.35f, 0f);
            trunk.transform.localScale = new Vector3(0.35f, height * 0.35f, 0.35f);
            Object.DestroyImmediate(trunk.GetComponent<Collider>());
            trunk.GetComponent<Renderer>().sharedMaterial = trunkMat;

            float crown = height * 0.42f;
            MakeSphere(tree.transform, "Canopy_A", new Vector3(0f, height * 0.75f, 0f), crown, foliageMat);
            MakeSphere(tree.transform, "Canopy_B",
                new Vector3(crown * 0.3f, height * 0.95f, crown * 0.2f), crown * 0.75f, foliageMat);
        }

        private static GameObject MakeSphere(Transform parent, string name, Vector3 pos,
                                             float diameter, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one * diameter;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>
        /// Neighbouring buildings whose roofs sit at several different heights. These are the
        /// intermediate planes: without them the eye jumps straight from the edge to the ground
        /// and loses any sense of how far that is.
        ///
        /// Placed in the blocks between the roads, not on them -- buildings standing in the
        /// middle of a carriageway undo exactly the legibility the grid was added for.
        /// </summary>
        private static void BuildNeighbouringRoofs(Transform parent)
        {
            var roofs = new GameObject("NeighbouringRoofs");
            roofs.transform.SetParent(parent, false);

            // x, z, roof height, footprint -- each sits inside a block of the grid.
            // Blocks along x: < -17.5 | -10.5..10.5 | > 17.5   (roads at -14 and 14)
            // Blocks along z: < 2.5   | 9.5..22.5   | > 29.5   (roads at 6 and 26)
            // The central block (-10.5..10.5, 9.5..22.5) is the park and stays empty.
            var layout = new[]
            {
                new Vector4(-24f,  -4f, 10f,  9f),
                new Vector4(  0f,  -5f,  7f,  9f),
                new Vector4( 24f,  -3f, 16f,  9f),
                new Vector4(-24f,  16f, 19f,  9f),
                new Vector4( 24f,  16f, 13f,  9f),
                new Vector4(-24f,  36f, 23f,  9f),
                new Vector4(  0f,  37f, 11f, 12f),
                new Vector4( 24f,  35f,  8f,  9f),
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

            // Several colours rather than one: distinguishable balloons can be tracked
            // individually as they pass, which is what produces the parallax reading. A cluster
            // of identical spheres tends to be seen as one texture instead.
            var palette = new[]
            {
                Mat("Cue_Balloon_Orange", new Color(0.90f, 0.45f, 0.30f)),
                Mat("Cue_Balloon_Yellow", new Color(0.93f, 0.79f, 0.28f)),
                Mat("Cue_Balloon_Teal",   new Color(0.25f, 0.66f, 0.64f)),
                Mat("Cue_Balloon_Pink",   new Color(0.86f, 0.42f, 0.60f)),
                Mat("Cue_Balloon_Violet", new Color(0.52f, 0.42f, 0.78f)),
                Mat("Cue_Balloon_Green",  new Color(0.45f, 0.72f, 0.35f)),
            };
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

                // Party-balloon sized, not weather-balloon sized. At the previous 35-70 cm they
                // read as spheres of unknown size drifting past, which destroys the very thing
                // they exist for: an object whose real size the eye already knows is what lets
                // it judge distance at all.
                float diameter = Random.Range(0.22f, 0.32f);
                go.transform.localScale = new Vector3(diameter, diameter * 1.25f, diameter);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.GetComponent<Renderer>().sharedMaterial = palette[i % palette.Length];

                var cue = go.AddComponent<FloatingHeightCue>();
                Configure(cue, riseSpeed: Random.Range(0.25f, 0.5f), topY: 36f, bottomY: -2f,
                          swayAmplitude: Random.Range(0.15f, 0.4f), swaySpeed: Random.Range(0.4f, 0.8f));
            }
        }

        /// <summary>
        /// Traffic driving along the roads of the grid, one lane per direction.
        ///
        /// Something has to be alive down there: motion at a size the eye can compare against
        /// is what turns "a surface far away" into "a street, and I am a long way above it".
        /// Following a road rather than drifting is what makes it read as traffic at all.
        /// </summary>
        private static void BuildStreetActivity(Transform parent)
        {
            var street = new GameObject("StreetActivity");
            street.transform.SetParent(parent, false);

            // Several body colours: a row of identical grey boxes reads as a pattern, and the
            // eye stops treating it as separate objects at separate distances.
            var paints = new[]
            {
                Mat("Cue_Car_Silver", new Color(0.76f, 0.77f, 0.79f)),
                Mat("Cue_Car_Dark",   new Color(0.16f, 0.17f, 0.20f)),
                Mat("Cue_Car_Red",    new Color(0.62f, 0.16f, 0.14f)),
                Mat("Cue_Car_Blue",   new Color(0.17f, 0.31f, 0.55f)),
                Mat("Cue_Car_White",  new Color(0.88f, 0.88f, 0.86f)),
            };

            Random.InitState(4711);
            int index = 0;

            float spanX = GroundMaxX - GroundMinX;
            float spanZ = GroundMaxZ - GroundMinZ;

            foreach (float z in RoadsAlongX)
            {
                // Two lanes, offset either side of the centre line, running opposite ways.
                AddVehicles(street.transform, paints, ref index, count: 4,
                    start: new Vector3(GroundMinX, 0.4f, z - RoadWidth * 0.22f),
                    direction: Vector3.right, loopLength: spanX, alongX: true);
                AddVehicles(street.transform, paints, ref index, count: 4,
                    start: new Vector3(GroundMaxX, 0.4f, z + RoadWidth * 0.22f),
                    direction: Vector3.left, loopLength: spanX, alongX: true);
            }

            foreach (float x in RoadsAlongZ)
            {
                AddVehicles(street.transform, paints, ref index, count: 3,
                    start: new Vector3(x - RoadWidth * 0.22f, 0.4f, GroundMinZ),
                    direction: Vector3.forward, loopLength: spanZ, alongX: false);
                AddVehicles(street.transform, paints, ref index, count: 3,
                    start: new Vector3(x + RoadWidth * 0.22f, 0.4f, GroundMaxZ),
                    direction: Vector3.back, loopLength: spanZ, alongX: false);
            }
        }

        private static void AddVehicles(Transform parent, Material[] paints, ref int index,
                                        int count, Vector3 start, Vector3 direction,
                                        float loopLength, bool alongX)
        {
            for (int i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"Traffic_{index}";
                go.transform.SetParent(parent, false);
                go.transform.localPosition = start;

                // Roughly car-sized, oriented along its own road.
                go.transform.localScale = alongX
                    ? new Vector3(Random.Range(3.6f, 4.6f), 1.4f, 1.8f)
                    : new Vector3(1.8f, 1.4f, Random.Range(3.6f, 4.6f));

                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.GetComponent<Renderer>().sharedMaterial = paints[index % paints.Length];

                var cue = go.AddComponent<StreetTrafficCue>();
                var so = new SerializedObject(cue);
                so.FindProperty("direction").vector3Value = direction;
                so.FindProperty("speed").floatValue = Random.Range(4.5f, 8f);
                so.FindProperty("loopLength").floatValue = loopLength;
                so.ApplyModifiedPropertiesWithoutUndo();

                index++;
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

            // GPU instancing is deliberately left off. It looks like the obvious win here --
            // hundreds of copies of one primitive -- but URP's SRP Batcher takes precedence and
            // bypasses instancing entirely. Measured: enabling it produced zero instanced draw
            // calls and changed nothing. The SRP Batcher is already doing the work, which is why
            // ~163 draw calls cost only 6 setPass calls.
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
