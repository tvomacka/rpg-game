using System.Collections;
using NUnit.Framework;
using RpgGame.Movement;
using RpgGame.Occlusion;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RpgGame.Tests.PlayMode
{
    /// <summary>Runs against the real FirstCase scene and its baked NavMesh.</summary>
    public class FirstCaseMovementTests
    {
        public const string SceneName = "FirstCase";

        // Points in the grey-box layout (see FirstCaseSceneBuilder). The house is centred on the origin.
        public static readonly Vector3 KitchenPoint = new(2.5f, 0f, 1.2f);
        public static readonly Vector3 StudyPoint = new(-2.5f, 0f, 0.3f);
        public static readonly Vector3 YardPoint = new(-5f, 0f, -9f);

        private const float WalkTimeoutSeconds = 15f;

        public static IEnumerator LoadScene()
        {
            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);
            yield return null; // scene objects Awake/OnEnable
            yield return null; // first Update/LateUpdate (agent placed, camera positioned)
        }

        public static IEnumerator WalkUntilArrived(PlayerMover mover, float timeout = WalkTimeoutSeconds)
        {
            yield return null; // let the path compute
            float deadline = Time.time + timeout;
            while (!mover.HasArrived && Time.time < deadline) yield return null;
        }

        public static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        private PlayerMover _mover;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return LoadScene();
            _mover = Object.FindAnyObjectByType<PlayerMover>();
            Assert.That(_mover, Is.Not.Null, "FirstCase has no PlayerMover");
            Assert.That(_mover.Agent.isOnNavMesh, Is.True, "Player is not on the baked NavMesh");
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator Player_ReachesProgrammaticDestination_ThroughTheHouse()
        {
            // Yard → front door → study → doorway → kitchen: only reachable if walls and doorways baked correctly.
            Assert.That(NavMesh.SamplePosition(KitchenPoint, out NavMeshHit hit, 0.5f, NavMesh.AllAreas), Is.True,
                "Kitchen point is not on the NavMesh");
            var path = new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(_mover.Agent.nextPosition, hit.position, NavMesh.AllAreas, path), Is.True);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), "No complete path from the yard into the kitchen");

            Assert.That(_mover.MoveTo(hit.position), Is.True);
            yield return WalkUntilArrived(_mover);

            Assert.That(FlatDistance(_mover.transform.position, hit.position), Is.LessThan(0.5f),
                $"Player stopped at {_mover.transform.position}, expected near {hit.position}");
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator Player_ReachesAClickedPoint()
        {
            var input = Object.FindAnyObjectByType<ClickToMoveInput>();
            Camera cam = Camera.main;
            Assert.That(input, Is.Not.Null);
            Assert.That(cam, Is.Not.Null);

            Vector3 screen = cam.WorldToScreenPoint(YardPoint);
            ClickResolution click = input.HandleClick(screen);

            Assert.That(click.HasDestination, Is.True, $"Click was rejected: {click.Rejection}");
            Assert.That(FlatDistance(click.Destination, YardPoint), Is.LessThan(0.3f), "Click resolved to the wrong place");

            var marker = Object.FindAnyObjectByType<ClickMarker>();
            Assert.That(marker.IsVisible, Is.True, "Click marker should show at the target");

            yield return WalkUntilArrived(_mover);
            Assert.That(FlatDistance(_mover.transform.position, YardPoint), Is.LessThan(0.5f));
        }

        [UnityTest]
        public IEnumerator ClickOverUi_IsIgnored()
        {
            var input = Object.FindAnyObjectByType<ClickToMoveInput>();
            Vector3 before = _mover.transform.position;
            ClickResolution click = input.HandleClick(Camera.main.WorldToScreenPoint(YardPoint), pointerOverUi: true);
            yield return null;
            Assert.That(click.Rejection, Is.EqualTo(ClickRejection.PointerOverUi));
            Assert.That(_mover.Agent.hasPath, Is.False);
            Assert.That(FlatDistance(_mover.transform.position, before), Is.LessThan(0.01f));
        }

        [UnityTest, Timeout(45000)]
        public IEnumerator Roof_HidesInside_AndReturnsOutside()
        {
            var roof = Object.FindAnyObjectByType<RoofHider>();
            Assert.That(roof, Is.Not.Null);
            Assert.That(roof.IsRoofHidden, Is.False, "Roof should be visible while the player is in the yard");

            _mover.MoveTo(StudyPoint);
            yield return WalkUntilArrived(_mover);
            Assert.That(roof.IsRoofHidden, Is.True, "Roof should hide once the player is inside");
            Assert.That(roof.GetComponentInChildren<Renderer>(true).enabled, Is.False);

            _mover.MoveTo(YardPoint);
            yield return WalkUntilArrived(_mover);
            Assert.That(roof.IsRoofHidden, Is.False, "Roof should come back once the player leaves");
            Assert.That(roof.GetComponentInChildren<Renderer>(true).enabled, Is.True);
        }
    }
}
