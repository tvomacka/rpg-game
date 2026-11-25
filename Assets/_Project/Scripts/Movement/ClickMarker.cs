using UnityEngine;

namespace RpgGame.Movement
{
    /// <summary>Small disc shown where the player clicked; hides on arrival or after a while.</summary>
    public sealed class ClickMarker : MonoBehaviour
    {
        [SerializeField] private Renderer _visual;
        [SerializeField] private PlayerMover _mover;
        [SerializeField, Min(0.1f)] private float _maxLifetime = 3f;
        [SerializeField] private float _heightOffset = 0.03f;

        private float _hideAt;

        public bool IsVisible => _visual != null && _visual.enabled;

        private void Awake() => SetVisible(false);

        public void Show(Vector3 position)
        {
            transform.position = position + Vector3.up * _heightOffset;
            _hideAt = Time.time + _maxLifetime;
            SetVisible(true);
        }

        private void Update()
        {
            if (!IsVisible) return;
            if (Time.time >= _hideAt || (_mover != null && _mover.HasArrived)) SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (_visual != null) _visual.enabled = visible;
        }
    }
}
