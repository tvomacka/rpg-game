using System;
using RpgGame.Movement;
using UnityEngine;

namespace RpgGame.Interaction
{
    /// <summary>
    /// Where should the player stand to use an object?
    /// 1. Its interaction point (a child transform), if that is on or near the NavMesh.
    /// 2. Otherwise the nearest NavMesh point within <c>fallbackRange</c> of the object.
    /// 3. Otherwise nowhere: the object can't be reached.
    /// </summary>
    public sealed class InteractionPointResolver
    {
        private readonly INavMeshSampler _sampler;
        private readonly float _pointSnap;
        private readonly float _fallbackRange;

        public InteractionPointResolver(INavMeshSampler sampler, float pointSnap, float fallbackRange)
        {
            _sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
            if (pointSnap <= 0f) throw new ArgumentOutOfRangeException(nameof(pointSnap));
            if (fallbackRange <= 0f) throw new ArgumentOutOfRangeException(nameof(fallbackRange));
            _pointSnap = pointSnap;
            _fallbackRange = fallbackRange;
        }

        public bool TryResolve(Vector3? interactionPoint, Vector3 objectPosition, out Vector3 destination)
        {
            if (interactionPoint.HasValue && _sampler.TrySample(interactionPoint.Value, _pointSnap, out destination))
                return true;
            return _sampler.TrySample(objectPosition, _fallbackRange, out destination);
        }
    }
}
