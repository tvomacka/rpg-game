using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RpgGame.CameraControl
{
    /// <summary>Scroll-wheel zoom for an orthographic CinemachineCamera, clamped by <see cref="ZoomLimits"/>.</summary>
    [RequireComponent(typeof(CinemachineCamera))]
    public sealed class CameraZoom : MonoBehaviour
    {
        public const string ZoomActionPath = "Gameplay/Zoom";

        [SerializeField] private InputActionAsset _actions;
        [SerializeField] private ZoomLimits _limits = new(4f, 12f, 1f);
        [Tooltip("How quickly the lens eases to the new size. 0 = instant.")]
        [SerializeField, Min(0f)] private float _sharpness = 12f;

        private CinemachineCamera _camera;
        private InputAction _zoom;
        private float _targetSize;

        public float TargetSize => _targetSize;
        public ZoomLimits Limits => _limits;

        private void Awake()
        {
            _camera = GetComponent<CinemachineCamera>();
            _targetSize = _limits.Clamp(_camera.Lens.OrthographicSize);
        }

        private void OnEnable()
        {
            if (_actions == null)
            {
                Debug.LogError($"{nameof(CameraZoom)} on '{name}' has no Input Actions asset.", this);
                enabled = false;
                return;
            }

            _zoom = _actions.FindAction(ZoomActionPath, throwIfNotFound: true);
            _zoom.Enable();
        }

        private void OnDisable() => _zoom?.Disable();

        private void Update()
        {
            float scroll = _zoom.ReadValue<Vector2>().y;
            if (scroll != 0f) _targetSize = OrthoZoom.ApplyScroll(_targetSize, scroll, _limits);

            LensSettings lens = _camera.Lens;
            lens.OrthographicSize = OrthoZoom.Smooth(lens.OrthographicSize, _targetSize, _sharpness, Time.deltaTime);
            _camera.Lens = lens;
        }
    }
}
