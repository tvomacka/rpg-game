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
    /// (no pink), and the character prefabs animate Idle/Walk from their NavMeshAgent.
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
            "House_Wall", "House_WallDoorway", "House_WallWindow", "House_Floor",
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
        public void Character_AnimatorHasIdleAndWalkDrivenBySpeed(string name)
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
                Does.Contain((LocomotionAnimation.WalkRateParameter, AnimatorControllerParameterType.Float)));

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState idle = sm.states.Select(s => s.state).SingleOrDefault(s => s.name == LocomotionAnimation.IdleState);
            AnimatorState walk = sm.states.Select(s => s.state).SingleOrDefault(s => s.name == LocomotionAnimation.WalkState);
            Assert.That(idle, Is.Not.Null, "Idle state");
            Assert.That(walk, Is.Not.Null, "Walk state");
            Assert.That(sm.defaultState, Is.SameAs(idle), "starts in Idle");
            Assert.That(idle.motion, Is.InstanceOf<AnimationClip>(), "Idle has a clip");
            Assert.That(walk.motion, Is.InstanceOf<AnimationClip>(), "Walk has a clip");
            Assert.That(((AnimationClip)idle.motion).isLooping, Is.True, "Idle loops");
            Assert.That(((AnimationClip)walk.motion).isLooping, Is.True, "Walk loops");

            AssertTransition(idle, walk, AnimatorConditionMode.Greater);
            AssertTransition(walk, idle, AnimatorConditionMode.Less);
        }

        private static void AssertTransition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode)
        {
            AnimatorStateTransition t = from.transitions.SingleOrDefault(x => x.destinationState == to);
            Assert.That(t, Is.Not.Null, $"{from.name} -> {to.name}");
            Assert.That(t.hasExitTime, Is.False, $"{from.name} -> {to.name} reacts immediately");
            Assert.That(t.conditions, Has.Exactly(1).Matches<AnimatorCondition>(c =>
                c.parameter == LocomotionAnimation.SpeedParameter && c.mode == mode &&
                Mathf.Approximately(c.threshold, LocomotionAnimation.WalkThreshold)));
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
        public void WalkRate_StandingStill_IsOne() => Assert.That(LocomotionAnimation.WalkRate(0f, 1.5f), Is.EqualTo(1f));

        [Test]
        public void WalkRate_AtClipSpeed_IsOne() => Assert.That(LocomotionAnimation.WalkRate(1.5f, 1.5f), Is.EqualTo(1f).Within(1e-5));

        [Test]
        public void WalkRate_ScalesWithAgentSpeed() => Assert.That(LocomotionAnimation.WalkRate(3f, 1.5f), Is.EqualTo(2f).Within(1e-5));

        [Test]
        public void WalkRate_IsClamped()
        {
            Assert.That(LocomotionAnimation.WalkRate(100f, 1.5f), Is.EqualTo(LocomotionAnimation.MaxWalkRate));
            Assert.That(LocomotionAnimation.WalkRate(0.2f, 1.5f), Is.EqualTo(LocomotionAnimation.MinWalkRate));
        }

        [Test]
        public void WalkRate_BadClipSpeed_IsOne() => Assert.That(LocomotionAnimation.WalkRate(3f, 0f), Is.EqualTo(1f));
    }
}
