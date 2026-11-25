using System.Collections;
using System.IO;
using NUnit.Framework;
using RpgGame.Movement;
using UnityEngine;
using UnityEngine.TestTools;

namespace RpgGame.Tests.PlayMode
{
    /// <summary>
    /// Not a test — a tool. Walks the player into the study and renders the game camera to
    /// TestResults/Screenshots/FirstCase-play.png. [Explicit] so normal (-nographics) runs skip it.
    /// Run with graphics, e.g.:
    ///   Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode
    ///     -testFilter RpgGame.Tests.PlayMode.ScreenshotCapture -testResults TestResults/screenshot.xml
    /// </summary>
    [Explicit("Captures a screenshot; needs a graphics device. Run by name.")]
    public class ScreenshotCapture
    {
        [UnityTest, Timeout(60000)]
        public IEnumerator CaptureFirstCaseInPlayMode()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("No graphics device (-nographics); cannot render.");

            yield return FirstCaseMovementTests.LoadScene();
            var mover = Object.FindAnyObjectByType<PlayerMover>();
            var input = Object.FindAnyObjectByType<ClickToMoveInput>();

            mover.MoveTo(FirstCaseMovementTests.StudyPoint + new Vector3(0.5f, 0f, -1f));
            yield return FirstCaseMovementTests.WalkUntilArrived(mover);
            yield return new WaitForSeconds(1.5f); // let the damped camera settle

            Camera cam = Camera.main;
            input.HandleClick(cam.WorldToScreenPoint(FirstCaseMovementTests.KitchenPoint)); // shows the click marker
            yield return null;

            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
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
            string file = Path.Combine(dir, "FirstCase-play.png");
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Debug.Log($"[ScreenshotCapture] Wrote {file}");

            Object.Destroy(tex);
            rt.Release();
        }
    }
}
