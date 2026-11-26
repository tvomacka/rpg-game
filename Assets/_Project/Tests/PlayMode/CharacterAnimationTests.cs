#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using RpgGame.Characters;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace RpgGame.Tests.PlayMode
{
    /// <summary>
    /// Issue #11: a KayKit character prefab dropped on the FirstCase NavMesh plays Walk while its agent moves
    /// and goes back to Idle when it stops.
    /// </summary>
    public class CharacterAnimationTests
    {
        private const string MiraPrefab = "Assets/_Project/Prefabs/NPC_Mira.prefab";

        [UnityTest, Timeout(30000)]
        public IEnumerator NpcPrefab_WalksWhileMoving_IdlesWhenStopped()
        {
            yield return FirstCaseMovementTests.LoadScene();

            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(MiraPrefab);
            Assert.That(prefab, Is.Not.Null);
            GameObject npc = Object.Instantiate(prefab, FirstCaseMovementTests.YardPoint, Quaternion.identity);
            var agent = npc.GetComponent<NavMeshAgent>();
            Animator animator = npc.GetComponent<AgentLocomotionAnimator>().Animator;

            yield return null;
            Assert.That(agent.isOnNavMesh, Is.True, "spawned on the NavMesh");
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(LocomotionAnimation.IdleState), Is.True, "starts idle");

            agent.SetDestination(FirstCaseMovementTests.YardPoint + new Vector3(4f, 0f, 0f));
            yield return WaitForState(animator, LocomotionAnimation.WalkState, 3f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(LocomotionAnimation.WalkState), Is.True, "walks while moving");

            yield return WaitForState(animator, LocomotionAnimation.IdleState, 8f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(LocomotionAnimation.IdleState), Is.True, "idles after arriving");
        }

        private static IEnumerator WaitForState(Animator animator, string state, float timeout)
        {
            float end = Time.time + timeout;
            while (Time.time < end && !(animator.GetCurrentAnimatorStateInfo(0).IsName(state) && !animator.IsInTransition(0)))
                yield return null;
        }
    }
}
#endif
