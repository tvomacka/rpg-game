using System.Collections;
using System.IO;
using NUnit.Framework;
using RpgGame.Interaction;
using RpgGame.Movement;
using UnityEngine;
using UnityEngine.TestTools;

namespace RpgGame.Tests.PlayMode
{
    /// <summary>
    /// Not a test — a tool. Renders the game camera to TestResults/Screenshots/*.png.
    /// [Explicit] so normal (-nographics) runs skip it. Run with graphics, e.g.:
    ///   Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode
    ///     -testFilter RpgGame.Tests.PlayMode.ScreenshotCapture -testResults TestResults/screenshot.xml
    /// </summary>
    [Explicit("Captures a screenshot; needs a graphics device. Run by name.")]
    public class ScreenshotCapture
    {
        [UnityTest, Timeout(60000)]
        public IEnumerator CaptureFirstCaseInPlayMode()
        {
            RequireGraphics();
            yield return FirstCaseMovementTests.LoadScene();
            var mover = Object.FindAnyObjectByType<PlayerMover>();
            var input = Object.FindAnyObjectByType<ClickToMoveInput>();

            mover.MoveTo(FirstCaseMovementTests.StudyPoint + new Vector3(0.5f, 0f, -1f));
            yield return FirstCaseMovementTests.WalkUntilArrived(mover);
            yield return new WaitForSeconds(1.5f); // let the damped camera settle

            Camera cam = Camera.main;
            input.HandleClick(cam.WorldToScreenPoint(FirstCaseMovementTests.KitchenPoint)); // shows the click marker
            yield return null;

            yield return Capture(cam, "FirstCase-play.png");
        }

        /// <summary>M2: the player has inspected the clock (popup open) and the pointer hovers the desk (label).</summary>
        [UnityTest, Timeout(60000)]
        public IEnumerator CaptureInteractInPlayMode()
        {
            RequireGraphics();
            yield return FirstCaseMovementTests.LoadScene();
            var interactor = Object.FindAnyObjectByType<PlayerInteractor>();
            var hover = Object.FindAnyObjectByType<InteractionHover>();
            var ui = Object.FindAnyObjectByType<InteractionUi>();
            hover.enabled = false;

            Interactable clock = null, desk = null;
            foreach (Interactable it in Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None))
            {
                if (it.name == "Study_Clock") clock = it;
                if (it.name == "Study_Desk") desk = it;
            }

            interactor.Interact(clock);
            float deadline = Time.time + 15f;
            while (!ui.IsPopupOpen && Time.time < deadline) yield return null;
            yield return new WaitForSeconds(1.5f); // camera settles

            Camera cam = Camera.main;
            var canvas = ui.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; // overlay UI doesn't render into a camera's RenderTexture
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;

            yield return Capture(cam, "M2-Interact.png", beforeRender: () =>
                hover.Refresh(cam.WorldToScreenPoint(desk.GetComponentInChildren<Collider>().bounds.center)));
        }

        // ---------- helpers ----------

        private static void RequireGraphics()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("No graphics device (-nographics); cannot render.");
        }

        private static IEnumerator Capture(Camera cam, string fileName, System.Action beforeRender = null)
        {
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            yield return null; // camera now renders at the RT's size
            beforeRender?.Invoke(); // screen positions are only right once the RT size applies
            yield return null; // WaitForEndOfFrame never fires in batch mode; the next frame is enough
            yield return null;

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            cam.targetTexture = null;

            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TestResults", "Screenshots"));
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, fileName);
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Debug.Log($"[ScreenshotCapture] Wrote {file}");

            Object.Destroy(tex);
            rt.Release();
        }
    }
}
