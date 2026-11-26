using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace RpgGame.Characters
{
    /// <summary>
    /// Feeds the NavMeshAgent's speed into the character Animator (Idle/Run). Sits on the prefab root
    /// next to the agent; the Animator is on the model child.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class AgentLocomotionAnimator : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash(LocomotionAnimation.SpeedParameter);
        private static readonly int RunRateId = Animator.StringToHash(LocomotionAnimation.RunRateParameter);

        [SerializeField] private Animator _animator;

        [Tooltip("Ground speed (m/s) at which the run clip's feet don't slide, at this character's scale. " +
                 "Measured from the clip by PlaceholderArtBuilder.")]
        [FormerlySerializedAs("_walkClipSpeed")]
        [SerializeField] private float _runClipSpeed = 3.5f;

        [Tooltip("Smoothing for the Speed parameter, in seconds.")]
        [SerializeField] private float _speedDampTime = 0.1f;

        private NavMeshAgent _agent;

        public Animator Animator => _animator;
        public float RunClipSpeed => _runClipSpeed;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
        }

        private void Update()
        {
            if (_animator == null || _animator.runtimeAnimatorController == null) return;

            float speed = _agent.isActiveAndEnabled ? _agent.velocity.magnitude : 0f; // NPCs' agents are off while they stand
            _animator.SetFloat(SpeedId, speed, _speedDampTime, Time.deltaTime);
            _animator.SetFloat(RunRateId, LocomotionAnimation.RunRate(speed, _runClipSpeed));
        }
    }
}
