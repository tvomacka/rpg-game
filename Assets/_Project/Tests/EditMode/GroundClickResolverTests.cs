using System;
using NUnit.Framework;
using RpgGame.Movement;
using UnityEngine;

namespace RpgGame.Tests.EditMode
{
    public class GroundClickResolverTests
    {
        /// <summary>Fake NavMesh: everything within <see cref="Radius"/> of the origin is walkable, snapped to y = 0.</summary>
        private sealed class DiscNavMesh : INavMeshSampler
        {
            public const float Radius = 5f;
            public int Calls;
            public float LastMaxDistance;

            public bool TrySample(Vector3 point, float maxDistance, out Vector3 navMeshPoint)
            {
                Calls++;
                LastMaxDistance = maxDistance;
                var flat = new Vector3(point.x, 0f, point.z);
                if (flat.magnitude <= Radius + maxDistance && Mathf.Abs(point.y) <= maxDistance)
                {
                    navMeshPoint = Vector3.ClampMagnitude(flat, Radius);
                    return true;
                }

                navMeshPoint = default;
                return false;
            }
        }

        private DiscNavMesh _navMesh;
        private GroundClickResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            _navMesh = new DiscNavMesh();
            _resolver = new GroundClickResolver(_navMesh, 1.5f);
        }

        [Test]
        public void ClickOnWalkableGround_GivesThatPoint()
        {
            ClickResolution r = _resolver.Resolve(false, true, new Vector3(2f, 0f, 1f));
            Assert.That(r.HasDestination, Is.True);
            Assert.That(r.Destination, Is.EqualTo(new Vector3(2f, 0f, 1f)));
            Assert.That(r.Rejection, Is.EqualTo(ClickRejection.None));
        }

        [Test]
        public void ClickOnUi_IsIgnored_AndNeverTouchesTheNavMesh()
        {
            ClickResolution r = _resolver.Resolve(true, true, Vector3.zero);
            Assert.That(r.HasDestination, Is.False);
            Assert.That(r.Rejection, Is.EqualTo(ClickRejection.PointerOverUi));
            Assert.That(_navMesh.Calls, Is.Zero);
        }

        [Test]
        public void ClickOnNothing_IsRejected()
        {
            ClickResolution r = _resolver.Resolve(false, false, Vector3.zero);
            Assert.That(r.Rejection, Is.EqualTo(ClickRejection.NoGroundHit));
        }

        [Test]
        public void ClickSlightlyOffTheMesh_SnapsToNearestWalkablePoint()
        {
            // e.g. clicking the top of a low wall at the edge of the walkable area
            ClickResolution r = _resolver.Resolve(false, true, new Vector3(5.5f, 0.6f, 0f));
            Assert.That(r.HasDestination, Is.True);
            Assert.That(r.Destination, Is.EqualTo(new Vector3(5f, 0f, 0f)));
        }

        [Test]
        public void ClickFarFromTheMesh_IsRejected()
        {
            ClickResolution r = _resolver.Resolve(false, true, new Vector3(20f, 0f, 0f));
            Assert.That(r.HasDestination, Is.False);
            Assert.That(r.Rejection, Is.EqualTo(ClickRejection.OffNavMesh));
        }

        [Test]
        public void SnapDistance_IsPassedToTheSampler()
        {
            _resolver.Resolve(false, true, Vector3.zero);
            Assert.That(_navMesh.LastMaxDistance, Is.EqualTo(1.5f));
        }

        [Test]
        public void Constructor_RejectsMissingSamplerOrBadDistance()
        {
            Assert.Throws<ArgumentNullException>(() => _ = new GroundClickResolver(null, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new GroundClickResolver(_navMesh, 0f));
        }
    }
}
