using System.Collections;
using NUnit.Framework;
using RpgGame.Interaction;
using RpgGame.Movement;
using UnityEngine;
using UnityEngine.TestTools;

namespace RpgGame.Tests.PlayMode
{
    /// <summary>Clicking interactables in the real FirstCase scene: walk there, then inspect or talk.</summary>
    public class FirstCaseInteractionTests
    {
        private const float ActionTimeoutSeconds = 15f;

        private PlayerMover _mover;
        private ClickToMoveInput _input;
        private PlayerInteractor _interactor;
        private InteractionUi _ui;
        private Camera _camera;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return FirstCaseMovementTests.LoadScene();
            _mover = Object.FindAnyObjectByType<PlayerMover>();
            _input = Object.FindAnyObjectByType<ClickToMoveInput>();
            _interactor = Object.FindAnyObjectByType<PlayerInteractor>();
            _ui = Object.FindAnyObjectByType<InteractionUi>(FindObjectsInactive.Include);
            _camera = Camera.main;
            Assert.That(_mover, Is.Not.Null);
            Assert.That(_input, Is.Not.Null);
            Assert.That(_interactor, Is.Not.Null, "Player has no PlayerInteractor");
            Assert.That(_ui, Is.Not.Null, "Scene has no InteractionUi");
            Assert.That(_ui.IsPopupOpen, Is.False);
        }

        [Test]
        public void TheCaseObjectsAreAllInteractables()
        {
            string[] expected =
            {
                "Study_Plants", "Study_Clock", "Study_Desk", "Study_Rug", "Yard_Brazier", "NPC_Mira", "NPC_Hollen",
            };
            foreach (string name in expected)
            {
                Interactable it = Find(name);
                Assert.That(it.Definition, Is.Not.Null, $"{name} has no definition");
                Assert.That(it.DisplayName, Is.Not.Empty, $"{name} has no display name");
                Assert.That(it.InteractionPoint, Is.Not.Null, $"{name} has no interaction point");
                Assert.That(it.GetComponentInChildren<Collider>(), Is.Not.Null, $"{name} can't be clicked: no collider");
                if (it.Action == InteractionKind.Inspect)
                    Assert.That(it.Definition.InspectText, Is.Not.Empty, $"{name} has no inspect text");
            }

            Assert.That(Find("NPC_Mira").Action, Is.EqualTo(InteractionKind.Talk));
            Assert.That(Find("NPC_Hollen").Action, Is.EqualTo(InteractionKind.Talk));
        }

