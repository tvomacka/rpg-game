using UnityEngine;
using UnityEngine.AI;

namespace RpgGame.Movement
{
    /// <summary>
    /// Tunable movement values for a NavMeshAgent. The values live in the asset, not in code;
    /// a stat-modifier system can feed these later.
    /// </summary>
    [CreateAssetMenu(menuName = "RpgGame/Movement Settings", fileName = "MovementSettings")]
    public sealed class MovementSettings : ScriptableObject
    {
        [Tooltip("Top walking speed in metres per second.")]
        [Min(0.1f)] public float MoveSpeed = 3.5f;

        [Tooltip("How fast the character turns to face its path, in degrees per second.")]
        [Min(1f)] public float AngularSpeed = 720f;

        [Tooltip("Acceleration in metres per second squared.")]
        [Min(0.1f)] public float Acceleration = 20f;

        [Tooltip("How close to the destination counts as arrived, in metres.")]
        [Min(0f)] public float StoppingDistance = 0.1f;

        public void ApplyTo(NavMeshAgent agent)
        {
            agent.speed = MoveSpeed;
            agent.angularSpeed = AngularSpeed;
            agent.acceleration = Acceleration;
            agent.stoppingDistance = StoppingDistance;
        }
    }
}
