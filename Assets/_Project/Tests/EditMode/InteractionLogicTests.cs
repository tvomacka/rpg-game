using System;
using System.Collections.Generic;
using NUnit.Framework;
using RpgGame.Interaction;
using RpgGame.Movement;
using UnityEngine;

namespace RpgGame.Tests.EditMode
{
    public class InteractionPointResolverTests
    {
        /// <summary>Fake NavMesh: a list of walkable points; sampling returns the nearest one within range.</summary>
        private sealed class PointsNavMesh : INavMeshSampler
        {
            public readonly List<Vector3> Walkable = new();
            public readonly List<float> Ranges = new();

            public bool TrySample(Vector3 point, float maxDistance, out Vector3 navMeshPoint)
            {
                Ranges.Add(maxDistance);
                navMeshPoint = default;
                float best = float.MaxValue;
                foreach (Vector3 w in Walkable)
                {
                    float d = Vector3.Distance(point, w);
                    if (d > maxDistance || d >= best) continue;
                    best = d;
                    navMeshPoint = w;
                }
                return best < float.MaxValue;
            }
        }

        private PointsNavMesh _navMesh;
        private InteractionPointResolver _resolver;

        private static readonly Vector3 Desk = new(0f, 0.4f, 0f);
        private static readonly Vector3 InFrontOfDesk = new(0f, 0f, -1f);
        private static readonly Vector3 BesideDesk = new(1.5f, 0f, 0f);

        [SetUp]
        public void SetUp()
        {
            _navMesh = new PointsNavMesh();
            _resolver = new InteractionPointResolver(_navMesh, pointSnap: 0.5f, fallbackRange: 2.5f);
        }

        [Test]
        public void InteractionPointOnTheNavMesh_IsUsed_EvenIfAnotherPointIsCloserToTheObject()
        {
            _navMesh.Walkable.Add(InFrontOfDesk);
            _navMesh.Walkable.Add(new Vector3(0.6f, 0f, 0f)); // closer to the desk, but not where we asked to stand
            Assert.That(_resolver.TryResolve(InFrontOfDesk, Desk, out Vector3 destination), Is.True);
            Assert.That(destination, Is.EqualTo(InFrontOfDesk));
        }

        [Test]
        public void InteractionPointSlightlyOff_SnapsWithinTheSmallSnapDistance()
        {
            _navMesh.Walkable.Add(InFrontOfDesk);
            Assert.That(_resolver.TryResolve(InFrontOfDesk + new Vector3(0.3f, 0f, 0f), Desk, out Vector3 destination), Is.True);
            Assert.That(destination, Is.EqualTo(InFrontOfDesk));
            Assert.That(_navMesh.Ranges[0], Is.EqualTo(0.5f), "The interaction point is sampled with the snap distance");
        }

        [Test]
        public void NoInteractionPoint_FallsBackToNearestNavMeshPointWithinRange()
        {
            _navMesh.Walkable.Add(BesideDesk);
            _navMesh.Walkable.Add(new Vector3(2.4f, 0f, 0f));
            Assert.That(_resolver.TryResolve(null, Desk, out Vector3 destination), Is.True);
            Assert.That(destination, Is.EqualTo(BesideDesk));
            Assert.That(_navMesh.Ranges, Is.EqualTo(new[] { 2.5f }), "Only the fallback sample, with the fallback range");
        }

        [Test]
        public void InteractionPointOffTheNavMesh_FallsBackToTheObject()
        {
            _navMesh.Walkable.Add(BesideDesk);
            var insideAWall = new Vector3(-3f, 0f, 0f);
            Assert.That(_resolver.TryResolve(insideAWall, Desk, out Vector3 destination), Is.True);
            Assert.That(destination, Is.EqualTo(BesideDesk));
        }

        [Test]
        public void NothingWithinRange_CannotBeReached()
        {
            _navMesh.Walkable.Add(new Vector3(10f, 0f, 0f));
            Assert.That(_resolver.TryResolve(null, Desk, out _), Is.False);
            Assert.That(_resolver.TryResolve(InFrontOfDesk, Desk, out _), Is.False);
        }

