using System;
using UnityEngine;

namespace RpgGame.Movement
{
    /// <summary>Finds the nearest walkable point; abstracted so tests can fake the NavMesh.</summary>
    public interface INavMeshSampler
    {
        bool TrySample(Vector3 point, float maxDistance, out Vector3 navMeshPoint);
    }

    public enum ClickRejection
    {
        None,
        PointerOverUi,
        NoGroundHit,
        OffNavMesh,
        /// <summary>The click hit an interactable; the interactor walks there instead.</summary>
        HandledByInteractable,
    }

    public readonly struct ClickResolution
    {
        public readonly bool HasDestination;
        public readonly Vector3 Destination;
        public readonly ClickRejection Rejection;

        private ClickResolution(bool hasDestination, Vector3 destination, ClickRejection rejection)
        {
            HasDestination = hasDestination;
            Destination = destination;
            Rejection = rejection;
        }

        public static ClickResolution Accept(Vector3 destination) => new(true, destination, ClickRejection.None);
        public static ClickResolution Reject(ClickRejection reason) => new(false, default, reason);
    }

    /// <summary>
    /// Turns "the player clicked here" into a walk destination:
    /// UI clicks are ignored, the ray must hit something, and the hit point is snapped to the NavMesh
    /// (so clicking a wall or a table walks to the nearest reachable spot next to it).
    /// </summary>
    public sealed class GroundClickResolver
    {
        private readonly INavMeshSampler _sampler;
        private readonly float _maxSnapDistance;

        public GroundClickResolver(INavMeshSampler sampler, float maxSnapDistance)
        {
            _sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
            if (maxSnapDistance <= 0f) throw new ArgumentOutOfRangeException(nameof(maxSnapDistance));
            _maxSnapDistance = maxSnapDistance;
        }

        public ClickResolution Resolve(bool pointerOverUi, bool rayHitSomething, Vector3 hitPoint)
        {
            if (pointerOverUi) return ClickResolution.Reject(ClickRejection.PointerOverUi);
            if (!rayHitSomething) return ClickResolution.Reject(ClickRejection.NoGroundHit);
            return _sampler.TrySample(hitPoint, _maxSnapDistance, out Vector3 onMesh)
                ? ClickResolution.Accept(onMesh)
                : ClickResolution.Reject(ClickRejection.OffNavMesh);
        }
    }
}
