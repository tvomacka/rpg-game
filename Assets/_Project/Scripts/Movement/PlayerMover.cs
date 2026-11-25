using UnityEngine;
using UnityEngine.AI;

namespace RpgGame.Movement
{
    /// <summary>Thin wrapper over the player's NavMeshAgent; movement values come from <see cref="MovementSettings"/>.</summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class PlayerMover : MonoBehaviour
    {
        [SerializeField] private MovementSettings _settings;

        private NavMeshAgent _agent;

        public NavMeshAgent Agent => _agent;

        public bool HasArrived =>
            _agent != null && Arrival.HasArrived(_agent.pathPending, _agent.remainingDistance, _agent.stoppingDistance);

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            if (_settings == null)
            {
                Debug.LogError($"{nameof(PlayerMover)} on '{name}' has no {nameof(MovementSettings)} assigned.", this);
                enabled = false;
                return;
            }

            _settings.ApplyTo(_agent);
        }

        public bool MoveTo(Vector3 destination) => enabled && _agent.isOnNavMesh && _agent.SetDestination(destination);
    }
}
