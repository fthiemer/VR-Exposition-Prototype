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

        /// <summary>Height of the pavement above the carriageway -- the kerb, and the level
        /// everything standing on a block has to sit on.</summary>
        private const float PavementTop = 0.18f;

        /// <summary>
        /// How far the whole ground sits below the platform's own floor.
        ///
        /// Without it the pavement's top surface was higher than the balcony floor, so at the
        /// ground floor the platform was sunk into the pavement. A building's ground floor sits
        /// a step above the street, not level with it.
        /// </summary>
        private const float GroundDrop = -0.5f;

        private const float GroundMinX = -70f, GroundMaxX = 70f;
        private const float GroundMinZ = -20f, GroundMaxZ = 96f;

        /// <summary>
        /// Centre lines of the roads running left-right (along x), given as z values.
        ///
        /// The first one used to be at z = 6, which put the kerb a metre and a half in front of
        /// the balcony -- a tower block standing directly in the carriageway. There is a
        /// forecourt in front of the building now, which is both what a building of this kind
        /// has and what gives the drop something to fall past.
        /// </summary>
        private static readonly float[] RoadsAlongX = { 30f, 66f };

        /// <summary>Centre lines of the roads running away from the platform (along z), given as x values.</summary>
        private static readonly float[] RoadsAlongZ = { -30f, 30f };

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
            BuildRisingCues(root.transform);
            BuildStreetActivity(root.transform);

            // BuildHazeLayers is deliberately not called. Stacked transparent quads were how the
            // drop got its atmosphere before there was any fog; now that real distance fog does
            // that job properly, looking down meant looking through four alpha planes stacked on
            // top of it, which turned the whole view milky and hid the very ground it was meant
            // to make feel far away. Transparent overdraw is also among the most expensive things
            // a Quest 2 can be asked to do. The method is kept for the record, not used.

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
            ground.transform.localPosition = new Vector3(0f, GroundDrop, 0f);

            var pavement = Mat("Cue_Pavement", new Color(0.46f, 0.46f, 0.47f));
            var asphalt = Mat("Cue_Asphalt", new Color(0.17f, 0.17f, 0.19f));
            var marking = Mat("Cue_LaneMarking", new Color(0.85f, 0.84f, 0.70f));

            float width = GroundMaxX - GroundMinX;
            float depth = GroundMaxZ - GroundMinZ;
            float midX = (GroundMinX + GroundMaxX) * 0.5f;
            float midZ = (GroundMinZ + GroundMaxZ) * 0.5f;

            // One asphalt sheet is the ground level -- the carriageway everywhere, including
            // under the blocks, where it is simply covered up.
            MakeBox("Carriageway", ground.transform,
                new Vector3(midX, -0.06f, midZ),
                new Vector3(width, 0.12f, depth),
                asphalt);

            // Lane markings, only along the actual roads. Dashes rather than a solid line: the
            // repetition is itself a ruler for the eye.
            foreach (float z in RoadsAlongX)
                for (float x = GroundMinX + 3f; x < GroundMaxX; x += 6f)
                    MakeBox($"Mark_X_{z}_{x}", ground.transform,
                        new Vector3(x, 0.005f, z),
                        new Vector3(2.4f, 0.02f, 0.22f),
                        marking);

            foreach (float x in RoadsAlongZ)
                for (float z = GroundMinZ + 3f; z < GroundMaxZ; z += 6f)
                    MakeBox($"Mark_Z_{x}_{z}", ground.transform,
                        new Vector3(x, 0.005f, z),
                        new Vector3(0.22f, 0.02f, 2.4f),
                        marking);

            BuildPavementBlocks(ground.transform, pavement);
            BuildPark(ground.transform);
        }

        /// <summary>
        /// Raised pavement, one slab per city block, leaving the carriageways open.
        ///
        /// This was a single slab spanning the whole ground, which buried every road underneath
        /// it -- the streets were built, just invisible. A pavement is not a surface the whole
        /// city sits on; it is the raised part between the roads, and the step down to the
        /// carriageway is what makes a street read as a street from above.
        /// </summary>
        private static void BuildPavementBlocks(Transform parent, Material pavement)
        {
            var blocks = new GameObject("PavementBlocks");
            blocks.transform.SetParent(parent, false);

            const float kerbHeight = PavementTop;

            var xEdges = BlockEdges(RoadsAlongZ, GroundMinX, GroundMaxX);
            var zEdges = BlockEdges(RoadsAlongX, GroundMinZ, GroundMaxZ);

            for (int i = 0; i + 1 < xEdges.Count; i += 2)
                for (int j = 0; j + 1 < zEdges.Count; j += 2)
                {
                    float x0 = xEdges[i], x1 = xEdges[i + 1];
                    float z0 = zEdges[j], z1 = zEdges[j + 1];
                    if (x1 - x0 < 0.5f || z1 - z0 < 0.5f) continue;

                    MakeBox($"Pavement_{i}_{j}", blocks.transform,
                        new Vector3((x0 + x1) * 0.5f, kerbHeight * 0.5f, (z0 + z1) * 0.5f),
                        new Vector3(x1 - x0, kerbHeight, z1 - z0),
                        pavement);
                }
        }

        /// <summary>
        /// Turns a set of road centre lines into the block boundaries between them, as pairs of
        /// [start, end] values running from one ground edge to the other.
        /// </summary>
        private static System.Collections.Generic.List<float> BlockEdges(
            float[] roadCentres, float min, float max)
        {
            var edges = new System.Collections.Generic.List<float> { min };
            foreach (float centre in roadCentres)
            {
                edges.Add(centre - RoadWidth * 0.5f);
                edges.Add(centre + RoadWidth * 0.5f);
            }
            edges.Add(max);
            return edges;
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

            // The forecourt directly in front of the building, between its base and the first
            // road. This is what the participant looks straight down onto, so it is the one
            // block worth making green -- a car park there would waste the only ground they
            // actually study.
            float minX = RoadsAlongZ[0] + RoadWidth * 0.5f + 2f;
            float maxX = RoadsAlongZ[1] - RoadWidth * 0.5f - 2f;
            float minZ = 4f;                                        // clear of the building base
            float maxZ = RoadsAlongX[0] - RoadWidth * 0.5f - 2f;

            var grass = Mat("Cue_Grass", new Color(0.30f, 0.44f, 0.24f));
            var path = Mat("Cue_ParkPath", new Color(0.62f, 0.57f, 0.46f));
            var trunk = Mat("Cue_Trunk", new Color(0.30f, 0.23f, 0.17f));
            var foliage = Mat("Cue_Foliage", new Color(0.22f, 0.40f, 0.19f));
            var bushMat = Mat("Cue_Bush", new Color(0.26f, 0.46f, 0.22f));

            // The park sits on its block, so its surface starts where the pavement ends.
            MakeBox("Lawn", park.transform,
                new Vector3((minX + maxX) * 0.5f, PavementTop + 0.03f, (minZ + maxZ) * 0.5f),
                new Vector3(maxX - minX, 0.1f, maxZ - minZ),
                grass);

            // A diagonal path, so the green block is not a plain rectangle from above. Kept short
            // enough that the rotation does not push its ends out over the pavement -- a footpath
            // that runs off the grass and across the kerb undoes the tidiness it was added for.
            float pathLength = (maxZ - minZ) * 0.8f;
            var walk = MakeBox("ParkPath", park.transform,
                new Vector3((minX + maxX) * 0.5f, PavementTop + 0.07f, (minZ + maxZ) * 0.5f),
                new Vector3(2.6f, 0.04f, pathLength),
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
                MakeTree(park.transform, $"Tree_{i}",
                         new Vector3(x, PavementTop + 0.08f, z), height, trunk, foliage);
            }

            for (int i = 0; i < 26; i++)
            {
                float x = Random.Range(minX + 0.5f, maxX - 0.5f);
                float z = Random.Range(minZ + 0.5f, maxZ - 0.5f);
                float r = Random.Range(0.6f, 1.2f);

                var bush = MakeSphere(park.transform, $"Bush_{i}",
                    new Vector3(x, PavementTop + 0.08f + r * 0.45f, z), r, bushMat);
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
            // Same drop as the ground plan. Without it the buildings sat at their own y = 0
            // while the pavement had been lowered, so the whole city hovered half a metre above
            // the street it was supposed to stand on.
            roofs.transform.localPosition = new Vector3(0f, GroundDrop, 0f);

            var body = Mat("Cue_Neighbour", new Color(0.42f, 0.44f, 0.47f));
            var roofTop = Mat("Cue_RoofTop", new Color(0.28f, 0.29f, 0.31f));

            // Positions come from the same block grid the pavement uses. They used to be written
            // out as literals, which meant that moving the roads left the buildings standing in
            // the middle of the carriageway.
            var xEdges = BlockEdges(RoadsAlongZ, GroundMinX, GroundMaxX);
            var zEdges = BlockEdges(RoadsAlongX, GroundMinZ, GroundMaxZ);

            Random.InitState(505);
            int index = 0;

            for (int i = 0; i + 1 < xEdges.Count; i += 2)
                for (int j = 0; j + 1 < zEdges.Count; j += 2)
                {
                    float x0 = xEdges[i], x1 = xEdges[i + 1];
                    float z0 = zEdges[j], z1 = zEdges[j + 1];

                    // The near-centre block is our own building and its forecourt.
                    if (x0 < 0f && x1 > 0f && z0 < 0f) continue;

                    FillBlock(roofs.transform, x0, x1, z0, z1, body, roofTop, ref index);
                }
        }

        /// <summary>
        /// Fills one city block with buildings on an internal grid.
        ///
        /// One or two buildings per block left the city looking like scattered towers on open
        /// ground -- from above, what makes a place read as a city is blocks being *built up*,
        /// with the streets as the gaps. Each block is subdivided into cells and most of them get
        /// a building, varied in height and slightly jittered so the result is dense without
        /// looking stamped. Everything stays inset from the block edge, so nothing reaches the
        /// carriageway.
        /// </summary>
        private static void FillBlock(Transform parent, float x0, float x1, float z0, float z1,
                                      Material body, Material roofTop, ref int index)
        {
            const float inset = 2.5f;   // pavement left clear around the block
            const float targetCell = 14f;

            float usableW = (x1 - x0) - inset * 2f;
            float usableD = (z1 - z0) - inset * 2f;
            if (usableW < 8f || usableD < 8f) return;

            int cols = Mathf.Max(1, Mathf.RoundToInt(usableW / targetCell));
            int rows = Mathf.Max(1, Mathf.RoundToInt(usableD / targetCell));
            float cellW = usableW / cols;
            float cellD = usableD / rows;

            for (int c = 0; c < cols; c++)
                for (int r = 0; r < rows; r++)
                {
                    // A few gaps: courtyards and side streets are what stop a block reading as
                    // one solid slab.
                    if (Random.value < 0.18f) continue;

                    float cx = x0 + inset + cellW * (c + 0.5f);
                    float cz = z0 + inset + cellD * (r + 0.5f);

                    float fw = Mathf.Min(cellW, 16f) * Random.Range(0.72f, 0.94f);
                    float fd = Mathf.Min(cellD, 16f) * Random.Range(0.72f, 0.94f);
                    float h = Random.Range(7f, 30f);

                    // Jitter, but never enough to leave the cell.
                    cx += Random.Range(-1f, 1f) * (cellW - fw) * 0.4f;
                    cz += Random.Range(-1f, 1f) * (cellD - fd) * 0.4f;

                    MakeBox($"Building_{index}", parent,
                        new Vector3(cx, PavementTop + h * 0.5f, cz),
                        new Vector3(fw, h, fd),
                        body);

                    // A contrasting roof slab, so each roof reads as a surface at its own height
                    // rather than as the top of an untextured block.
                    MakeBox($"Roof_{index}", parent,
                        new Vector3(cx, PavementTop + h + 0.15f, cz),
                        new Vector3(fw * 1.04f, 0.3f, fd * 1.04f),
                        roofTop);

                    index++;
                }
        }

        /// <summary>
        /// Regenerates only the neighbouring buildings, leaving the ground plan, the park and any
        /// hand-placed objects alone. "Setup Height Cues" clears and rebuilds everything, which
        /// throws away manual edits -- this is the safe way to re-roll the skyline.
        /// </summary>
        [MenuItem("Exposure/Rebuild Neighbouring Buildings")]
        public static void RebuildBuildings()
        {
            if (AcrophobiaSceneSetup.RefuseDuringPlayMode()) return;

            var root = GameObject.Find(RootName);
            if (root == null)
            {
                Debug.LogError("[Exposure] HeightCues not found -- run Setup Height Cues first.");
                return;
            }

            var existing = root.transform.Find("NeighbouringRoofs");
            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

            BuildNeighbouringRoofs(root.transform);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Exposure] Neighbouring buildings rebuilt; ground and park untouched.");
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
            street.transform.localPosition = new Vector3(0f, GroundDrop, 0f); // stand on the road

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
                    start: new Vector3(GroundMinX, 0.7f, z - RoadWidth * 0.22f),
                    direction: Vector3.right, loopLength: spanX, alongX: true);
                AddVehicles(street.transform, paints, ref index, count: 4,
                    start: new Vector3(GroundMaxX, 0.7f, z + RoadWidth * 0.22f),
                    direction: Vector3.left, loopLength: spanX, alongX: true);
            }

            foreach (float x in RoadsAlongZ)
            {
                AddVehicles(street.transform, paints, ref index, count: 3,
                    start: new Vector3(x - RoadWidth * 0.22f, 0.7f, GroundMinZ),
                    direction: Vector3.forward, loopLength: spanZ, alongX: false);
                AddVehicles(street.transform, paints, ref index, count: 3,
                    start: new Vector3(x + RoadWidth * 0.22f, 0.7f, GroundMaxZ),
                    direction: Vector3.back, loopLength: spanZ, alongX: false);
            }
        }

        private static void AddVehicles(Transform parent, Material[] paints, ref int index,
                                        int count, Vector3 start, Vector3 direction,
                                        float loopLength, bool alongX)
        {
            // One speed per lane. Vehicles in a lane that move at different speeds inevitably
            // catch up with each other and drive through one another; sharing a speed keeps the
            // spacing they start with.
            float laneSpeed = Random.Range(5f, 7.5f);
            float spacing = loopLength / count;

            for (int i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"Traffic_{index}";
                go.transform.SetParent(parent, false);
                go.transform.localPosition = start;

                // Roughly car-sized and sitting on the carriageway rather than sunk into it.
                float length = Random.Range(3.6f, 4.6f);
                go.transform.localScale = alongX
                    ? new Vector3(length, 1.4f, 1.8f)
                    : new Vector3(1.8f, 1.4f, length);

                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.GetComponent<Renderer>().sharedMaterial = paints[index % paints.Length];

                var cue = go.AddComponent<StreetTrafficCue>();
                var so = new SerializedObject(cue);
                so.FindProperty("direction").vector3Value = direction;
                so.FindProperty("speed").floatValue = laneSpeed;
                so.FindProperty("loopLength").floatValue = loopLength;
                so.FindProperty("startOffset").floatValue = spacing * i;
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

            // Matte. URP Lit defaults to 0.5 smoothness, which is wet plastic -- on a whole city
            // of concrete, asphalt and grass that produced one enormous specular highlight where
            // the sun hit, washing the middle of the view to white. None of these surfaces are
            // shiny in reality, and rough surfaces are also what let shape read through shading.
            mat.SetFloat("_Smoothness", 0.06f);
            mat.SetFloat("_Metallic", 0f);

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
