using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RpgGame.Interaction
{
    /// <summary>
    /// Every frame: what interactable is under the pointer? Highlights it, swaps the cursor and shows
    /// its name in a label. The change logic is in <see cref="HoverTracker{T}"/>.
    /// </summary>
    public sealed class InteractionHover : MonoBehaviour
    {
        public const string PointActionPath = "Gameplay/Point";

        [SerializeField] private InputActionAsset _actions;
        [SerializeField] private Camera _camera;
        [SerializeField] private InteractionSettings _settings;
        [SerializeField] private InteractionUi _ui;
        [SerializeField] private LayerMask _mask = Physics.DefaultRaycastLayers;
        [SerializeField, Min(1f)] private float _maxRayDistance = 200f;

        private readonly HoverTracker<Interactable> _tracker = new();
        private InputAction _point;

        public Interactable Current => _tracker.Current;

        private void Awake()
        {
            if (_camera == null) _camera = Camera.main;
        }

        private void OnEnable()
        {
            if (_actions == null || _settings == null)
            {
                Debug.LogError($"{nameof(InteractionHover)} on '{name}' is missing its input actions or settings.", this);
                enabled = false;
                return;
            }

            _point = _actions.FindAction(PointActionPath, throwIfNotFound: true);
            _point.Enable();
        }

        private void OnDisable()
        {
            Apply(_tracker.Clear());
            _point?.Disable();
        }

        private void Update()
        {
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Refresh(_point.ReadValue<Vector2>(), overUi);
        }

        /// <summary>Public so PlayMode tests can hover a screen position without faking devices.</summary>
        public Interactable Refresh(Vector2 screenPosition, bool pointerOverUi = false)
        {
            Interactable under = pointerOverUi ? null : InteractableUnder(_camera, screenPosition, _mask, _maxRayDistance);
            Apply(_tracker.Set(under));
            if (under != null && _ui != null) _ui.ShowHover(under.DisplayName, LabelPosition(under));
            return under;
        }

        /// <summary>The interactable whose collider is the first thing the ray hits, or null.</summary>
        public static Interactable InteractableUnder(Camera cam, Vector2 screenPosition, LayerMask mask, float maxDistance)
        {
            if (cam == null) return null;
            Ray ray = cam.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, mask, QueryTriggerInteraction.Ignore)) return null;
            var target = hit.collider.GetComponentInParent<Interactable>();
            return target != null && target.isActiveAndEnabled ? target : null;
        }

        private Vector2 LabelPosition(Interactable target)
        {
            Vector3 screen = _camera.WorldToScreenPoint(target.LabelAnchor);
            return new Vector2(screen.x, screen.y + _settings.LabelOffsetPixels);
        }

        private void Apply(HoverChange<Interactable> change)
        {
            if (!change.Changed) return;
            if (change.Previous != null) change.Previous.SetHighlighted(false, default, 0f);

            if (change.Current != null)
            {
                change.Current.SetHighlighted(true, _settings.HighlightColor, _settings.HighlightAmount);
                Cursor.SetCursor(_settings.HoverCursor, _settings.HoverCursorHotspot, CursorMode.Auto);
            }
            else
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                if (_ui != null) _ui.HideHover();
            }
        }
    }
}
