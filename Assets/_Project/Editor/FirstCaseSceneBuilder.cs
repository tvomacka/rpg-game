using System;
using RpgGame.CameraControl;
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

        private const int IgnoreRaycastLayer = 2;

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
            var mats = new Materials();
            MovementSettings movement = GetOrCreateMovementSettings();
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath)
                          ?? throw new InvalidOperationException($"Missing input actions at {InputActionsPath}");

            GameObject level = BuildLevel(mats);
            BuildRoof(mats);
            ClickMarker marker = BuildClickMarker(mats);
            GameObject player = BuildPlayer(mats, movement, actions, marker);
            SetRef(marker, "_mover", player.GetComponent<PlayerMover>());
            BuildCamera(player.transform, actions, player.GetComponent<ClickToMoveInput>());
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

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
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;

            // Big enough that the ground's edge stays off-screen even at maximum zoom-out.
            Box("Yard", level.transform, new Vector3(0f, -0.1f, 0f), new Vector3(80f, 0.2f, 80f), mats.Grass);

            var house = new GameObject("House").transform;
            house.SetParent(level.transform, false);
            Box("Floor", house, new Vector3(0f, 0f, 0f), new Vector3(HouseWidth, 0.04f, HouseDepth), mats.Floor);

            float hw = HouseWidth / 2f, hd = HouseDepth / 2f, t = WallThickness;

            // Back walls (camera looks at their inner face): full height.
            Wall("Wall_North", house, -hw - t / 2f, hw + t / 2f, hd, alongX: true, FullWallHeight, mats.Wall);
            Wall("Wall_East", house, -hd, hd, hw, alongX: false, FullWallHeight, mats.Wall);

            // Camera-facing walls: low cutaways so the interior stays visible.
            float doorL = FrontDoorX - DoorWidth / 2f, doorR = FrontDoorX + DoorWidth / 2f;
            Wall("Wall_South_A", house, -hw - t / 2f, doorL, -hd, alongX: true, CutawayWallHeight, mats.WallCutaway);
            Wall("Wall_South_B", house, doorR, hw + t / 2f, -hd, alongX: true, CutawayWallHeight, mats.WallCutaway);
            Wall("Wall_West", house, -hd, hd, -hw, alongX: false, CutawayWallHeight, mats.WallCutaway);

            // Study | kitchen partition with a doorway. Its study-side face points at the camera, so it is low too.
            Wall("Partition_A", house, -hd, -DoorWidth / 2f, 0f, alongX: false, CutawayWallHeight, mats.WallCutaway);
            Wall("Partition_B", house, DoorWidth / 2f, hd, 0f, alongX: false, CutawayWallHeight, mats.WallCutaway);

            var props = new GameObject("Props").transform;
            props.SetParent(level.transform, false);
            Box("Study_Desk", props, new Vector3(-3f, 0.4f, 1.9f), new Vector3(1.8f, 0.8f, 0.8f), mats.Furniture);
            Box("Kitchen_Table", props, new Vector3(2.6f, 0.4f, -0.6f), new Vector3(1.6f, 0.8f, 1.0f), mats.Furniture);
            Box("Kitchen_Stove", props, new Vector3(4.1f, 0.6f, 2.3f), new Vector3(1.2f, 1.2f, 0.8f), mats.Furniture);
            GameObject brazier = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            brazier.name = "Yard_Brazier";
            brazier.transform.SetParent(props, false);
            brazier.transform.localPosition = new Vector3(-7f, 0.4f, -5f);
            brazier.transform.localScale = new Vector3(0.8f, 0.4f, 0.8f);
            brazier.GetComponent<Renderer>().sharedMaterial = mats.Furniture;

            return level;
        }

        /// <summary>Wall segment from <paramref name="from"/> to <paramref name="to"/> along one axis, at a fixed coordinate on the other.</summary>
        private static void Wall(string name, Transform parent, float from, float to, float at, bool alongX, float height, Material mat)
        {
            float length = to - from, mid = (from + to) / 2f;
            Vector3 pos = alongX ? new Vector3(mid, height / 2f, at) : new Vector3(at, height / 2f, mid);
            Vector3 size = alongX
                ? new Vector3(length, height, WallThickness)
                : new Vector3(WallThickness, height, length);
            Box(name, parent, pos, size, mat);
        }

        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
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

        private static void BuildCamera(Transform target, InputActionAsset actions, ClickToMoveInput input)
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
