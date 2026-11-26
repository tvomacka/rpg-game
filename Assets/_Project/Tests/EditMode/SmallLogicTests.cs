using NUnit.Framework;
using RpgGame.CameraControl;
using RpgGame.Movement;
using RpgGame.Occlusion;
using UnityEngine;

namespace RpgGame.Tests.EditMode
{
    public class OccupancyCounterTests
    {
        [Test]
        public void StartsEmpty() => Assert.That(new OccupancyCounter().IsOccupied, Is.False);

        [Test]
        public void EnterThenExit_IsEmptyAgain()
        {
            var c = new OccupancyCounter();
            c.Enter();
            Assert.That(c.IsOccupied, Is.True);
            c.Exit();
            Assert.That(c.IsOccupied, Is.False);
        }

        [Test]
        public void TwoCollidersInside_StaysOccupiedUntilBothLeave()
        {
            var c = new OccupancyCounter();
            c.Enter();
            c.Enter();
            c.Exit();
            Assert.That(c.IsOccupied, Is.True);
            c.Exit();
            Assert.That(c.IsOccupied, Is.False);
        }

        [Test]
        public void UnmatchedExit_DoesNotGoNegative()
        {
            var c = new OccupancyCounter();
            c.Exit();
            c.Enter();
            Assert.That(c.IsOccupied, Is.True);
            Assert.That(c.Count, Is.EqualTo(1));
        }
    }

    public class ArrivalTests
    {
        [Test]
        public void PathPending_IsNeverArrived() =>
            Assert.That(Arrival.HasArrived(true, 0f, 0.1f), Is.False);

        [Test]
        public void WithinStoppingDistance_IsArrived() =>
            Assert.That(Arrival.HasArrived(false, 0.1f, 0.1f), Is.True);

        [Test]
        public void FarAway_IsNotArrived() =>
            Assert.That(Arrival.HasArrived(false, 3f, 0.1f), Is.False);
    }

    public class IsometricViewTests
    {
        [Test]
        public void CameraAtOffset_LooksAtTheTarget()
        {
            Vector3 target = new(3f, 0f, -2f);
            Vector3 camPos = target + IsometricView.FollowOffset(30f, 45f, 40f);
            Vector3 forward = IsometricView.Rotation(30f, 45f) * Vector3.forward;
            Vector3 toTarget = (target - camPos).normalized;
            Assert.That(Vector3.Dot(forward, toTarget), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void DefaultView_SitsAboveAndSouthWestOfTheTarget()
        {
            Vector3 offset = IsometricView.FollowOffset(IsometricView.DefaultPitch, IsometricView.DefaultYaw, 40f);
            Assert.That(offset.y, Is.EqualTo(20f).Within(1e-3f), "30° pitch at 40 m → 20 m up");
            Assert.That(offset.x, Is.LessThan(0f));
            Assert.That(offset.z, Is.LessThan(0f));
            Assert.That(offset.x, Is.EqualTo(offset.z).Within(1e-4f), "45° yaw → equal x and z");
        }
    }
}
