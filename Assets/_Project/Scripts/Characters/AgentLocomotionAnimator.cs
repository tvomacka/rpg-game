using UnityEngine;
using UnityEngine.AI;

namespace RpgGame.Characters
{
    /// <summary>
    /// Feeds the NavMeshAgent's speed into the character Animator (Idle/Walk). Sits on the prefab root
    /// next to the agent; the Animator is on the model child.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class AgentLocomotionAnimator : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash(LocomotionAnimation.SpeedParameter);
        private static readonly int WalkRateId = Animator.StringToHash(LocomotionAnimation.WalkRateParameter);

        [SerializeField] private Animator _animator;

        [Tooltip("Ground speed (m/s) at which the walk clip's feet don't slide, at this character's scale.")]
        [SerializeField] private float _walkClipSpeed = 1.5f;

        [Tooltip("Smoothing for the Speed parameter, in seconds.")]
        [SerializeField] private float _speedDampTime = 0.1f;

        private NavMeshAgent _agent;

        public Animator Animator => _animator;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
        }

        private void Update()
        {
            if (_animator == null || _animator.runtimeAnimatorController == null) return;

            float speed = _agent.velocity.magnitude;
            _animator.SetFloat(SpeedId, speed, _speedDampTime, Time.deltaTime);
            _animator.SetFloat(WalkRateId, LocomotionAnimation.WalkRate(speed, _walkClipSpeed));
        }
    }
}
