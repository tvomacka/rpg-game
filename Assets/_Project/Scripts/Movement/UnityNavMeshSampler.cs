using UnityEngine;
using UnityEngine.AI;

namespace RpgGame.Movement
{
    /// <summary>The real NavMesh behind <see cref="INavMeshSampler"/>.</summary>
    public sealed class UnityNavMeshSampler : INavMeshSampler
    {
        public bool TrySample(Vector3 point, float maxDistance, out Vector3 navMeshPoint)
        {
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, maxDistance, NavMesh.AllAreas))
            {
                navMeshPoint = hit.position;
                return true;
            }

            navMeshPoint = default;
            return false;
        }
    }
}
