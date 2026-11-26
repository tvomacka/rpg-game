using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using RpgGame.Movement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace RpgGame.Tests.PlayMode
{
    /// <summary>
    /// Cutaway walls are low only visually: the NavMesh must treat every wall as solid, with openings only at doors.
    /// </summary>
    public class FirstCaseWallTests
    {
        // Layout from FirstCaseSceneBuilder: house x ∈ [-5, 5], z ∈ [-3, 3]; front door in the south wall.
        private const float SouthWallZ = -3f;
        private const float WestWallX = -5f;
        private const float FrontDoorMinX = -3.3f;
        private const float FrontDoorMaxX = -1.7f;
        private const float FullWallHeight = 2.5f;

        private PlayerMover _mover;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return FirstCaseMovementTests.LoadScene();
            _mover = Object.FindAnyObjectByType<PlayerMover>();
            Assert.That(_mover, Is.Not.Null);
        }

        [Test]
        public void PathFromYardToStudy_GoesThroughTheFrontDoor()
        {
            // The straight line between these points crosses the low south wall well west of the door.
            Vector3 yard = OnNavMesh(new Vector3(-4.5f, 0f, -6f));
            Vector3 study = OnNavMesh(new Vector3(-4f, 0f, 0f));

            var path = new NavMeshPath();
            NavMesh.CalculatePath(yard, study, NavMesh.AllAreas, path);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));

            List<float> crossings = CrossingsOfSouthWall(path.corners);
            Assert.That(crossings, Is.Not.Empty, "Path never crossed the south wall line");
            foreach (float x in crossings)
                Assert.That(x, Is.InRange(FrontDoorMinX, FrontDoorMaxX),
                    $"Path crosses the south wall at x={x:F2}, outside the front door [{FrontDoorMinX}, {FrontDoorMaxX}]");
            AssertNeverCrossesWestWall(path.corners);
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator ClickJustAcrossACutawayWall_WalksRoundThroughTheDoor()
        {
            // Player inside the study, hugging the low south wall; click the yard right on the other side.
            Assert.That(_mover.Agent.Warp(OnNavMesh(new Vector3(-4.2f, 0f, -2.3f))), Is.True);
            yield return null;
            Vector3 target = new(-4.2f, 0f, -3.8f);

            var input = Object.FindAnyObjectByType<ClickToMoveInput>();
            ClickResolution click = input.HandleClick(Camera.main.WorldToScreenPoint(target));
            Assert.That(click.HasDestination, Is.True, $"Click rejected: {click.Rejection}");
            Assert.That(click.Destination.z, Is.LessThan(SouthWallZ), "Click should resolve to the yard side of the wall");

            while (_mover.Agent.pathPending) yield return null;
            NavMeshPath path = _mover.Agent.path;
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));

            float straight = FirstCaseMovementTests.FlatDistance(_mover.transform.position, click.Destination);
            Assert.That(path.corners.Length, Is.GreaterThan(2), "Path is a straight line through the wall");
            Assert.That(PathLength(path.corners), Is.GreaterThan(straight * 2f), "Path is not going round the wall");
            foreach (float x in CrossingsOfSouthWall(path.corners))
                Assert.That(x, Is.InRange(FrontDoorMinX, FrontDoorMaxX), $"Path crosses the wall at x={x:F2}");
        }

        [Test]
        public void NavMeshRaycast_AcrossEveryCutawayWall_IsBlocked()
        {
            GameObject walls = GameObject.Find("Level/House/Walls");
            Assert.That(walls, Is.Not.Null, "Scene has no Level/House/Walls group");

            int checkedWalls = 0;
            foreach (Transform wall in walls.transform)
            {
                Vector3 size = wall.localScale;
                if (size.y >= FullWallHeight) continue; // only the low cutaways
                checkedWalls++;

                Vector3 normal = size.x < size.z ? Vector3.right : Vector3.forward; // across the thin axis
                Vector3 centre = new(wall.position.x, 0f, wall.position.z);
                Vector3 a = OnNavMesh(centre - normal);
                Vector3 b = OnNavMesh(centre + normal);

                Assert.That(NavMesh.Raycast(a, b, out NavMeshHit hit, NavMesh.AllAreas), Is.True,
                    $"NavMesh.Raycast passes straight through '{wall.name}'");
                Assert.That(hit.hit, Is.True);

                var path = new NavMeshPath();
                if (NavMesh.CalculatePath(a, b, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete)
                    Assert.That(PathLength(path.corners), Is.GreaterThan(3f),
                        $"Path across '{wall.name}' is suspiciously short: there is a gap in the wall");
            }

            Assert.That(checkedWalls, Is.EqualTo(5), "Expected 5 cutaway wall segments (south A/B, west, partition A/B)");
        }

        // ---------- helpers ----------

        private static Vector3 OnNavMesh(Vector3 p)
        {
            Assert.That(NavMesh.SamplePosition(p, out NavMeshHit hit, 0.6f, NavMesh.AllAreas), Is.True, $"{p} is not on the NavMesh");
            return hit.position;
        }

        /// <summary>x coordinates where the path crosses the south wall line within the house's width.</summary>
        private static List<float> CrossingsOfSouthWall(Vector3[] corners)
        {
            var xs = new List<float>();
            for (int i = 1; i < corners.Length; i++)
            {
                Vector3 a = corners[i - 1], b = corners[i];
                if ((a.z - SouthWallZ) * (b.z - SouthWallZ) >= 0f) continue;
                float t = (SouthWallZ - a.z) / (b.z - a.z);
                float x = Mathf.Lerp(a.x, b.x, t);
                if (x > WestWallX - 0.2f && x < 5.2f) xs.Add(x);
            }
            return xs;
        }

        private static void AssertNeverCrossesWestWall(Vector3[] corners)
        {
            for (int i = 1; i < corners.Length; i++)
            {
                Vector3 a = corners[i - 1], b = corners[i];
                if ((a.x - WestWallX) * (b.x - WestWallX) >= 0f) continue;
                float z = Mathf.Lerp(a.z, b.z, (WestWallX - a.x) / (b.x - a.x));
                Assert.That(z, Is.Not.InRange(-3.1f, 3.1f), $"Path crosses the solid west wall at z={z:F2}");
            }
        }

        private static float PathLength(Vector3[] corners)
        {
            float length = 0f;
            for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
            return length;
        }
    }
}
