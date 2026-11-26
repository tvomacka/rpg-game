using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RpgGame.Editor
{
    /// <summary>
    /// Renders the placeholder-art prefabs side by side to Docs/screenshots/011-KayKit-lineup.png.
    /// Builds a temporary additive scene, renders one frame and closes it; nothing is saved to the project.
    /// Needs a graphics device, so in batch mode run it without -nographics:
    ///   Unity.exe -batchmode -projectPath . -executeMethod RpgGame.Editor.PlaceholderArtLineup.RenderFromCommandLine -logFile TestResults\lineup.log
    /// </summary>
    public static class PlaceholderArtLineup
    {
        public const string OutputPath = "Docs/screenshots/011-KayKit-lineup.png";
        private const int Width = 1920;
        private const int Height = 1080;
        private const float Gap = 0.5f;
        private const int WarmUpFrames = 3;

        private static readonly string[][] Rows =
        {
            new[] { "Player", "NPC_Mira", "NPC_Hollen" },
            new[] { "Props/Prop_Desk", "Props/Prop_Chair", "Props/Prop_Ledger", "Props/Prop_Rug", "Props/Prop_PlantPot_Small", "Props/Prop_PlantPot_Large", "Props/Prop_Bookshelf", "Props/Prop_WallClock" },
            new[] { "Props/Prop_KitchenTable", "Props/Prop_Stool", "Props/Prop_KitchenShelves", "Props/Prop_Barrel", "Props/Prop_Keg", "Props/Prop_Crates", "Props/Prop_Hearth" },
            new[] { "Props/Prop_Brazier", "Props/Prop_Fence", "Props/Prop_FenceGate", "Props/House_Wall", "Props/House_WallDoorway", "Props/House_WallWindow", "Props/House_WallLow", "Props/House_Floor" },
        };

        [MenuItem("RpgGame/Render Placeholder Art Lineup")]
        public static void Render() => RenderTo(OutputPath);

        public static void RenderFromCommandLine()
        {
            try
            {
                RenderTo(OutputPath);
                EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        private static void RenderTo(string output)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new System.InvalidOperationException("No graphics device (-nographics); cannot render the lineup.");

            // An untitled scene can't sit next to an additive one, so then the lineup replaces it
            // (after offering to save, when a person is at the editor).
            Scene previousActive = SceneManager.GetActiveScene();
            bool replace = string.IsNullOrEmpty(previousActive.path);
            if (replace && !Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, replace ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var temporary = new System.Collections.Generic.List<Object>();
            try
            {
                Bounds all = Layout(temporary);
                Camera cam = SetUpStage(all, temporary);

                var rt = new RenderTexture(Width, Height, 24) { antiAliasing = 4 };
                temporary.Add(rt);
                cam.targetTexture = rt;
                // The first Camera.Render() calls from an editor script draw before material buffers and textures
                // are uploaded (everything takes one material's look); warm up, then capture.
                for (int i = 0; i < WarmUpFrames; i++) cam.Render();
                cam.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                temporary.Add(tex);
                tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                tex.Apply();
                RenderTexture.active = previous;
                cam.targetTexture = null;

                string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", output));
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllBytes(full, tex.EncodeToPNG());
                Debug.Log($"[PlaceholderArtLineup] Wrote {full}");
            }
            finally
            {
                if (replace)
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
                else
                {
                    SceneManager.SetActiveScene(previousActive);
                    EditorSceneManager.CloseScene(scene, true);
                }
                foreach (Object o in temporary)
                    if (o != null) Object.DestroyImmediate(o);
            }
        }

        /// <summary>One row per group, front to back; returns the bounds of everything placed.</summary>
        private static Bounds Layout(System.Collections.Generic.List<Object> temporary)
        {
            var all = new Bounds(Vector3.zero, Vector3.zero);
            float z = 0f;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            foreach (string[] row in Rows)
            {
                var placed = row.Select(name =>
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{PlaceholderArtBuilder.PrefabRoot}/{name}.prefab");
                    if (asset == null) throw new System.InvalidOperationException($"Missing prefab {name}");
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                    Pose(go);
                    return (go, bounds: PlaceholderArtBuilder.RendererBounds(go));
                }).ToList();

                float width = placed.Sum(p => p.bounds.size.x) + Gap * (placed.Count - 1);
                float depth = placed.Max(p => p.bounds.size.z);
                float x = -width / 2f;
                foreach (var (go, b) in placed)
                {
                    // Move so the item's bounds start at x and are centred on this row's z.
                    go.transform.position += new Vector3(x - b.min.x, 0f, z - b.center.z);
                    Bounds moved = PlaceholderArtBuilder.RendererBounds(go);
                    all.Encapsulate(moved);
                    Label(go.name.Replace("Props/", ""), new Vector3(moved.center.x, 0.02f, moved.min.z - 0.35f), font);
                    x += b.size.x + Gap;
                }

                z += depth + 2.5f;
            }

            return all;
        }

        /// <summary>Characters: player mid-run, NPCs in their idle pose (clips sampled directly).</summary>
        private static void Pose(GameObject go)
        {
            var animator = go.GetComponentInChildren<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null) return;
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // face the camera
            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            bool walk = go.name == "Player";
            AnimationClip clip = clips.First(c => c.name == (walk ? PlaceholderArtBuilder.RunClip : PlaceholderArtBuilder.IdleClip));
            clip.SampleAnimation(animator.gameObject, walk ? clip.length * 0.3f : 0f);
        }

        private static void Label(string text, Vector3 position, Font font)
        {
            var go = new GameObject("Label_" + text);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // lying on the ground, readable from above
            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = font;
            mesh.fontSize = 48;
            mesh.characterSize = 0.035f;
            mesh.anchor = TextAnchor.UpperCenter;
            mesh.color = new Color(0.1f, 0.1f, 0.12f);
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        private static Camera SetUpStage(Bounds all, System.Collections.Generic.List<Object> temporary)
        {
            var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            groundMat.SetColor("_BaseColor", new Color(0.78f, 0.8f, 0.76f));
            temporary.Add(groundMat);
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.position = new Vector3(all.center.x, -0.01f, all.center.z);
            ground.transform.localScale = new Vector3(all.size.x, 1f, all.size.z) * 0.3f;
            ground.GetComponent<Renderer>().sharedMaterial = groundMat;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            // A fresh scene's ambient probe isn't recomputed from ambientLight without a GI update; set it directly.
            var ambient = new Color(0.45f, 0.46f, 0.5f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(ambient);
            RenderSettings.ambientProbe = sh;
            RenderSettings.skybox = null;
            RenderSettings.fog = false;

            var cam = new GameObject("LineupCamera").AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.17f, 0.2f);
            cam.transform.rotation = Quaternion.Euler(35f, 0f, 0f);
            cam.transform.position = all.center - cam.transform.forward * 50f;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 200f;
            cam.aspect = (float)Width / Height;

            // Fit the bounds: project the corners into camera space.
            float halfW = 0f, halfH = 0f;
            Vector3 c = all.center, e = all.extents;
            for (int i = 0; i < 8; i++)
            {
                var corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                Vector3 local = cam.transform.InverseTransformPoint(corner) - cam.transform.InverseTransformPoint(c);
                halfW = Mathf.Max(halfW, Mathf.Abs(local.x));
                halfH = Mathf.Max(halfH, Mathf.Abs(local.y));
            }

            cam.orthographicSize = Mathf.Max(halfH, halfW / cam.aspect) * 1.08f;
            return cam;
        }
    }
}
