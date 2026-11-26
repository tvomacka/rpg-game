using System.Collections.Generic;
using System.Linq;
using RpgGame.Characters;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace RpgGame.Editor
{
    /// <summary>
    /// Builds the KayKit placeholder-art prefabs (issue #11): configures the model importers, makes one URP Lit
    /// material per texture atlas, an Idle/Walk Animator Controller per character, and the prefabs in
    /// Assets/_Project/Prefabs. Safe to re-run: existing assets are updated in place, so GUIDs stay stable.
    /// Menu: RpgGame → Rebuild Placeholder Art Prefabs. Batch: -executeMethod RpgGame.Editor.PlaceholderArtBuilder.BuildFromCommandLine
    /// </summary>
    public static class PlaceholderArtBuilder
    {
        public const string KayKitRoot = "Assets/ThirdParty/KayKit";
        public const string PrefabRoot = "Assets/_Project/Prefabs";
        public const string PropFolder = PrefabRoot + "/Props";
        private const string AnimatorFolder = "Assets/_Project/Art/Characters";

        /// <summary>KayKit's kits share one scale where a character is ~2.4 units tall; this makes it ~1.8 m.</summary>
        public const float KayKitScale = 0.75f;

        /// <summary>The Medieval Hexagon kit is miniature (hex-tile scale); its fences need this to reach ~1 m.</summary>
        private const float HexagonKitScale = 1.6f;

        public const string IdleClip = "Idle";
        public const string WalkClip = "Walking_A";

        private const string Adventurers = KayKitRoot + "/CharacterPackAdventurers";
        private const string Furniture = KayKitRoot + "/FurnitureBits";
        private const string Dungeon = KayKitRoot + "/DungeonRemastered";
        private const string Hexagon = KayKitRoot + "/MedievalHexagon";
        private const string Restaurant = KayKitRoot + "/RestaurantBits";

        private const int IgnoreRaycastLayer = 2;

        // ---------- What gets built ----------

        private sealed class CharacterSpec
        {
            public string Prefab, Model, Atlas;
            public string[] Hide;
            public bool IsPlayer;
        }

        private static readonly string[] RogueGear = { "Knife_Offhand", "1H_Crossbow", "2H_Crossbow", "Knife", "Throwable" };

        private static readonly CharacterSpec[] Characters =
        {
            new() { Prefab = "Player", Model = "RogueHooded", Atlas = "rogue_texture", Hide = RogueGear, IsPlayer = true },
            new() { Prefab = "NPC_Mira", Model = "Rogue", Atlas = "rogue_texture", Hide = RogueGear },
            new()
            {
                Prefab = "NPC_Hollen", Model = "Barbarian", Atlas = "barbarian_texture",
                // A merchant, not a warrior: no weapons, no mug, and no bear-head hat.
                Hide = new[] { "1H_Axe_Offhand", "Barbarian_Round_Shield", "1H_Axe", "2H_Axe", "Mug", "Barbarian_Hat" },
            },
        };

        private readonly struct Part
        {
            public readonly string Model;
            public readonly Vector3 Position, Euler;
            public readonly float Scale;

            public Part(string model, float scale = KayKitScale, Vector3 position = default, Vector3 euler = default)
            {
                Model = model;
                Scale = scale;
                Position = position;
                Euler = euler;
            }
        }

        private sealed class PropSpec
        {
            public string Name;
            public Part[] Parts;
            public bool Collider = true;
            /// <summary>Shift the parts so their bounds are centred on the pivot in X/Z (bottom stays at y = 0).</summary>
            public bool Recenter;
        }

        public static IReadOnlyList<string> CharacterPrefabNames => Characters.Select(c => c.Prefab).ToArray();
        public static IReadOnlyList<string> PropPrefabNames => Props.Select(p => p.Name).ToArray();

        private static PropSpec Single(string name, string model, float scale = KayKitScale, bool collider = true) =>
            new() { Name = name, Parts = new[] { new Part(model, scale) }, Collider = collider };

        private static readonly PropSpec[] Props =
        {
            // Study
            Single("Prop_Desk", Furniture + "/Models/table_medium_long.fbx"),
            Single("Prop_Chair", Furniture + "/Models/chair_A_wood.fbx"),
            Single("Prop_Rug", Furniture + "/Models/rug_rectangle_stripes_A.fbx"),
            Single("Prop_PlantPot_Small", Furniture + "/Models/cactus_small_A.fbx"),
            Single("Prop_PlantPot_Large", Furniture + "/Models/cactus_medium_A.fbx"),
            Single("Prop_Bookshelf", Furniture + "/Models/shelf_B_large_decorated.fbx"),
            Single("Prop_Ledger", Furniture + "/Models/book_single.fbx"),
            new()
            {
                // No clock in any KayKit pack: a pewter plate hung on the wall reads as a clock face from
                // the isometric camera. Pivot at floor level, face toward -Z; place it against a wall.
                Name = "Prop_WallClock",
                Parts = new[] { new Part(Dungeon + "/Models/plate.fbx", 0.6f, new Vector3(0f, 1.8f, 0f), new Vector3(-90f, 0f, 0f)) },
            },

            // Kitchen
            Single("Prop_KitchenTable", Dungeon + "/Models/table_medium.fbx"),
            Single("Prop_Stool", Dungeon + "/Models/stool.fbx"),
            Single("Prop_KitchenShelves", Dungeon + "/Models/shelves.fbx"),
            Single("Prop_Barrel", Dungeon + "/Models/barrel_large.fbx", 0.5f),
            Single("Prop_Keg", Dungeon + "/Models/keg.fbx", 0.5f),
            Single("Prop_Crates", Dungeon + "/Models/crates_stacked.fbx", 0.5f),
            new()
            {
                // No hearth or (non-modern) stove in KayKit: a stone arch wall piece as the chimney breast,
                // with a stew pot over a fire in front of it.
                Name = "Prop_Hearth",
                Parts = new[]
                {
                    new Part(Dungeon + "/Models/wall_arched.fbx", 0.4f),
                    new Part(Dungeon + "/Models/torch_lit.fbx", 0.6f, new Vector3(0f, 0.05f, -0.45f)),
                    new Part(Restaurant + "/Models/pot_A_stew.fbx", 0.4f, new Vector3(0f, 0.3f, -0.45f)),
                },
            },

            // Yard
            new()
            {
                // No brazier in KayKit: an iron pot on a stool with a torch's flames inside.
                Name = "Prop_Brazier",
                Parts = new[]
                {
                    new Part(Dungeon + "/Models/stool.fbx", 0.9f),
                    new Part(Restaurant + "/Models/pot_large.fbx", 0.35f, new Vector3(0f, 0.45f, 0f)),
                    new Part(Dungeon + "/Models/torch_lit.fbx", 0.7f, new Vector3(0f, 0.5f, 0f)),
                },
            },
            // Hexagon fences are modelled off-centre along Z; turned to run along X and recentred on the pivot.
            new() { Name = "Prop_Fence", Recenter = true, Parts = new[] { new Part(Hexagon + "/Models/fence_wood_straight.fbx", HexagonKitScale, euler: new Vector3(0f, 90f, 0f)) } },
            new() { Name = "Prop_FenceGate", Recenter = true, Parts = new[] { new Part(Hexagon + "/Models/fence_wood_straight_gate.fbx", HexagonKitScale, euler: new Vector3(0f, 90f, 0f)) } },

            // House pieces (3 m wide, 3 m tall at KayKitScale)
            Single("House_Wall", Dungeon + "/Models/wall.fbx"),
            Single("House_WallDoorway", Dungeon + "/Models/wall_doorway.fbx"),
            Single("House_WallWindow", Dungeon + "/Models/wall_window_open.fbx"),
            Single("House_Floor", Dungeon + "/Models/floor_wood_large.fbx", KayKitScale, collider: false),
        };

        /// <summary>Texture atlas → generated material. One per atlas: every KayKit model in a pack shares it.</summary>
        private static readonly (string Folder, string Atlas)[] Atlases =
        {
            (Adventurers, "rogue_texture"),
            (Adventurers, "barbarian_texture"),
            (Furniture, "furniturebits_texture"),
            (Dungeon, "dungeon_texture"),
            (Hexagon, "hexagons_medieval"),
            (Restaurant, "restaurantbits_texture"),
        };

        // ---------- Entry points ----------

        [MenuItem("RpgGame/Rebuild Placeholder Art Prefabs")]
        public static void Rebuild() => Build();

        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        private static void Build()
        {
            EnsureFolder(PropFolder);
            EnsureFolder(AnimatorFolder);

            var materials = Atlases.ToDictionary(a => a.Atlas, a => GetOrCreateAtlasMaterial(a.Folder, a.Atlas));

            foreach (string folder in new[] { Furniture, Dungeon, Hexagon, Restaurant })
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder + "/Models" }))
                ConfigureProp(AssetDatabase.GUIDToAssetPath(guid), materials[AtlasOf(folder)]);

            foreach (CharacterSpec c in Characters)
            {
                string modelPath = $"{Adventurers}/Models/{c.Model}.fbx";
                ConfigureCharacter(modelPath, materials[c.Atlas]);
                AnimatorController controller = BuildController(c, modelPath);
                BuildCharacterPrefab(c, modelPath, controller);
            }

            foreach (PropSpec p in Props) BuildPropPrefab(p);

            AssetDatabase.SaveAssets();
            Debug.Log($"[PlaceholderArtBuilder] Built {Characters.Length} character and {Props.Length} prop prefabs.");
        }

        private static string AtlasOf(string folder) => Atlases.First(a => a.Folder == folder).Atlas;

        // ---------- Importers and materials ----------

        private static Material GetOrCreateAtlasMaterial(string packFolder, string atlas)
        {
            string folder = packFolder + "/Materials";
            EnsureFolder(folder);
            string path = $"{folder}/M_KayKit_{atlas}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{packFolder}/Textures/{atlas}.png");
            if (tex == null) throw new System.InvalidOperationException($"Missing atlas texture {packFolder}/Textures/{atlas}.png");
            mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.15f);
            mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Points every material slot in the model at the pack's atlas material.</summary>
        private static void RemapMaterials(ModelImporter importer, string path, Material target)
        {
            var names = new HashSet<string>(AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().Select(m => m.name));
            foreach (var key in importer.GetExternalObjectMap().Keys.Where(k => k.type == typeof(Material)))
                names.Add(key.name);
            foreach (string name in names)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), target);
        }

        private static void ConfigureProp(string path, Material atlas)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            RemapMaterials(importer, path, atlas);
            importer.SaveAndReimport();
        }

        private static void ConfigureCharacter(string path, Material atlas)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.importCameras = false;
            importer.importLights = false;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            RemapMaterials(importer, path, atlas);

            // The FBX carries 75 clips; import only the two the MVP uses.
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations
                .Where(c => c.name == IdleClip || c.name == WalkClip)
                .ToArray();
            if (clips.Length != 2)
                throw new System.InvalidOperationException($"{path}: expected clips '{IdleClip}' and '{WalkClip}'.");
            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.loopTime = true;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        // ---------- Animator ----------

        private static AnimatorController BuildController(CharacterSpec c, string modelPath)
        {
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>()
                .Where(a => !a.name.StartsWith("__preview")).ToArray();
            AnimationClip idle = clips.Single(a => a.name == IdleClip);
            AnimationClip walk = clips.Single(a => a.name == WalkClip);

            string path = $"{AnimatorFolder}/AC_{c.Prefab}.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path)
                             ?? AnimatorController.CreateAnimatorControllerAtPath(path);

            foreach (AnimatorControllerParameter p in controller.parameters) controller.RemoveParameter(p);
            controller.AddParameter(LocomotionAnimation.SpeedParameter, AnimatorControllerParameterType.Float);
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = LocomotionAnimation.WalkRateParameter,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1f,
            });

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState s in sm.states) sm.RemoveState(s.state);

            AnimatorState idleState = sm.AddState(LocomotionAnimation.IdleState, new Vector3(300f, 0f));
            idleState.motion = idle;
            AnimatorState walkState = sm.AddState(LocomotionAnimation.WalkState, new Vector3(300f, 120f));
            walkState.motion = walk;
            walkState.speedParameterActive = true;
            walkState.speedParameter = LocomotionAnimation.WalkRateParameter;
            sm.defaultState = idleState;

            AddTransition(idleState, walkState, AnimatorConditionMode.Greater);
            AddTransition(walkState, idleState, AnimatorConditionMode.Less);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddTransition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0.15f;
            t.AddCondition(mode, LocomotionAnimation.WalkThreshold, LocomotionAnimation.SpeedParameter);
        }

        // ---------- Prefabs ----------

        private static void BuildCharacterPrefab(CharacterSpec c, string modelPath, AnimatorController controller)
        {
            var root = new GameObject(c.Prefab);
            try
            {
                var agent = root.AddComponent<NavMeshAgent>();
                agent.height = 1.8f;
                agent.radius = 0.4f;

                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
                model.name = "Model";
                model.transform.SetParent(root.transform, false);
                model.transform.localScale = Vector3.one * KayKitScale;
                foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                    if (c.Hide.Contains(t.name)) t.gameObject.SetActive(false);

                var animator = model.GetComponent<Animator>();
                if (animator == null) animator = model.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;

                var driver = root.AddComponent<AgentLocomotionAnimator>();
                var so = new SerializedObject(driver);
                so.FindProperty("_animator").objectReferenceValue = animator;
                so.ApplyModifiedPropertiesWithoutUndo();

                if (c.IsPlayer)
                {
                    // Matches the grey-box player: clicks pass through to the ground, and a kinematic body
                    // so the roof's trigger volume sees the player.
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = IgnoreRaycastLayer;
                    root.AddComponent<Rigidbody>().isKinematic = true;
                }

                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.center = new Vector3(0f, 0.9f, 0f);
                capsule.height = 1.8f;
                capsule.radius = 0.35f;

                PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabRoot}/{c.Prefab}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildPropPrefab(PropSpec p)
        {
            var root = new GameObject(p.Name);
            try
            {
                foreach (Part part in p.Parts)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(part.Model);
                    if (asset == null) throw new System.InvalidOperationException($"{p.Name}: missing model {part.Model}");
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                    model.transform.SetParent(root.transform, false);
                    model.transform.localPosition = part.Position;
                    model.transform.localEulerAngles = part.Euler;
                    model.transform.localScale = Vector3.one * part.Scale;
                }

                if (p.Recenter)
                {
                    Bounds parts = RendererBounds(root);
                    var shift = new Vector3(-parts.center.x, 0f, -parts.center.z);
                    foreach (Transform child in root.transform) child.localPosition += shift;
                }

                if (p.Collider)
                {
                    Bounds b = RendererBounds(root);
                    var box = root.AddComponent<BoxCollider>();
                    box.center = b.center;
                    box.size = b.size;
                }

                PrefabUtility.SaveAsPrefabAsset(root, $"{PropFolder}/{p.Name}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>World-space bounds of all renderers; the root sits at the origin unrotated, so that is local too.</summary>
        public static Bounds RendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.zero);
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers.Skip(1)) b.Encapsulate(r.bounds);
            return b;
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
