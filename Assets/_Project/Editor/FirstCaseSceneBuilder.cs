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
    /// Generates the grey-box FirstCase scene: yard, two-room house with cutaway walls, roof that hides
    /// when the player is inside, player capsule, click marker, isometric Cinemachine camera and baked NavMesh.
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
        private const float WallThickness = 0.2f;
        private const float FullWallHeight = 2.5f;
        private const float CutawayWallHeight = 0.6f;
        private const float DoorWidth = 1.6f;
        private const float FrontDoorX = -2.5f; // front door in the study's south wall

        private const float CameraPitch = IsometricView.DefaultPitch;
        private const float CameraYaw = IsometricView.DefaultYaw;
        private const float CameraDistance = 40f;
        private const float StartOrthoSize = 7f;

        private static readonly Vector3 PlayerStart = new(FrontDoorX, 0f, -7f);

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
            var mats = new Materials();
            MovementSettings movement = GetOrCreateMovementSettings();
            InteractionSettings interaction = GetOrCreateInteractionSettings();
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath)
                          ?? throw new InvalidOperationException($"Missing input actions at {InputActionsPath}");

            GameObject level = BuildLevel(mats);
            BuildInteractables(level.transform, mats, interaction);
            BuildRoof(mats);
            ClickMarker marker = BuildClickMarker(mats);
            GameObject player = BuildPlayer(mats, movement, actions, marker);
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
            Box("Floor", house, new Vector3(0f, 0f, 0f), new Vector3(HouseWidth, 0.04f, HouseDepth), mats.Floor);
            Transform walls = NotWalkableGroup("Walls", house);
            Transform blockers = NotWalkableGroup("WallBlockers", house);

            float hw = HouseWidth / 2f, hd = HouseDepth / 2f, t = WallThickness;

            // Back walls (camera looks at their inner face): full height.
            Wall("Wall_North", walls, blockers, -hw - t / 2f, hw + t / 2f, hd, alongX: true, FullWallHeight, mats.Wall);
            Wall("Wall_East", walls, blockers, -hd, hd, hw, alongX: false, FullWallHeight, mats.Wall);

            // Camera-facing walls: low cutaways so the interior stays visible.
            float doorL = FrontDoorX - DoorWidth / 2f, doorR = FrontDoorX + DoorWidth / 2f;
            Wall("Wall_South_A", walls, blockers, -hw - t / 2f, doorL, -hd, alongX: true, CutawayWallHeight, mats.WallCutaway);
            Wall("Wall_South_B", walls, blockers, doorR, hw + t / 2f, -hd, alongX: true, CutawayWallHeight, mats.WallCutaway);
            Wall("Wall_West", walls, blockers, -hd, hd, -hw, alongX: false, CutawayWallHeight, mats.WallCutaway);

            // Study | kitchen partition with a doorway. Its study-side face points at the camera, so it is low too.
            Wall("Partition_A", walls, blockers, -hd, -DoorWidth / 2f, 0f, alongX: false, CutawayWallHeight, mats.WallCutaway);
            Wall("Partition_B", walls, blockers, DoorWidth / 2f, hd, 0f, alongX: false, CutawayWallHeight, mats.WallCutaway);

            // Plain props. The case's objects (desk, brazier, …) are interactables, built in BuildInteractables.
            Transform props = NotWalkableGroup("Props", level.transform);
            Box("Kitchen_Table", props, new Vector3(2.6f, 0.4f, -0.6f), new Vector3(1.6f, 0.8f, 1.0f), mats.Furniture);
            Box("Kitchen_Stove", props, new Vector3(4.1f, 0.6f, 2.3f), new Vector3(1.2f, 1.2f, 0.8f), mats.Furniture);

            return level;
        }

        // ---------- Interactables ----------

        /// <summary>
        /// The case's objects and people. Each gets an <see cref="InteractableDefinition"/> asset (created once,
        /// then left alone so edits survive rebuilds) and an InteractionPoint child on open floor.
        /// Solid things sit under Not Walkable groups in Level so the NavMesh bakes round them; the rug is
        /// outside Level so the floor under it stays walkable.
        /// </summary>
        private static void BuildInteractables(Transform level, Materials mats, InteractionSettings settings)
        {
            Transform props = NotWalkableGroup("Interactables", level);

            // Study (x ∈ [-5, 0], z ∈ [-3, 3]). The north wall is full height, so wall-mounted things go there.
            Interactable desk = MakeInteractable("Study_Desk", props, new Vector3(-3f, 0f, 1.9f),
                Definition("Study_Desk", "Desk with ledger", InteractionKind.Inspect,
                    "Aldric's writing desk. A heavy ledger lies open on it, its spine cracked from use."),
                interactionPoint: new Vector3(-3f, 0f, 0.8f));
            Box("Desk", desk.transform, new Vector3(0f, 0.4f, 0f), new Vector3(1.8f, 0.8f, 0.8f), mats.Furniture);
            Box("Ledger", desk.transform, new Vector3(0.3f, 0.83f, 0f), new Vector3(0.5f, 0.06f, 0.35f), mats.Paper);

            Interactable clock = MakeInteractable("Study_Clock", props, new Vector3(-4.3f, 1.8f, 2.86f),
                Definition("Study_Clock", "Wall clock", InteractionKind.Inspect,
                    "A plain wall clock in a brass case. Its hands have stopped."),
                interactionPoint: new Vector3(-4.3f, 0f, 0.9f));
            Box("Clock", clock.transform, Vector3.zero, new Vector3(0.5f, 0.5f, 0.06f), mats.Clock);

            Interactable plants = MakeInteractable("Study_Plants", props, new Vector3(-1f, 0f, 2.75f),
                Definition("Study_Plants", "Windowsill plants", InteractionKind.Inspect,
                    "A row of potted herbs on the windowsill."),
                interactionPoint: new Vector3(-1f, 0f, 1.7f));
            Box("Sill", plants.transform, new Vector3(0f, 0.9f, 0f), new Vector3(1f, 0.08f, 0.3f), mats.Furniture);
            for (int i = -1; i <= 1; i++)
            {
                Box($"Pot_{i + 1}", plants.transform, new Vector3(0.3f * i, 1.02f, 0f), new Vector3(0.16f, 0.16f, 0.16f), mats.Furniture);
                Box($"Leaves_{i + 1}", plants.transform, new Vector3(0.3f * i, 1.2f, 0f), new Vector3(0.22f, 0.2f, 0.22f), mats.Plant);
            }

            var walkableProps = new GameObject("WalkableInteractables").transform; // outside Level: not baked
            Interactable rug = MakeInteractable("Study_Rug", walkableProps, new Vector3(-3f, 0f, -1f),
                Definition("Study_Rug", "Rug", InteractionKind.Inspect,
                    "A thick woven rug. It covers most of the study floor."),
                interactionPoint: new Vector3(-1.6f, 0f, -1f));
            Box("Rug", rug.transform, new Vector3(0f, 0.03f, 0f), new Vector3(1.8f, 0.02f, 1.2f), mats.Rug);

            // Yard.
            Interactable brazier = MakeInteractable("Yard_Brazier", props, new Vector3(-7f, 0f, -5f),
                Definition("Yard_Brazier", "Brazier", InteractionKind.Inspect,
                    "An iron brazier, full of grey ash."),
                interactionPoint: new Vector3(-6f, 0f, -5.6f));
            Primitive(PrimitiveType.Cylinder, "Bowl", brazier.transform, new Vector3(0f, 0.4f, 0f), new Vector3(0.8f, 0.4f, 0.8f), mats.Clock);

            // People: still capsules in their own colour, facing into the space they stand in.
            Transform npcs = NotWalkableGroup("NPCs", level);
            Npc("NPC_Mira", npcs, new Vector3(3.9f, 0f, 0.6f), yaw: 250f, mats, settings,
                Definition("NPC_Mira", "Mira", InteractionKind.Talk, null),
                interactionPoint: new Vector3(2.7f, 0f, 1f));
            Npc("NPC_Hollen", npcs, new Vector3(-8f, 0f, -7.5f), yaw: 45f, mats, settings,
                Definition("NPC_Hollen", "Hollen", InteractionKind.Talk, null),
                interactionPoint: new Vector3(-6.9f, 0f, -6.9f));
        }

        private static void Npc(string name, Transform parent, Vector3 position, float yaw, Materials mats, InteractionSettings settings,
                                InteractableDefinition definition, Vector3 interactionPoint)
        {
            Interactable npc = MakeInteractable(name, parent, position, definition, interactionPoint);
            npc.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            SetRef(npc.gameObject.AddComponent<NpcFacing>(), "_settings", settings); // turns to the player while talking
            Primitive(PrimitiveType.Capsule, "Body", npc.transform, new Vector3(0f, 1f, 0f), new Vector3(0.8f, 1f, 0.8f), mats.Npc);
            GameObject nose = Primitive(PrimitiveType.Cube, "FacingIndicator", npc.transform, new Vector3(0f, 1.5f, 0.35f),
                new Vector3(0.25f, 0.15f, 0.25f), mats.Marker);
            Object.DestroyImmediate(nose.GetComponent<Collider>());
        }

        /// <summary>Unscaled root with the <see cref="Interactable"/>; geometry goes in children.</summary>
        private static Interactable MakeInteractable(string name, Transform parent, Vector3 position,
                                                     InteractableDefinition definition, Vector3 interactionPoint)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
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

        /// <summary>Wall segment from <paramref name="from"/> to <paramref name="to"/> along one axis, at a fixed coordinate on the other.</summary>
        /// <remarks>
        /// Wall height is visual only. A cutaway (low) wall also gets an invisible full-height blocker,
        /// otherwise the agent's step height (0.75 m for Humanoid) lets the NavMesh bake straight over it.
        /// Blockers sit on Ignore Raycast so clicks go through them to the floor.
        /// </remarks>
        private static void Wall(string name, Transform parent, Transform blockerParent, float from, float to, float at,
                                 bool alongX, float height, Material mat)
        {
            Box(name, parent, WallCenter(from, to, at, alongX, height), WallSize(from, to, alongX, height), mat);
            if (height >= FullWallHeight) return;

            var blocker = new GameObject(name + "_Blocker") { layer = IgnoreRaycastLayer };
            blocker.transform.SetParent(blockerParent, false);
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

        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material mat) =>
            Primitive(PrimitiveType.Cube, name, parent, position, size, mat);

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
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

        private static GameObject BuildPlayer(Materials mats, MovementSettings movement, InputActionAsset actions, ClickMarker marker)
        {
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.layer = IgnoreRaycastLayer; // clicks pass through the player to the ground
            player.transform.position = PlayerStart + Vector3.up; // capsule pivot is its centre
            player.GetComponent<Renderer>().sharedMaterial = mats.Player;

            GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "FacingIndicator";
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            nose.layer = IgnoreRaycastLayer;
            nose.transform.SetParent(player.transform, false);
            nose.transform.localPosition = new Vector3(0f, 0.5f, 0.45f);
            nose.transform.localScale = new Vector3(0.3f, 0.2f, 0.3f);
            nose.GetComponent<Renderer>().sharedMaterial = mats.Marker;

            var body = player.AddComponent<Rigidbody>();
            body.isKinematic = true; // needed for trigger events; the NavMeshAgent drives the transform

            var agent = player.AddComponent<NavMeshAgent>();
            agent.baseOffset = 1f;
            agent.height = 2f;
            agent.radius = 0.4f;

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
            public readonly Material Grass = Get("Grass", new Color(0.36f, 0.52f, 0.30f));
            public readonly Material Floor = Get("Floor", new Color(0.55f, 0.45f, 0.35f));
            public readonly Material Wall = Get("Wall", new Color(0.80f, 0.78f, 0.72f));
            public readonly Material WallCutaway = Get("WallCutaway", new Color(0.62f, 0.60f, 0.56f));
            public readonly Material Roof = Get("Roof", new Color(0.55f, 0.25f, 0.20f));
            public readonly Material Furniture = Get("Furniture", new Color(0.40f, 0.30f, 0.22f));
            public readonly Material Player = Get("Player", new Color(0.20f, 0.45f, 0.85f));
            public readonly Material Marker = Get("Marker", new Color(1.00f, 0.85f, 0.20f));
            public readonly Material Npc = Get("Npc", new Color(0.85f, 0.45f, 0.20f));
            public readonly Material Plant = Get("Plant", new Color(0.45f, 0.42f, 0.22f));
            public readonly Material Clock = Get("Clock", new Color(0.30f, 0.26f, 0.22f));
            public readonly Material Rug = Get("Rug", new Color(0.50f, 0.16f, 0.16f));
            public readonly Material Paper = Get("Paper", new Color(0.90f, 0.87f, 0.78f));

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
