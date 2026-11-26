using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RpgGame.Characters;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace RpgGame.Tests.EditMode
{
    /// <summary>
    /// Issue #11: every KayKit placeholder prefab loads, has no missing scripts or materials, renders with URP
    /// (no pink), and the character prefabs animate Idle/Run from their NavMeshAgent.
    /// </summary>
    public class PlaceholderArtPrefabTests
    {
        private const string PrefabRoot = "Assets/_Project/Prefabs";

        private static readonly string[] CharacterPrefabs = { "Player", "NPC_Mira", "NPC_Hollen" };

        private static readonly string[] PropPrefabs =
        {
            // Study
            "Prop_Desk", "Prop_Chair", "Prop_Rug", "Prop_PlantPot_Small", "Prop_PlantPot_Large",
            "Prop_Bookshelf", "Prop_Ledger", "Prop_WallClock",
            // Kitchen
            "Prop_KitchenTable", "Prop_Stool", "Prop_KitchenShelves", "Prop_Barrel", "Prop_Keg", "Prop_Crates", "Prop_Hearth",
            // Yard
            "Prop_Brazier", "Prop_Fence", "Prop_FenceGate",
            // House pieces
            "House_Wall", "House_WallDoorway", "House_WallWindow", "House_WallLow", "House_Floor",
        };

        private static IEnumerable<string> AllPrefabPaths =>
            CharacterPrefabs.Select(n => $"{PrefabRoot}/{n}.prefab")
                .Concat(PropPrefabs.Select(n => $"{PrefabRoot}/Props/{n}.prefab"));

        private static GameObject Load(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, $"Prefab missing: {path}");
            return prefab;
        }

        [TestCaseSource(nameof(AllPrefabPaths))]
        public void Prefab_LoadsWithNoMissingScripts(string path)
        {
            GameObject prefab = Load(path);
            foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject), Is.Zero,
                    $"{path}: missing script on '{t.name}'");
        }

        [TestCaseSource(nameof(AllPrefabPaths))]
        public void Prefab_EveryRendererHasATexturedUrpMaterial(string path)
        {
            GameObject prefab = Load(path);
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty, $"{path}: no renderers");

            foreach (Renderer r in renderers)
            {
                Assert.That(r.sharedMaterials, Is.Not.Empty, $"{path}/{r.name}: no material slots");
                foreach (Material m in r.sharedMaterials)
                {
                    Assert.That(m, Is.Not.Null, $"{path}/{r.name}: missing material");
                    Assert.That(m.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"),
                        $"{path}/{r.name}: material '{m.name}' would not render correctly in URP");
                    Assert.That(m.shader.isSupported, Is.True, $"{path}/{r.name}: shader not supported");
                    Assert.That(m.GetTexture("_BaseMap"), Is.Not.Null, $"{path}/{r.name}: '{m.name}' has no atlas texture");
                }
            }
        }

        [TestCaseSource(nameof(CharacterPrefabs))]
        public void Character_HasAgentAndAnimatorDriver(string name)
        {
            GameObject prefab = Load($"{PrefabRoot}/{name}.prefab");
            Assert.That(prefab.GetComponent<NavMeshAgent>(), Is.Not.Null, "NavMeshAgent on the root");

            var driver = prefab.GetComponent<AgentLocomotionAnimator>();
            Assert.That(driver, Is.Not.Null, "AgentLocomotionAnimator on the root");
            Assert.That(driver.Animator, Is.Not.Null, "driver is wired to the model's Animator");
            Assert.That(driver.Animator.applyRootMotion, Is.False, "the NavMeshAgent moves the character, not root motion");
        }

        [TestCaseSource(nameof(CharacterPrefabs))]
        public void Character_AnimatorHasIdleAndRunDrivenBySpeed(string name)
        {
            GameObject prefab = Load($"{PrefabRoot}/{name}.prefab");
            var animator = prefab.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null, "Animator");
            Assert.That(animator.avatar, Is.Not.Null, "Animator has the model's avatar");

            var controller = animator.runtimeAnimatorController as AnimatorController;
            Assert.That(controller, Is.Not.Null, "Animator Controller assigned");

            Assert.That(controller.parameters.Select(p => (p.name, p.type)),
                Does.Contain((LocomotionAnimation.SpeedParameter, AnimatorControllerParameterType.Float)));
            Assert.That(controller.parameters.Select(p => (p.name, p.type)),
                Does.Contain((LocomotionAnimation.RunRateParameter, AnimatorControllerParameterType.Float)));

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState idle = sm.states.Select(s => s.state).SingleOrDefault(s => s.name == LocomotionAnimation.IdleState);
            AnimatorState run = sm.states.Select(s => s.state).SingleOrDefault(s => s.name == LocomotionAnimation.RunState);
            Assert.That(idle, Is.Not.Null, "Idle state");
            Assert.That(run, Is.Not.Null, "Run state");
            Assert.That(sm.defaultState, Is.SameAs(idle), "starts in Idle");
            Assert.That(idle.motion, Is.InstanceOf<AnimationClip>(), "Idle has a clip");
            Assert.That(run.motion, Is.InstanceOf<AnimationClip>(), "Run has a clip");
            Assert.That(run.motion.name, Is.EqualTo("Running_A"), "Run plays the pack's run clip (owner decision, #11)");
            Assert.That(((AnimationClip)idle.motion).isLooping, Is.True, "Idle loops");
            Assert.That(((AnimationClip)run.motion).isLooping, Is.True, "Run loops");

            AssertTransition(idle, run, AnimatorConditionMode.Greater);
            AssertTransition(run, idle, AnimatorConditionMode.Less);
        }

        private static void AssertTransition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode)
        {
            AnimatorStateTransition t = from.transitions.SingleOrDefault(x => x.destinationState == to);
            Assert.That(t, Is.Not.Null, $"{from.name} -> {to.name}");
            Assert.That(t.hasExitTime, Is.False, $"{from.name} -> {to.name} reacts immediately");
            Assert.That(t.conditions, Has.Exactly(1).Matches<AnimatorCondition>(c =>
                c.parameter == LocomotionAnimation.SpeedParameter && c.mode == mode &&
                Mathf.Approximately(c.threshold, LocomotionAnimation.MoveThreshold)));
        }

        /// <summary>
        /// No foot sliding at the game's movement speed: the run clip's measured ground speed must be close enough
        /// to MovementSettings.MoveSpeed that the playback rate needed to match it isn't clamped.
        /// </summary>
        [TestCaseSource(nameof(CharacterPrefabs))]
        public void Character_RunClipMatchesTheMovementSpeedWithoutClamping(string name)
        {
            var driver = Load($"{PrefabRoot}/{name}.prefab").GetComponent<AgentLocomotionAnimator>();
            var movement = AssetDatabase.LoadAssetAtPath<RpgGame.Movement.MovementSettings>("Assets/_Project/Data/PlayerMovement.asset");
            Assert.That(movement, Is.Not.Null);

            Assert.That(driver.RunClipSpeed, Is.InRange(1.5f, 6f), "measured run clip ground speed (m/s) is implausible");
            float needed = movement.MoveSpeed / driver.RunClipSpeed;
            Assert.That(needed, Is.InRange(LocomotionAnimation.MinRunRate, LocomotionAnimation.MaxRunRate),
                $"at {movement.MoveSpeed} m/s the run clip would need rate {needed:F2}, which is clamped: feet would slide");
            Assert.That(LocomotionAnimation.RunRate(movement.MoveSpeed, driver.RunClipSpeed) * driver.RunClipSpeed,
                Is.EqualTo(movement.MoveSpeed).Within(1e-3f), "feet move at the agent's speed");
        }

        [Test]
        public void Characters_UseThreeDifferentModels()
        {
            // #11 part 2: the player, Mira and Hollen must be easy to tell apart.
            var bodies = CharacterPrefabs.Select(n => string.Join(",",
                Load($"{PrefabRoot}/{n}.prefab").GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Select(r => r.sharedMesh.name).OrderBy(x => x))).ToArray();
            Assert.That(bodies.Distinct().Count(), Is.EqualTo(CharacterPrefabs.Length), string.Join(" | ", bodies));
        }

        [Test]
        public void LowWall_IsTheWallCutDownToCutawayHeight()
        {
            Bounds Size(string prefab)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(Load($"{PrefabRoot}/Props/{prefab}.prefab"));
                try
                {
                    return instance.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }

            Bounds wall = Size("House_Wall"), low = Size("House_WallLow");
            Assert.That(low.size.y, Is.EqualTo(0.6f).Within(0.02f), "cutaway height");
            Assert.That(low.min.y, Is.EqualTo(wall.min.y).Within(0.01f), "same base");
            Assert.That(low.size.x, Is.EqualTo(wall.size.x).Within(0.05f), "same length");
            Assert.That(low.size.z, Is.EqualTo(wall.size.z).Within(0.05f), "same thickness");
        }

        [Test]
        public void Player_IsIgnoredByClickRaycasts()
        {
            GameObject prefab = Load($"{PrefabRoot}/Player.prefab");
            int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
            foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
                Assert.That(t.gameObject.layer, Is.EqualTo(ignoreRaycast), $"'{t.name}' would block ground clicks");
        }

        [Test]
        public void Characters_AreRoughlyHumanHeight()
        {
            foreach (string name in CharacterPrefabs)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(Load($"{PrefabRoot}/{name}.prefab"));
                try
                {
                    Bounds b = instance.GetComponentsInChildren<Renderer>()
                        .Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
                    // Bind pose is a T-pose, so only height is meaningful here.
                    Assert.That(b.size.y, Is.InRange(1.4f, 2.1f), $"{name} height");
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }
    }

    public class LocomotionAnimationTests
    {
        [Test]
        public void RunRate_StandingStill_IsOne() => Assert.That(LocomotionAnimation.RunRate(0f, 1.5f), Is.EqualTo(1f));

        [Test]
        public void RunRate_AtClipSpeed_IsOne() => Assert.That(LocomotionAnimation.RunRate(1.5f, 1.5f), Is.EqualTo(1f).Within(1e-5));

        [Test]
        public void RunRate_ScalesWithAgentSpeed() => Assert.That(LocomotionAnimation.RunRate(3f, 1.5f), Is.EqualTo(2f).Within(1e-5));

        [Test]
        public void RunRate_IsClamped()
        {
            Assert.That(LocomotionAnimation.RunRate(100f, 1.5f), Is.EqualTo(LocomotionAnimation.MaxRunRate));
            Assert.That(LocomotionAnimation.RunRate(0.2f, 1.5f), Is.EqualTo(LocomotionAnimation.MinRunRate));
        }

        [Test]
        public void RunRate_BadClipSpeed_IsOne() => Assert.That(LocomotionAnimation.RunRate(3f, 0f), Is.EqualTo(1f));
    }
}