        [Test]
        public void Constructor_RejectsBadArguments()
        {
            Assert.Throws<ArgumentNullException>(() => _ = new InteractionPointResolver(null, 0.5f, 2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new InteractionPointResolver(_navMesh, 0f, 2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new InteractionPointResolver(_navMesh, 0.5f, 0f));
        }
    }

    public class PendingInteractionTests
    {
        private sealed class Thing { }

        private readonly Thing _clock = new();
        private readonly Thing _mira = new();
        private PendingInteraction<Thing> _pending;

        [SetUp]
        public void SetUp() => _pending = new PendingInteraction<Thing>(arrivalTolerance: 0.5f);

        [Test]
        public void StartsWithNothingPending()
        {
            Assert.That(_pending.HasPending, Is.False);
            Assert.That(_pending.Tick(true, 0f, out Thing done), Is.EqualTo(PendingOutcome.None));
            Assert.That(done, Is.Null);
        }

        [Test]
        public void WhileWalking_NothingRuns()
        {
            _pending.Request(_clock, Vector3.one);
            Assert.That(_pending.Tick(false, 5f, out Thing done), Is.EqualTo(PendingOutcome.Walking));
            Assert.That(done, Is.Null);
            Assert.That(_pending.HasPending, Is.True);
        }

        [Test]
        public void StillMovingButClose_KeepsWalkingUntilTheAgentStops()
        {
            _pending.Request(_clock, Vector3.one);
            Assert.That(_pending.Tick(false, 0.2f, out _), Is.EqualTo(PendingOutcome.Walking));
        }

        [Test]
        public void Arrived_RunsOnce_AndClears()
        {
            _pending.Request(_clock, Vector3.one);
            Assert.That(_pending.Tick(true, 0.3f, out Thing done), Is.EqualTo(PendingOutcome.Arrived));
            Assert.That(done, Is.SameAs(_clock));
            Assert.That(_pending.HasPending, Is.False);
            Assert.That(_pending.Tick(true, 0.3f, out _), Is.EqualTo(PendingOutcome.None), "Must not run twice");
        }

        [Test]
        public void StoppedShort_Fails_AndClears()
        {
            _pending.Request(_clock, Vector3.one);
            Assert.That(_pending.Tick(true, 3f, out Thing done), Is.EqualTo(PendingOutcome.Failed));
            Assert.That(done, Is.SameAs(_clock));
            Assert.That(_pending.HasPending, Is.False);
        }

        [Test]
        public void Cancel_BeforeArrival_MeansNothingRuns()
        {
            _pending.Request(_clock, Vector3.one);
            _pending.Cancel(); // the player clicked the ground mid-walk
            Assert.That(_pending.HasPending, Is.False);
            Assert.That(_pending.Tick(true, 0f, out Thing done), Is.EqualTo(PendingOutcome.None));
            Assert.That(done, Is.Null);
        }

        [Test]
        public void NewRequest_ReplacesTheOldOne()
        {
            _pending.Request(_clock, Vector3.one);
            _pending.Request(_mira, Vector3.zero);
            Assert.That(_pending.Target, Is.SameAs(_mira));
            Assert.That(_pending.Destination, Is.EqualTo(Vector3.zero));
            _pending.Tick(true, 0f, out Thing done);
            Assert.That(done, Is.SameAs(_mira));
        }
    }

    public class HoverTrackerTests
    {
        private sealed class Thing { }

        private readonly Thing _clock = new();
        private readonly Thing _rug = new();

        [Test]
        public void StartsWithNothingHovered() => Assert.That(new HoverTracker<Thing>().Current, Is.Null);

        [Test]
        public void HoveringSomething_IsAChangeFromNothing()
        {
            var tracker = new HoverTracker<Thing>();
            HoverChange<Thing> c = tracker.Set(_clock);
            Assert.That(c.Changed, Is.True);
            Assert.That(c.Previous, Is.Null);
            Assert.That(c.Current, Is.SameAs(_clock));
            Assert.That(tracker.Current, Is.SameAs(_clock));
        }

        [Test]
        public void StayingOnTheSameThing_IsNoChange()
        {
            var tracker = new HoverTracker<Thing>();
            tracker.Set(_clock);
            Assert.That(tracker.Set(_clock).Changed, Is.False);
        }

        [Test]
        public void MovingStraightToAnotherThing_ReportsBoth()
        {
            var tracker = new HoverTracker<Thing>();
            tracker.Set(_clock);
            HoverChange<Thing> c = tracker.Set(_rug);
            Assert.That(c.Changed, Is.True);
            Assert.That(c.Previous, Is.SameAs(_clock), "so the old one can be un-highlighted");
            Assert.That(c.Current, Is.SameAs(_rug));
        }

        [Test]
        public void LeavingAndClear_GoBackToNothing()
        {
            var tracker = new HoverTracker<Thing>();
            tracker.Set(_clock);
            HoverChange<Thing> c = tracker.Set(null);
            Assert.That(c.Changed, Is.True);
            Assert.That(c.Previous, Is.SameAs(_clock));
            Assert.That(tracker.Current, Is.Null);

            tracker.Set(_rug);
            Assert.That(tracker.Clear().Previous, Is.SameAs(_rug));
            Assert.That(tracker.Clear().Changed, Is.False, "Clearing twice is no change");
        }
    }

    public class FacingStateTests
    {
        [Test]
        public void StartsSettledAtRest()
        {
            var s = new FacingState(30f);
            Assert.That(s.TargetYaw, Is.EqualTo(30f));
            Assert.That(s.IsFacingSomeone, Is.False);
            Assert.That(s.IsSettled(30f), Is.True);
        }

        [Test]
        public void Face_TurnsAtTheGivenRate_AndLandsExactlyOnTarget()
        {
            var s = new FacingState(0f);
            s.Face(90f);
            Assert.That(s.IsFacingSomeone, Is.True);

            float yaw = s.Step(0f, degreesPerSecond: 100f, deltaTime: 0.5f);
            Assert.That(yaw, Is.EqualTo(50f).Within(1e-4f), "Turns rate × time, not instantly");
            Assert.That(s.IsSettled(yaw), Is.False);

            yaw = s.Step(yaw, 100f, 0.5f);
            Assert.That(yaw, Is.EqualTo(90f).Within(1e-4f), "Doesn't overshoot");
            Assert.That(s.IsSettled(yaw), Is.True);
        }

        [Test]
        public void Turning_TakesTheShortWayRound()
        {
            var s = new FacingState(350f);
            s.Face(10f);
            float yaw = s.Step(350f, 10f, 1f);
            Assert.That(Mathf.DeltaAngle(yaw, 0f), Is.EqualTo(0f).Within(1e-4f), "350° → 10° goes through 0°, not 180°");
        }

        [Test]
        public void ReturnToRest_TurnsBackToTheOriginalFacing()
        {
            var s = new FacingState(250f);
            s.Face(120f);
            float yaw = 120f; // facing the player
            s.ReturnToRest();
            Assert.That(s.IsFacingSomeone, Is.False);
            Assert.That(s.TargetYaw, Is.EqualTo(250f));
            Assert.That(s.IsSettled(yaw), Is.False);

            for (int i = 0; i < 100 && !s.IsSettled(yaw); i++) yaw = s.Step(yaw, 240f, 0.02f);
            Assert.That(Mathf.DeltaAngle(yaw, 250f), Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void YawTowards_IsMeasuredFromNorthClockwise_OnTheGround()
        {
            Assert.That(FacingState.TryYawTowards(Vector3.zero, new Vector3(0f, 5f, 2f), out float north), Is.True);
            Assert.That(north, Is.EqualTo(0f).Within(1e-4f));
            FacingState.TryYawTowards(Vector3.zero, new Vector3(3f, 0f, 0f), out float east);
            Assert.That(east, Is.EqualTo(90f).Within(1e-4f));
            Assert.That(FacingState.TryYawTowards(Vector3.one, Vector3.one + Vector3.up, out _), Is.False, "Same spot: no direction");
        }
    }

    public class InteractionMathTests
    {
        [Test]
        public void FlatLookRotation_FacesTheTarget_IgnoringHeight()
        {
            Assert.That(InteractionMath.TryFlatLookRotation(Vector3.zero, new Vector3(3f, 5f, 0f), out Quaternion r), Is.True);
            Vector3 forward = r * Vector3.forward;
            Assert.That(forward.x, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(forward.y, Is.EqualTo(0f).Within(1e-5f), "Never tilts up or down");
        }

        [Test]
        public void FlatLookRotation_StandingOnTheTarget_GivesNoRotation() =>
            Assert.That(InteractionMath.TryFlatLookRotation(Vector3.one, Vector3.one + Vector3.up, out _), Is.False);

        [Test]
        public void WalkingAway_IsMeasuredOnTheGround()
        {
            Assert.That(InteractionMath.HasWalkedAway(Vector3.zero, new Vector3(1f, 9f, 0f), 1.5f), Is.False, "Height doesn't count");
            Assert.That(InteractionMath.HasWalkedAway(Vector3.zero, new Vector3(1.6f, 0f, 0f), 1.5f), Is.True);
        }

        [Test]
        public void TalkPlaceholder_InsertsTheName()
        {
            Assert.That(InteractionMath.TalkPlaceholder("{0} has nothing to say yet.", "Mira"), Is.EqualTo("Mira has nothing to say yet."));
            Assert.That(InteractionMath.TalkPlaceholder(null, "Mira"), Is.EqualTo("Mira"));
        }
    }

    public class HighlightColorTests
    {
        private static readonly Color Gold = new(1f, 0.85f, 0.35f);

        [Test]
        public void WhiteBase_TexturedArt_GetsBrighterAndWarmer()
        {
            Color c = Interactable.HighlightColor(Color.white, Gold, 0.45f);
            Assert.That(c.grayscale, Is.GreaterThan(1.2f), "multiplies the texture up, so it visibly lights up");
            Assert.That(c.r, Is.GreaterThan(c.b), "warm");
        }

        [Test]
        public void DarkBase_GreyBox_GetsBrighter() =>
            Assert.That(Interactable.HighlightColor(new Color(0.3f, 0.3f, 0.3f), Gold, 0.45f).grayscale, Is.GreaterThan(0.5f));

        [Test]
        public void KeepsAlpha() =>
            Assert.That(Interactable.HighlightColor(new Color(1f, 1f, 1f, 0.5f), Gold, 0.45f).a, Is.EqualTo(0.5f));

        [Test]
        public void ZeroAmount_IsUnchanged() =>
            Assert.That(Interactable.HighlightColor(new Color(0.2f, 0.4f, 0.6f), Gold, 0f), Is.EqualTo(new Color(0.2f, 0.4f, 0.6f)));
    }
}