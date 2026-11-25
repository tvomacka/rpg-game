using System;
using NUnit.Framework;
using RpgGame.CameraControl;

namespace RpgGame.Tests.EditMode
{
    public class OrthoZoomTests
    {
        private static readonly ZoomLimits Limits = new(4f, 12f, 1f);

        [Test]
        public void ScrollUp_ZoomsIn_ByOneStep() =>
            Assert.That(OrthoZoom.ApplyScroll(7f, 1f, Limits), Is.EqualTo(6f));

        [Test]
        public void ScrollDown_ZoomsOut_ByOneStep() =>
            Assert.That(OrthoZoom.ApplyScroll(7f, -1f, Limits), Is.EqualTo(8f));

        [Test]
        public void OnlyTheSignOfTheScrollMatters() =>
            Assert.That(OrthoZoom.ApplyScroll(7f, 120f, Limits), Is.EqualTo(OrthoZoom.ApplyScroll(7f, 0.01f, Limits)));

        [Test]
        public void NoScroll_KeepsSize() =>
            Assert.That(OrthoZoom.ApplyScroll(7f, 0f, Limits), Is.EqualTo(7f));

        [Test]
        public void ZoomIn_IsClampedToMin() =>
            Assert.That(OrthoZoom.ApplyScroll(4.5f, 1f, Limits), Is.EqualTo(4f));

        [Test]
        public void ZoomOut_IsClampedToMax() =>
            Assert.That(OrthoZoom.ApplyScroll(11.5f, -1f, Limits), Is.EqualTo(12f));

        [Test]
        public void OutOfRangeStart_IsPulledBackIntoRange() =>
            Assert.That(OrthoZoom.ApplyScroll(50f, 0f, Limits), Is.EqualTo(12f));

        [Test]
        public void Limits_RejectMaxBelowMin() =>
            Assert.Throws<ArgumentException>(() => _ = new ZoomLimits(10f, 5f, 1f));

        [Test]
        public void Limits_RejectNonPositiveMin() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new ZoomLimits(0f, 5f, 1f));

        [Test]
        public void Smooth_MovesTowardTarget_WithoutOvershoot()
        {
            float next = OrthoZoom.Smooth(10f, 6f, 12f, 1f / 60f);
            Assert.That(next, Is.LessThan(10f).And.GreaterThan(6f));
        }

        [Test]
        public void Smooth_ConvergesOverTime()
        {
            float size = 10f;
            for (int i = 0; i < 120; i++) size = OrthoZoom.Smooth(size, 6f, 12f, 1f / 60f);
            Assert.That(size, Is.EqualTo(6f).Within(0.01f));
        }

        [Test]
        public void Smooth_ZeroSharpness_SnapsToTarget() =>
            Assert.That(OrthoZoom.Smooth(10f, 6f, 0f, 1f / 60f), Is.EqualTo(6f));
    }
}
