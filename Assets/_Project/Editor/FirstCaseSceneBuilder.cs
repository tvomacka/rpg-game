using System;
using System.IO;
using RpgGame.CameraControl;
using RpgGame.Interaction;
using RpgGame.Movement;
using RpgGame.Occlusion;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RpgGame.Editor
{
    /// <summary>
    /// Generates the FirstCase scene from the KayKit placeholder prefabs (see <see cref="PlaceholderArtBuilder"/>):
    /// yard with fence, two-room house with cutaway walls, roof that hides when the player is inside, the player
    /// and two NPCs, click marker, isometric Cinemachine camera and baked NavMesh.
    /// The scene is an output of this script — hand edits are lost on rebuild.
    /// Batch mode: Unity.exe -batchmode -projectPath . -executeMethod RpgGame.Editor.FirstCaseSceneBuilder.BuildFromCommandLine
    /// </summary>
    public static class FirstCaseSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/FirstCase.unity";
        private const string SceneDataFolder = "Assets/_Project/Scenes/FirstCase";
        private const string NavMeshPath = SceneDataFolder + "/NavMesh-Level.asset";
        private const string MaterialsFolder = "Assets/_Project/Art/Placeholder";
        private const string MovementSettingsPath = "Assets/_Project/Data/PlayerMovement.asset";
        private const string InputActionsPath = "Assets/_Project/Input/GameControls.inputactions";
        private const string InteractionSettingsPath = "Assets/_Project/Data/Interaction.asset";
        private const string InteractablesFolder = "Assets/_Project/Data/Interactables";
        private const string HoverCursorPath = MaterialsFolder + "/T_CursorInteract.png";

        private const int IgnoreRaycastLayer = 2;
        private const int NotWalkableArea = 1; // built-in NavMesh area index

        // House footprint (metres), centred on the origin. Camera looks from the south-west (−x, −z).
        private const float HouseWidth = 10f;   // x
        private const float HouseDepth = 6f;    // z
        private const float WallThickness = 0.4f;
        private const float FullWallHeight = 3f; // a KayKit wall at KayKitScale
        private const float CutawayWallHeight = PlaceholderArtBuilder.LowWallHeight;
        private const float DoorWidth = 1.6f;
        private const float FrontDoorX = -2.5f; // front door in the study's south wall

        /// <summary>Inner face of the north wall: things hung on it or standing against it go here.</summary>
        private const float NorthInner = HouseDepth / 2f - WallThickness / 2f;
        private const float EastInner = HouseWidth / 2f - WallThickness / 2f;

        // KayKit house pieces at KayKitScale: 3 m long, 0.75 m thick (measured; checked in Build).
        private const float WallPieceLength = 3f;
        private const float WallPieceThickness = 0.75f;
        private const float FloorTileSize = 3f;

        private const float CameraPitch = IsometricView.DefaultPitch;
        private const float CameraYaw = IsometricView.DefaultYaw;
        private const float CameraDistance = 40f;
        private const float StartOrthoSize = 7f;

        private static readonly Vector3 PlayerStart = new(FrontDoorX, 0f, -7f);

        // Prefabs from PlaceholderArtBuilder.
        private const string Prefabs = PlaceholderArtBuilder.PrefabRoot;
        private const string PropPrefabs = PlaceholderArtBuilder.PropFolder;
        private const string WallPrefab = PropPrefabs + "/House_Wall.prefab";
        private const string LowWallPrefab = PropPrefabs + "/" + PlaceholderArtBuilder.LowWallPrefab + ".prefab";
        private const string WindowPrefab = PropPrefabs + "/House_WallWindow.prefab";
        private const string DoorwayPrefab = PropPrefabs + "/House_WallDoorway.prefab";
        private const string FloorPrefab = PropPrefabs + "/House_Floor.prefab";

        [MenuItem("RpgGame/Rebuild FirstCase Scene")]
        public static void Rebuild()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
            Debug.Log($"[FirstCaseSceneBuilder] Rebuilt {ScenePath}");
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
                Debug.Log($"[FirstCaseSceneBuilder] Rebuilt {ScenePath}");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        private static void Build()
        {
            // New scene FIRST: NewScene(Single) unloads unused assets, which would kill any asset loaded before it.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            EnsureFolder(SceneDataFolder);
            EnsureFolder(InteractablesFolder);
            CheckPieceSize(WallPrefab, WallPieceLength, FullWallHeight, WallPieceThickness);
            CheckPieceSize(FloorPrefab, FloorTileSize, float.NaN, FloorTileSize);
            var mats = new Materials();
            MovementSettings movement = GetOrCreateMovementSettings();
            InteractionSettings interaction = GetOrCreateInteractionSettings();
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath)
                          ?? throw new InvalidOperationException($"Missing input actions at {InputActionsPath}");

            GameObject level = BuildLevel(mats);
            BuildInteractables(level.transform, interaction);
            BuildRoof(mats);
            ClickMarker marker = BuildClickMarker(mats);
            GameObject player = BuildPlayer(movement, actions, marker);
            SetRef(marker, "_mover", player.GetComponent<PlayerMover>());
            Camera main = BuildCamera(player.transform, actions, player.GetComponent<ClickToMoveInput>());
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            InteractionUi ui = BuildInteractionUi();
            AddPlayerInteraction(player, actions, interaction, ui, marker, main);

            BakeNavMesh(level.GetComponent<NavMeshSurface>());

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException($"Failed to save {ScenePath}");
            AssetDatabase.SaveAssets();
        }

        // ---------- Geometry ----------

        private static GameObject BuildLevel(Materials mats)
        {
            var level = new GameObject("Level");
            var surface = level.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            // Physics colliders, not render meshes: the invisible full-height wall blockers must be baked.
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            // Big enough that the ground's edge stays off-screen even at maximum zoom-out.
            Box("Yard", level.transform, new Vector3(0f, -0.1f, 0f), new Vector3(80f, 0.2f, 80f), mats.Grass);

            var house = new GameObject("House").transform;
            house.SetParent(level.transform, false);
            BuildFloor(house);
            Transform walls = new GameObject("Walls").transform; // visual only: no colliders
            walls.SetParent(house, false);
            Transform blockers = NotWalkableGroup("WallBlockers", house);

            float hw = HouseWidth / 2f, hd = HouseDepth / 2f, t = WallThickness;

            // Back walls (camera looks at their inner face): full height. The study's window is in the north wall,
            // and the kitchen has a (closed) back door in the east wall.
            Wall("Wall_North", walls, blockers, -hw - t / 2f, hw + t / 2f, hd, alongX: true,
                WallPrefab, WindowPrefab, WallPrefab, WallPrefab);
            Wall("Wall_East", walls, blockers, -hd, hd, hw, alongX: false, DoorwayPrefab, WallPrefab);

            // Camera-facing walls: low cutaways so the interior stays visible.
            float doorL = FrontDoorX - DoorWidth / 2f, doorR = FrontDoorX + DoorWidth / 2f;
            LowWall("Wall_South_A", walls, blockers, -hw - t / 2f, doorL, -hd, alongX: true);
            LowWall("Wall_South_B", walls, blockers, doorR, hw + t / 2f, -hd, alongX: true);
            LowWall("Wall_West", walls, blockers, -hd, hd, -hw, alongX: false);

            // Study | kitchen partition with a doorway. Its study-side face points at the camera, so it is low too.
            LowWall("Partition_A", walls, blockers, -hd, -DoorWidth / 2f, 0f, alongX: false);
            LowWall("Partition_B", walls, blockers, DoorWidth / 2f, hd, 0f, alongX: false);

            // Plain props. The case's objects (desk, brazier, …) are interactables, built in BuildInteractables.
            Transform props = NotWalkableGroup("Props", level.transform);

            // Study (x ∈ [-5, 0], z ∈ [-3, 3]).
            Place("Prop_Chair", props, new Vector3(-4.35f, 0f, 2.05f), yaw: 90f);
            Place("Prop_Bookshelf", props, new Vector3(-3f, 1.3f, NorthInner - 0.375f)); // wall shelf above the desk

            // Kitchen (x ∈ [0, 5], z ∈ [-3, 3]).
            // Keep aisles ≥ 0.5 m (after the agent's 0.5 m clearance) between furniture and walls: the bake drops
            // small walkable pockets that are cut off, so a stool too close to the partition loses the whole corner.
            Place("Prop_KitchenTable", props, new Vector3(3f, 0f, -0.4f)).transform.localScale = new Vector3(0.85f, 1f, 0.85f);
            Place("Prop_Stool", props, new Vector3(1.95f, 0f, -0.4f));
            Place("Prop_Stool", props, new Vector3(4.05f, 0f, -0.4f), yaw: 30f);
            Place("Prop_KitchenShelves", props, new Vector3(1.4f, 0f, NorthInner - 0.5625f)); // wall-mounted
            Place("Prop_Hearth", props, new Vector3(EastInner - 0.2f, 0f, 1.85f), yaw: 90f); // faces the room (−x)
            Place("Prop_Keg", props, new Vector3(3f, 0f, 2.3f), yaw: 90f);
            Place("Prop_Barrel", props, new Vector3(4.3f, 0f, -2.3f));
            Place("Prop_Crates", props, new Vector3(3.3f, 0f, -2.35f));

            BuildFence(NotWalkableGroup("Fence", level.transform));
            return level;
        }

        /// <summary>Wood floor tiles (visual) over one flat collider, which is what clicks and the NavMesh use.</summary>
        private static void BuildFloor(Transform house)
        {
            GameObject floor = Box("Floor", house, Vector3.zero, new Vector3(HouseWidth, 0.04f, HouseDepth), null);
            Object.DestroyImmediate(floor.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(floor.GetComponent<MeshFilter>());
            const float floorTop = 0.02f;

            int nx = Mathf.Max(1, Mathf.RoundToInt(HouseWidth / FloorTileSize));
            int nz = Mathf.Max(1, Mathf.RoundToInt(HouseDepth / FloorTileSize));
            float sx = HouseWidth / nx, sz = HouseDepth / nz;
            var tiles = new GameObject("FloorTiles").transform;
            tiles.SetParent(house, false);
            for (int x = 0; x < nx; x++)
            for (int z = 0; z < nz; z++)
            {
                var pos = new Vector3(-HouseWidth / 2f + sx * (x + 0.5f), 0f, -HouseDepth / 2f + sz * (z + 0.5f));
                GameObject tile = Instantiate(FloorPrefab, tiles, pos);
                tile.transform.localScale = new Vector3(sx / FloorTileSize, 1f, sz / FloorTileSize);
                SitOn(tile, floorTop, top: true);
            }
        }

        /// <summary>
        /// A full-height wall segment from <paramref name="from"/> to <paramref name="to"/> along one axis, at a fixed
        /// coordinate on the other, made of the given KayKit pieces stretched to share the length equally.
        /// </summary>
        private static void Wall(string name, Transform parent, Transform blockerParent, float from, float to, float at,
                                 bool alongX, params string[] pieces)
        {
            Transform segment = Segment(name, parent, from, to, at, alongX);
            float length = to - from, pieceLength = length / pieces.Length;
            for (int i = 0; i < pieces.Length; i++)
            {
                GameObject piece = Instantiate(pieces[i], segment, new Vector3(-length / 2f + pieceLength * (i + 0.5f), 0f, 0f));
                piece.name = $"{name}_{i}_{Path.GetFileNameWithoutExtension(pieces[i])}";
                piece.transform.localScale = new Vector3(pieceLength / WallPieceLength, 1f, WallThickness / WallPieceThickness);
                foreach (Collider c in piece.GetComponentsInChildren<Collider>()) c.enabled = false; // the blocker blocks
            }

            Blocker(name, blockerParent, from, to, at, alongX);
        }

        /// <summary>
        /// A cutaway (low) wall: the generated low KayKit wall, plus an invisible full-height blocker, otherwise the
        /// agent's step height (0.75 m for Humanoid) lets the NavMesh bake straight over it.
        /// </summary>
        private static void LowWall(string name, Transform parent, Transform blockerParent, float from, float to, float at, bool alongX)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt((to - from) / WallPieceLength));
            var pieces = new string[count];
            for (int i = 0; i < count; i++) pieces[i] = LowWallPrefab;
            Wall(name, parent, blockerParent, from, to, at, alongX, pieces);
        }

        /// <summary>Empty parent for one wall segment: centred on it, its local +x running along the wall.</summary>
        private static Transform Segment(string name, Transform parent, float from, float to, float at, bool alongX)
        {
            var segment = new GameObject(name).transform;
            segment.SetParent(parent, false);
            Vector3 centre = WallCenter(from, to, at, alongX, 0f);
            segment.localPosition = new Vector3(centre.x, 0f, centre.z);
            segment.localRotation = Quaternion.Euler(0f, alongX ? 0f : -90f, 0f); // −90: local +x → world +z
            return segment;
        }

        /// <summary>Invisible full-height collider the NavMesh bakes as solid. On Ignore Raycast so clicks go through.</summary>
        private static void Blocker(string name, Transform parent, float from, float to, float at, bool alongX)
        {
            var blocker = new GameObject(name + "_Blocker") { layer = IgnoreRaycastLayer };
            blocker.transform.SetParent(parent, false);
            blocker.transform.localPosition = WallCenter(from, to, at, alongX, FullWallHeight);
            blocker.AddComponent<BoxCollider>().size = WallSize(from, to, alongX, FullWallHeight);
        }

        private static Vector3 WallCenter(float from, float to, float at, bool alongX, float height)
        {
            float mid = (from + to) / 2f;
            return alongX ? new Vector3(mid, height / 2f, at) : new Vector3(at, height / 2f, mid);
        }

        private static Vector3 WallSize(float from, float to, bool alongX, float height) =>
            alongX ? new Vector3(to - from, height, WallThickness) : new Vector3(WallThickness, height, to - from);

        /// <summary>A wooden fence along the yard's south and west sides, with a gate in line with the front door.</summary>
        private static void BuildFence(Transform parent)
        {
            const float south = -11.5f, west = -12f, east = 6f, north = -2f;
            FenceRun("Fence_South", parent, west, east, south, alongX: true, gateNear: FrontDoorX);
            FenceRun("Fence_West", parent, south, north, west, alongX: false, gateNear: float.NaN);
        }

        private static void FenceRun(string name, Transform parent, float from, float to, float at, bool alongX, float gateNear)
        {
            const float pieceLength = 1.848f; // Prop_Fence at its prefab scale
            int count = Mathf.Max(1, Mathf.RoundToInt((to - from) / pieceLength));
            float length = (to - from) / count;
            int gate = float.IsNaN(gateNear) ? -1 : Mathf.Clamp(Mathf.RoundToInt((gateNear - from) / length - 0.5f), 0, count - 1);
            for (int i = 0; i < count; i++)
            {
                float along = from + length * (i + 0.5f);
                Vector3 pos = alongX ? new Vector3(along, 0f, at) : new Vector3(at, 0f, along);
                GameObject piece = Place(i == gate ? "Prop_FenceGate" : "Prop_Fence", parent, pos, yaw: alongX ? 0f : 90f);
                piece.name = $"{name}_{i}";
                piece.transform.localScale = new Vector3(length / pieceLength, 1f, 1f);
            }
        }

        // ---------- Interactables ----------

        /// <summary>
        /// The case's objects and people. Each gets an <see cref="InteractableDefinition"/> asset (created once,
        /// then left alone so edits survive rebuilds) and an InteractionPoint child on open floor.
        /// Solid things sit under Not Walkable groups in Level so the NavMesh bakes round them; the rug is
        /// outside Level so the floor under it stays walkable.
        /// </summary>
        private static void BuildInteractables(Transform level, InteractionSettings settings)
        {
            Transform props = NotWalkableGroup("Interactables", level);

            // Study (x ∈ [-5, 0], z ∈ [-3, 3]). The north wall is full height, so wall-mounted things go there.
            Interactable desk = MakeInteractable("Study_Desk", props, new Vector3(-3f, 0f, 2.05f),
                Definition("Study_Desk", "Desk with ledger", InteractionKind.Inspect,
                    "Aldric's writing desk. A heavy ledger lies open on it, its spine cracked from use."),
                interactionPoint: new Vector3(-3f, 0f, 0.8f));
            GameObject deskModel = Place("Prop_Desk", desk.transform, Vector3.zero);
            deskModel.transform.localScale = new Vector3(0.8f, 1f, 0.8f); // the kit's long table is 2.25 m: too big for the study
            GameObject ledger = Place("Prop_Ledger", desk.transform, new Vector3(0.35f, 0f, -0.05f));
            ledger.transform.localRotation = Quaternion.Euler(0f, 20f, 90f); // lying flat on the desk
            ledger.transform.localScale = Vector3.one * 1.4f;
            SitOn(ledger, PlaceholderArtBuilder.RendererBounds(deskModel).max.y, top: false);

            Interactable clock = MakeInteractable("Study_Clock", props, new Vector3(-4.3f, 0f, NorthInner),
                Definition("Study_Clock", "Wall clock", InteractionKind.Inspect,
                    "A plain wall clock in a brass case. Its hands have stopped."),
                interactionPoint: new Vector3(-4.3f, 0f, 0.9f));
            Place("Prop_WallClock", clock.transform, Vector3.zero); // hangs 1.8 m up, face toward −z

            // The window is the second of four pieces in the north wall: centred at x = −5.2 + 1.5 × 2.6.
            float windowX = -HouseWidth / 2f - WallThickness / 2f + 1.5f * (HouseWidth + WallThickness) / 4f;
            Interactable plants = MakeInteractable("Study_Plants", props, new Vector3(windowX, WindowSillHeight(), HouseDepth / 2f),
                Definition("Study_Plants", "Windowsill plants", InteractionKind.Inspect,
                    "A row of potted herbs on the windowsill."),
                interactionPoint: new Vector3(windowX, 0f, 1.7f));
            Place("Prop_PlantPot_Small", plants.transform, new Vector3(-0.38f, 0f, 0f));
            Place("Prop_PlantPot_Large", plants.transform, Vector3.zero).transform.localScale = Vector3.one * 0.6f;
            Place("Prop_PlantPot_Small", plants.transform, new Vector3(0.38f, 0f, 0f), yaw: 40f);

            var walkableProps = new GameObject("WalkableInteractables").transform; // outside Level: not baked
            Interactable rug = MakeInteractable("Study_Rug", walkableProps, new Vector3(-3f, 0f, -0.9f),
                Definition("Study_Rug", "Rug", InteractionKind.Inspect,
                    "A thick woven rug. It covers most of the study floor."),
                interactionPoint: new Vector3(-1.6f, 0f, -1f));
            Place("Prop_Rug", rug.transform, new Vector3(0f, 0.01f, 0f)).transform.localScale = new Vector3(0.8f, 1f, 0.8f);

            // Yard.
            Interactable brazier = MakeInteractable("Yard_Brazier", props, new Vector3(-7f, 0f, -5f),
                Definition("Yard_Brazier", "Brazier", InteractionKind.Inspect,
                    "An iron brazier, full of grey ash."),
                interactionPoint: new Vector3(-6f, 0f, -5.6f));
            Place("Prop_Brazier", brazier.transform, Vector3.zero);

            // People, facing into the space they stand in.
            Transform npcs = NotWalkableGroup("NPCs", level);
            Npc("NPC_Mira", npcs, new Vector3(3.9f, 0f, 0.6f), yaw: 250f, settings,
                Definition("NPC_Mira", "Mira", InteractionKind.Talk, null),
                interactionPoint: new Vector3(2.7f, 0f, 1f));
            Npc("NPC_Hollen", npcs, new Vector3(-8f, 0f, -7.5f), yaw: 120f, settings, // watching the path from the gate
                Definition("NPC_Hollen", "Hollen", InteractionKind.Talk, null),
                interactionPoint: new Vector3(-6.9f, 0f, -6.9f));
        }

        /// <summary>An NPC from its character prefab. Its capsule collider is what the pointer hits and the NavMesh bakes round.</summary>
        private static void Npc(string name, Transform parent, Vector3 position, float yaw, InteractionSettings settings,
                                InteractableDefinition definition, Vector3 interactionPoint)
        {
            GameObject npc = Instantiate($"{Prefabs}/{name}.prefab", parent, position);
            npc.name = name;
            npc.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            // They stand still in the MVP. An enabled agent would sit in the hole its own collider carves in the NavMesh.
            npc.GetComponent<NavMeshAgent>().enabled = false;
            AddInteractable(npc, definition, interactionPoint);
            SetRef(npc.AddComponent<NpcFacing>(), "_settings", settings); // turns to the player while talking
        }

        /// <summary>Unscaled root with the <see cref="Interactable"/>; models go in children.</summary>
        private static Interactable MakeInteractable(string name, Transform parent, Vector3 position,
                                                     InteractableDefinition definition, Vector3 interactionPoint)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            return AddInteractable(root, definition, interactionPoint);
        }

        private static Interactable AddInteractable(GameObject root, InteractableDefinition definition, Vector3 interactionPoint)
        {
            var interactable = root.AddComponent<Interactable>();
            var point = new GameObject("InteractionPoint").transform;
            point.SetParent(root.transform, false);
            point.position = interactionPoint;

            SetRef(interactable, "_definition", definition);
            SetRef(interactable, "_interactionPoint", point);
            return interactable;
        }

        /// <summary>The definition asset for one object; created with placeholder content only if it doesn't exist yet.</summary>
        private static InteractableDefinition Definition(string file, string displayName, InteractionKind action, string inspectText)
        {
            string path = $"{InteractablesFolder}/{file}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<InteractableDefinition>(path);
            if (existing != null) return existing;

            var created = ScriptableObject.CreateInstance<InteractableDefinition>();
            created.DisplayName = displayName;
            created.Action = action;
            created.InspectText = inspectText;
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        /// <summary>
        /// Height of the window's sill, read from the mesh: drop a ray down the middle of a window piece and take
        /// the lowest upward face above knee height (lower ones are the stones at the wall's foot).
        /// </summary>
        private static float WindowSillHeight()
        {
            var probe = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab(WindowPrefab));
            try
            {
                MeshFilter filter = probe.GetComponentInChildren<MeshFilter>();
                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                Physics.SyncTransforms();

                float sill = float.NaN;
                var ray = new Ray(new Vector3(0f, FullWallHeight + 1f, 0f), Vector3.down);
                for (int i = 0; i < 32 && collider.Raycast(ray, out RaycastHit hit, 10f); i++)
                {
                    if (hit.point.y > 0.4f) sill = hit.point.y;
                    ray.origin = hit.point + Vector3.down * 0.005f;
                }

                if (float.IsNaN(sill)) throw new InvalidOperationException("Couldn't find the window sill in House_WallWindow.");
                return sill;
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }
        }

        /// <summary>Group whose whole hierarchy bakes as Not Walkable, so nothing can be stood on or stepped over.</summary>
        private static Transform NotWalkableGroup(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var modifier = go.AddComponent<NavMeshModifier>();
            modifier.overrideArea = true;
            modifier.area = NotWalkableArea;
            modifier.applyToChildren = true;
            return go.transform;
        }

        // ---------- Prefab placement ----------

        private static GameObject LoadPrefab(string path) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(path)
            ?? throw new InvalidOperationException($"Missing prefab {path}. Run RpgGame → Rebuild Placeholder Art Prefabs.");

        private static GameObject Instantiate(string path, Transform parent, Vector3 localPosition)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab(path), parent);
            go.transform.localPosition = localPosition;
            return go;
        }

        /// <summary>A prop prefab from Prefabs/Props by name.</summary>
        private static GameObject Place(string prop, Transform parent, Vector3 localPosition, float yaw = 0f)
        {
            GameObject go = Instantiate($"{PropPrefabs}/{prop}.prefab", parent, localPosition);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        /// <summary>Moves <paramref name="go"/> vertically so its visible bottom (or top) is at world height <paramref name="y"/>.</summary>
        private static void SitOn(GameObject go, float y, bool top)
        {
            Bounds b = PlaceholderArtBuilder.RendererBounds(go);
            go.transform.position += Vector3.up * (y - (top ? b.max.y : b.min.y));
        }

        /// <summary>The scene's layout assumes these piece sizes; fail loudly if the kit or its import scale changes.</summary>
        private static void CheckPieceSize(string path, float x, float y, float z)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab(path));
            try
            {
                Vector3 size = PlaceholderArtBuilder.RendererBounds(go).size;
                bool ok = Mathf.Abs(size.x - x) < 0.05f && Mathf.Abs(size.z - z) < 0.05f && (float.IsNaN(y) || Mathf.Abs(size.y - y) < 0.05f);
                if (!ok) throw new InvalidOperationException($"{path} is {size}, the layout expects ({x}, {y}, {z}).");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static void BuildRoof(Materials mats)
        {
            // Outside "Level" so it is not baked into the NavMesh.
            var interior = new GameObject("House_Interior");
            interior.layer = IgnoreRaycastLayer;
            var trigger = interior.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1.2f, 0f);
            trigger.size = new Vector3(HouseWidth - WallThickness, 2.4f, HouseDepth - WallThickness);

            GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            Object.DestroyImmediate(roof.GetComponent<Collider>()); // never block click raycasts
            roof.transform.SetParent(interior.transform, false);
            roof.transform.localPosition = new Vector3(0f, FullWallHeight + 0.1f, 0f);
            roof.transform.localScale = new Vector3(HouseWidth + 0.6f, 0.2f, HouseDepth + 0.6f);
            var roofRenderer = roof.GetComponent<Renderer>();
            roofRenderer.sharedMaterial = mats.Roof;

            var hider = interior.AddComponent<RoofHider>();
            var so = new SerializedObject(hider);
            SerializedProperty list = so.FindProperty("_roofRenderers");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = roofRenderer;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Player, marker, camera ----------

        private static ClickMarker BuildClickMarker(Materials mats)
        {
            var root = new GameObject("ClickMarker");
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Visual";
            Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.layer = IgnoreRaycastLayer;
            disc.transform.SetParent(root.transform, false);
            disc.transform.localScale = new Vector3(0.5f, 0.01f, 0.5f);
            var r = disc.GetComponent<Renderer>();
            r.sharedMaterial = mats.Marker;
            r.shadowCastingMode = ShadowCastingMode.Off;

            var marker = root.AddComponent<ClickMarker>();
            SetRef(marker, "_visual", r);
            return marker;
        }

        /// <summary>
        /// The Player prefab (model, NavMeshAgent, run/idle animation, kinematic body, Ignore Raycast layer)
        /// plus the scene-specific movement components.
        /// </summary>
        private static GameObject BuildPlayer(MovementSettings movement, InputActionAsset actions, ClickMarker marker)
        {
            GameObject player = Instantiate($"{Prefabs}/Player.prefab", null, PlayerStart);
            player.name = "Player";

            var mover = player.AddComponent<PlayerMover>();
            SetRef(mover, "_settings", movement);

            var input = player.AddComponent<ClickToMoveInput>();
            SetRef(input, "_actions", actions);
            SetRef(input, "_mover", mover);
            SetRef(input, "_marker", marker);
            return player;
        }

        private static Camera BuildCamera(Transform target, InputActionAsset actions, ClickToMoveInput input)
        {
            Camera main = Camera.main ?? throw new InvalidOperationException("Default scene has no Main Camera.");
            main.orthographic = true;
            main.orthographicSize = StartOrthoSize;
            main.gameObject.AddComponent<CinemachineBrain>();
            SetRef(input, "_camera", main);

            Quaternion rotation = IsometricView.Rotation(CameraPitch, CameraYaw);
            Vector3 offset = IsometricView.FollowOffset(CameraPitch, CameraYaw, CameraDistance);

            var go = new GameObject("IsoCamera");
            go.transform.SetPositionAndRotation(target.position + offset, rotation);

            var cam = go.AddComponent<CinemachineCamera>();
            LensSettings lens = LensSettings.Default;
            lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
            lens.OrthographicSize = StartOrthoSize;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 200f;
            cam.Lens = lens;
            cam.Follow = target;

            // No rotation-control component: the camera keeps its fixed isometric rotation.
            var follow = go.AddComponent<CinemachineFollow>();
            follow.FollowOffset = offset;
            TrackerSettings tracker = TrackerSettings.Default;
            tracker.BindingMode = BindingMode.WorldSpace;
            tracker.PositionDamping = new Vector3(0.6f, 0.6f, 0.6f);
            follow.TrackerSettings = tracker;

            var zoom = go.AddComponent<CameraZoom>();
            SetRef(zoom, "_actions", actions);

            main.transform.SetPositionAndRotation(go.transform.position, rotation);
            return main;
        }

        // ---------- Interaction: player components and UI ----------

        private static void AddPlayerInteraction(GameObject player, InputActionAsset actions, InteractionSettings settings,
                                                 InteractionUi ui, ClickMarker marker, Camera cam)
        {
            var interactor = player.AddComponent<PlayerInteractor>();
            SetRef(interactor, "_actions", actions);
            SetRef(interactor, "_mover", player.GetComponent<PlayerMover>());
            SetRef(interactor, "_settings", settings);
            SetRef(interactor, "_ui", ui);
            SetRef(interactor, "_marker", marker);
            SetRef(player.GetComponent<ClickToMoveInput>(), "_interactor", interactor);

            var hover = player.AddComponent<InteractionHover>();
            SetRef(hover, "_actions", actions);
            SetRef(hover, "_camera", cam);
            SetRef(hover, "_settings", settings);
            SetRef(hover, "_ui", ui);
        }

        /// <summary>Screen-space canvas with the hover label and the inspect/talk popup (placeholder look).</summary>
        private static InteractionUi BuildInteractionUi()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                        ?? throw new InvalidOperationException("Built-in LegacyRuntime font not found.");

            var canvasGo = new GameObject("InteractionUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Hover label: sized to its text, pivot at the bottom so it sits above the object. Never blocks clicks.
            RectTransform label = UiPanel("HoverLabel", canvasGo.transform, new Color(0.08f, 0.08f, 0.1f, 0.85f), raycastTarget: false);
            label.pivot = new Vector2(0.5f, 0f);
            label.anchorMin = label.anchorMax = Vector2.zero;
            var labelLayout = label.gameObject.AddComponent<HorizontalLayoutGroup>();
            labelLayout.padding = new RectOffset(12, 12, 6, 6);
            labelLayout.childControlWidth = labelLayout.childControlHeight = true;
            labelLayout.childForceExpandWidth = labelLayout.childForceExpandHeight = false;
            FitToContent(label.gameObject, horizontal: true);
            Text labelText = UiText("Text", label, font, 22, FontStyle.Normal, new Color(1f, 0.92f, 0.7f));
            labelText.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Popup: bottom centre, fixed width, grows with its text. Blocks clicks so clicking it doesn't close it.
            RectTransform popup = UiPanel("Popup", canvasGo.transform, new Color(0.08f, 0.07f, 0.06f, 0.92f), raycastTarget: true);
            popup.anchorMin = popup.anchorMax = popup.pivot = new Vector2(0.5f, 0f);
            popup.anchoredPosition = new Vector2(0f, 48f);
            popup.sizeDelta = new Vector2(760f, 0f);
            var popupLayout = popup.gameObject.AddComponent<VerticalLayoutGroup>();
            popupLayout.padding = new RectOffset(28, 28, 20, 24);
            popupLayout.spacing = 10f;
            popupLayout.childControlWidth = popupLayout.childControlHeight = true;
            popupLayout.childForceExpandWidth = true;
            popupLayout.childForceExpandHeight = false;
            FitToContent(popup.gameObject, horizontal: false);
            Text title = UiText("Title", popup, font, 28, FontStyle.Bold, new Color(1f, 0.85f, 0.45f));
            Text body = UiText("Body", popup, font, 24, FontStyle.Normal, new Color(0.92f, 0.9f, 0.86f));

            var ui = canvasGo.AddComponent<InteractionUi>();
            SetRef(ui, "_hoverLabel", label);
            SetRef(ui, "_hoverText", labelText);
            SetRef(ui, "_popup", popup.gameObject);
            SetRef(ui, "_popupTitle", title);
            SetRef(ui, "_popupBody", body);
            label.gameObject.SetActive(false);
            popup.gameObject.SetActive(false);
            return ui;
        }

        private static RectTransform UiPanel(string name, Transform parent, Color color, bool raycastTarget)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return (RectTransform)go.transform;
        }

        private static Text UiText(string name, Transform parent, Font font, int size, FontStyle style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void FitToContent(GameObject go, bool horizontal)
        {
            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = horizontal ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        // ---------- NavMesh ----------

        private static void BakeNavMesh(NavMeshSurface surface)
        {
            surface.BuildNavMesh();
            if (surface.navMeshData == null) throw new InvalidOperationException("NavMesh bake produced no data.");

            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshPath) != null) AssetDatabase.DeleteAsset(NavMeshPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
            EditorUtility.SetDirty(surface);
        }

        // ---------- Assets ----------

        private static MovementSettings GetOrCreateMovementSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<MovementSettings>(MovementSettingsPath);
            if (existing != null) return existing; // keep designer-tuned values
            var created = ScriptableObject.CreateInstance<MovementSettings>();
            AssetDatabase.CreateAsset(created, MovementSettingsPath);
            return created;
        }

        private static InteractionSettings GetOrCreateInteractionSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<InteractionSettings>(InteractionSettingsPath);
            if (existing != null) return existing; // keep designer-tuned values
            var created = ScriptableObject.CreateInstance<InteractionSettings>();
            created.TalkPlaceholderFormat = "{0} has nothing to say yet.";
            created.HoverCursor = GetOrCreateHoverCursor();
            created.HoverCursorHotspot = new Vector2(16f, 16f);
            AssetDatabase.CreateAsset(created, InteractionSettingsPath);
            return created;
        }

        /// <summary>Placeholder 32×32 hover cursor: a gold ring with a dot, dark outline. Replace the PNG freely.</summary>
        private static Texture2D GetOrCreateHoverCursor()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(HoverCursorPath);
            if (existing != null) return existing;

            const int size = 32;
            var gold = new Color(1f, 0.85f, 0.3f, 1f);
            var dark = new Color(0.1f, 0.08f, 0.05f, 1f);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var centre = new Vector2(size / 2f, size / 2f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), centre);
                Color c = Color.clear;
                if (d >= 9f && d <= 13.5f) c = d >= 10f && d <= 12.5f ? gold : dark;
                else if (d <= 3f) c = d <= 2f ? gold : dark;
                tex.SetPixel(x, y, c);
            }

            File.WriteAllBytes(Path.Combine(Application.dataPath, "..", HoverCursorPath), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(HoverCursorPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(HoverCursorPath);
            importer.textureType = TextureImporterType.Cursor;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(HoverCursorPath);
        }

        private sealed class Materials
        {
            // Everything else is KayKit art; these have no kit equivalent.
            public readonly Material Grass = Get("Grass", new Color(0.36f, 0.52f, 0.30f));
            public readonly Material Roof = Get("Roof", new Color(0.55f, 0.25f, 0.20f));
            public readonly Material Marker = Get("Marker", new Color(1.00f, 0.85f, 0.20f));

            private static Material Get(string name, Color color)
            {
                string path = $"{MaterialsFolder}/M_{name}.mat";
                var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (existing != null) return existing;

                Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                                ?? throw new InvalidOperationException("URP Lit shader not found.");
                var mat = new Material(shader) { name = $"M_{name}" };
                mat.SetColor("_BaseColor", color);
                AssetDatabase.CreateAsset(mat, path);
                return mat;
            }
        }

        // ---------- Helpers ----------

        private static void SetRef(Object component, string property, Object value)
        {
            if (value == null) // also catches destroyed ("fake null") Unity objects
                throw new InvalidOperationException($"Refusing to assign a missing object to {component.GetType().Name}.{property}.");
            var so = new SerializedObject(component);
            SerializedProperty p = so.FindProperty(property)
                                   ?? throw new InvalidOperationException($"{component.GetType().Name} has no '{property}'.");
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
