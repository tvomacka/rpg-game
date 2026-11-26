using RpgGame.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RpgGame.Movement
{
    /// <summary>
    /// Left click → raycast from the camera. An <see cref="Interactable"/> under the pointer goes to the
    /// <see cref="PlayerInteractor"/> (and never also becomes a ground move); anything else cancels a pending
    /// interaction and goes <see cref="GroundClickResolver"/> → walk there.
    /// Polls in Update (not in an input callback) so the UI check sees the current frame's pointer state.
    /// </summary>
    public sealed class ClickToMoveInput : MonoBehaviour
    {
        public const string ClickActionPath = "Gameplay/Click";
        public const string PointActionPath = "Gameplay/Point";

        [SerializeField] private InputActionAsset _actions;
        [SerializeField] private Camera _camera;
        [SerializeField] private PlayerMover _mover;
        [SerializeField] private ClickMarker _marker;
        [Tooltip("Optional. Without it, clicking an interactable walks to the clicked point like any ground click.")]
        [SerializeField] private PlayerInteractor _interactor;
        [SerializeField] private LayerMask _groundMask = Physics.DefaultRaycastLayers;
        [SerializeField, Min(1f)] private float _maxRayDistance = 200f;
        [SerializeField, Min(0.1f)] private float _maxSnapDistance = 1.5f;

        private InputAction _click;
        private InputAction _point;
        private GroundClickResolver _resolver;

        private void Awake()
        {
            _resolver = new GroundClickResolver(new UnityNavMeshSampler(), _maxSnapDistance);
            if (_camera == null) _camera = Camera.main;
        }

        private void OnEnable()
        {
            if (_actions == null)
            {
                Debug.LogError($"{nameof(ClickToMoveInput)} on '{name}' has no Input Actions asset.", this);
                enabled = false;
                return;
            }

            _click = _actions.FindAction(ClickActionPath, throwIfNotFound: true);
            _point = _actions.FindAction(PointActionPath, throwIfNotFound: true);
            _click.Enable();
            _point.Enable();
        }

        private void OnDisable()
        {
            _click?.Disable();
            _point?.Disable();
        }

        private void Update()
        {
            if (_click.WasPressedThisFrame()) HandleClick(_point.ReadValue<Vector2>(), IsPointerOverUi());
        }

        /// <summary>Public so PlayMode tests can click a screen position without faking devices.</summary>
        public ClickResolution HandleClick(Vector2 screenPosition, bool pointerOverUi = false)
        {
            bool hit = false;
            Vector3 hitPoint = default;
            if (!pointerOverUi && _camera != null)
            {
                Ray ray = _camera.ScreenPointToRay(screenPosition);
                hit = Physics.Raycast(ray, out RaycastHit info, _maxRayDistance, _groundMask, QueryTriggerInteraction.Ignore);
                hitPoint = info.point;

                if (hit && _interactor != null)
                {
                    var target = info.collider.GetComponentInParent<Interactable>();
                    if (target != null && target.isActiveAndEnabled)
                    {
                        _interactor.Interact(target);
                        return ClickResolution.Reject(ClickRejection.HandledByInteractable);
                    }
                }
            }

            // Any click that isn't on the UI or an interactable cancels a pending interaction and closes its popup.
            if (!pointerOverUi && _interactor != null) _interactor.CancelAndDismiss();

            ClickResolution result = _resolver.Resolve(pointerOverUi, hit, hitPoint);
            if (result.HasDestination && _mover.MoveTo(result.Destination) && _marker != null)
                _marker.Show(result.Destination);
            return result;
        }

        private static bool IsPointerOverUi() =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