        [Test]
        public void EveryInteractionPoint_IsReachableFromTheStart()
        {
            foreach (Interactable it in Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None))
            {
                Assert.That(UnityEngine.AI.NavMesh.SamplePosition(it.InteractionPoint.position, out var hit, 0.5f,
                    UnityEngine.AI.NavMesh.AllAreas), Is.True, $"{it.name}'s interaction point is off the NavMesh");
                var path = new UnityEngine.AI.NavMeshPath();
                UnityEngine.AI.NavMesh.CalculatePath(_mover.Agent.nextPosition, hit.position, UnityEngine.AI.NavMesh.AllAreas, path);
                Assert.That(path.status, Is.EqualTo(UnityEngine.AI.NavMeshPathStatus.PathComplete), $"Can't walk to {it.name}");
            }
        }

        [Test]
        public void Highlight_TintsEveryVisibleMeshOfEveryInteractable()
        {
            var block = new MaterialPropertyBlock();
            int baseColor = Shader.PropertyToID("_BaseColor");
            var tint = new Color(1f, 0.85f, 0.35f);

            foreach (Interactable it in Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None))
            {
                Renderer[] renderers = it.GetComponentsInChildren<Renderer>();
                Assert.That(renderers, Is.Not.Empty, $"{it.name} has nothing to highlight");
                it.SetHighlighted(true, tint, 0.45f);
                foreach (Renderer r in renderers)
                {
                    r.GetPropertyBlock(block);
                    Assert.That(block.isEmpty, Is.False, $"{it.name}/{r.name} isn't tinted when highlighted");
                    Assert.That(block.GetColor(baseColor), Is.Not.EqualTo(r.sharedMaterial.GetColor(baseColor)),
                        $"{it.name}/{r.name}: highlight colour is the same as the material's");
                    // A plain tint barely shows on white-based textured art: it must get visibly brighter.
                    Assert.That(block.GetColor(baseColor).grayscale, Is.GreaterThan(r.sharedMaterial.GetColor(baseColor).grayscale * 1.2f),
                        $"{it.name}/{r.name} doesn't light up when highlighted");
                }

                it.SetHighlighted(false, default, 0f);
                foreach (Renderer r in renderers)
                {
                    r.GetPropertyBlock(block);
                    Assert.That(block.isEmpty, Is.True, $"{it.name}/{r.name} stays tinted");
                }
            }
        }

        [UnityTest]
        public IEnumerator HoveringTheClock_HighlightsIt_AndShowsItsName()
        {
            var hover = Object.FindAnyObjectByType<InteractionHover>();
            hover.enabled = false; // stop the real (absent) mouse from overriding the test's pointer
            Interactable clock = Find("Study_Clock");

            Interactable under = hover.Refresh(ScreenPointOf(clock));
            yield return null;
            Assert.That(under, Is.SameAs(clock), "Pointer over the clock doesn't find it");
            Assert.That(clock.IsHighlighted, Is.True);
            Assert.That(_ui.IsHoverLabelVisible, Is.True);
            Assert.That(_ui.HoverLabelText, Is.EqualTo(clock.DisplayName));

            hover.Refresh(_camera.WorldToScreenPoint(FirstCaseMovementTests.YardPoint));
            Assert.That(clock.IsHighlighted, Is.False, "Highlight stays after the pointer left");
            Assert.That(_ui.IsHoverLabelVisible, Is.False);
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator ClickingTheClock_WalksThere_AndOpensItsInspectText()
        {
            Interactable clock = Find("Study_Clock");
            Vector3 screen = ScreenPointOf(clock);

            ClickResolution click = _input.HandleClick(screen);
            Assert.That(click.Rejection, Is.EqualTo(ClickRejection.HandledByInteractable), "Click didn't reach the clock");
            Assert.That(_interactor.PendingTarget, Is.SameAs(clock));
            Assert.That(FirstCaseMovementTests.FlatDistance(_mover.Agent.destination, clock.InteractionPoint.position),
                Is.LessThan(0.5f), "Player isn't walking to the clock's interaction point (a ground move to the click point?)");

            yield return WaitForPopup();

            Assert.That(_ui.IsPopupOpen, Is.True, "Inspect popup never opened");
            Assert.That(_ui.PopupTitle, Is.EqualTo(clock.DisplayName));
            Assert.That(_ui.PopupBody, Is.EqualTo(clock.Definition.InspectText));
            Assert.That(FirstCaseMovementTests.FlatDistance(_mover.transform.position, clock.InteractionPoint.position),
                Is.LessThan(0.6f), "Popup opened before the player got there");

            // Clicking the ground closes it.
            _input.HandleClick(_camera.WorldToScreenPoint(FirstCaseMovementTests.StudyPoint));
            Assert.That(_ui.IsPopupOpen, Is.False, "Click elsewhere should close the popup");
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator ClickingMira_TurnsThePlayerToFaceHer_AndShowsThePlaceholderLine()
        {
            Interactable mira = Find("NPC_Mira");
            Assert.That(_input.HandleClick(ScreenPointOf(mira)).Rejection, Is.EqualTo(ClickRejection.HandledByInteractable));

            yield return WaitForPopup();
            Assert.That(_ui.IsPopupOpen, Is.True, "Talk popup never opened");
            Assert.That(_ui.PopupTitle, Is.EqualTo("Mira"));
            Assert.That(_ui.PopupBody, Is.EqualTo("Mira has nothing to say yet."));

            float deadline = Time.time + 3f;
            while (_interactor.IsTurning && Time.time < deadline) yield return null;

            Vector3 toMira = mira.transform.position - _mover.transform.position;
            toMira.y = 0f;
            Vector3 forward = _mover.transform.forward;
            forward.y = 0f;
            Assert.That(Vector3.Angle(forward, toMira), Is.LessThan(5f), "Player isn't facing Mira");
        }

        [UnityTest, Timeout(40000)]
        public IEnumerator Mira_TurnsToFaceThePlayer_ThenBackWhenThePopupCloses()
        {
            Interactable mira = Find("NPC_Mira");
            var facing = mira.GetComponent<NpcFacing>();
            Assert.That(facing, Is.Not.Null, "Mira has no NpcFacing");
            float restYaw = mira.transform.eulerAngles.y;

            _input.HandleClick(ScreenPointOf(mira));
            yield return WaitForPopup();
            Assert.That(_ui.IsPopupOpen, Is.True, "Talk popup never opened");
            Assert.That(facing.IsFacingSomeone, Is.True);

            yield return WaitUntilSettled(facing);
            Vector3 toPlayer = _mover.transform.position - mira.transform.position;
            toPlayer.y = 0f;
            Vector3 miraForward = mira.transform.forward;
            Assert.That(miraForward.y, Is.EqualTo(0f).Within(1e-4f), "Mira tilted: she should only turn about Y");
            Assert.That(Vector3.Angle(miraForward, toPlayer), Is.LessThan(5f), "Mira isn't facing the player");

            // Close the popup by clicking elsewhere (Esc and walking away go through the same close path).
            _input.HandleClick(_camera.WorldToScreenPoint(mira.InteractionPoint.position));
            Assert.That(_ui.IsPopupOpen, Is.False);
            Assert.That(facing.IsFacingSomeone, Is.False);

            yield return WaitUntilSettled(facing);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(mira.transform.eulerAngles.y, restYaw)), Is.LessThan(0.5f),
                "Mira didn't turn back to her original facing");
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator ClickingTheGroundMidWalk_CancelsThePendingInteraction()
        {
            Interactable clock = Find("Study_Clock");
            _input.HandleClick(ScreenPointOf(clock));
            Assert.That(_interactor.HasPending, Is.True);

            yield return new WaitForSeconds(0.5f); // part-way to the house
            Assert.That(_interactor.HasPending, Is.True, "Interaction finished suspiciously fast");

            ClickResolution ground = _input.HandleClick(_camera.WorldToScreenPoint(FirstCaseMovementTests.YardPoint));
            Assert.That(ground.HasDestination, Is.True, $"Ground click rejected: {ground.Rejection}");
            Assert.That(_interactor.HasPending, Is.False, "Ground click should cancel the pending interaction");

            yield return FirstCaseMovementTests.WalkUntilArrived(_mover);
            Assert.That(FirstCaseMovementTests.FlatDistance(_mover.transform.position, FirstCaseMovementTests.YardPoint), Is.LessThan(0.5f));
            Assert.That(_ui.IsPopupOpen, Is.False, "Cancelled interaction still opened its popup");
        }

        [UnityTest, Timeout(45000)]
        public IEnumerator Popup_ClosesWhenThePlayerWalksAway()
        {
            Interactable desk = Find("Study_Desk");
            _input.HandleClick(ScreenPointOf(desk));
            yield return WaitForPopup();
            Assert.That(_ui.IsPopupOpen, Is.True);

            _mover.MoveTo(FirstCaseMovementTests.YardPoint); // not a click: walking away on its own must close it
            yield return FirstCaseMovementTests.WalkUntilArrived(_mover);
            Assert.That(_ui.IsPopupOpen, Is.False);
        }

        // ---------- helpers ----------

        private static Interactable Find(string name)
        {
            foreach (Interactable it in Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None))
                if (it.name == name) return it;
            Assert.Fail($"No interactable named '{name}' in the scene");
            return null;
        }

        /// <summary>Screen position of the middle of the object's first collider.</summary>
        private Vector3 ScreenPointOf(Interactable it) =>
            _camera.WorldToScreenPoint(it.GetComponentInChildren<Collider>().bounds.center);

        private static IEnumerator WaitUntilSettled(NpcFacing facing)
        {
            yield return null; // let the new target take effect
            float deadline = Time.time + 5f;
            while (!facing.IsSettled && Time.time < deadline) yield return null;
            Assert.That(facing.IsSettled, Is.True, "Mira never finished turning");
        }

        private IEnumerator WaitForPopup()
        {
            float deadline = Time.time + ActionTimeoutSeconds;
            while (!_ui.IsPopupOpen && Time.time < deadline) yield return null;
        }
    }
}
