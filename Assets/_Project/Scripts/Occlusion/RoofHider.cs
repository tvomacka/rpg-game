using RpgGame.Movement;
using UnityEngine;

namespace RpgGame.Occlusion
{
    /// <summary>
    /// Sits on a trigger volume covering a building's interior. While the player is inside,
    /// the roof renderers are switched off so the isometric camera can see in.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class RoofHider : MonoBehaviour
    {
        [SerializeField] private Renderer[] _roofRenderers = new Renderer[0];

        private readonly OccupancyCounter _occupancy = new();

        public bool IsRoofHidden => _occupancy.IsOccupied;

        private void Reset() => GetComponent<Collider>().isTrigger = true;

        private void OnEnable() => Apply();

        private void OnDisable()
        {
            _occupancy.Reset();
            Apply();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayer(other)) return;
            _occupancy.Enter();
            Apply();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsPlayer(other)) return;
            _occupancy.Exit();
            Apply();
        }

        private static bool IsPlayer(Collider other) => other.GetComponentInParent<PlayerMover>() != null;

        private void Apply()
        {
            bool visible = !_occupancy.IsOccupied;
            foreach (Renderer r in _roofRenderers)
                if (r != null) r.enabled = visible;
        }
    }
}
