using RpgGame.Movement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RpgGame.Interaction
{
    /// <summary>
    /// Click an interactable → walk to its interaction point → run its action (Inspect or Talk).
    /// A click elsewhere cancels the walk and closes the popup; the popup also closes on Esc or when
    /// the player walks away. The rules live in <see cref="PendingInteraction{T}"/>,
    /// <see cref="InteractionPointResolver"/> and <see cref="InteractionMath"/>.
    /// </summary>
    [RequireComponent(typeof(PlayerMover))]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        public const string CancelActionPath = "Gameplay/Cancel";

        [SerializeField] private InputActionAsset _actions;
        [SerializeField] private PlayerMover _mover;
        [SerializeField] private InteractionSettings _settings;
        [SerializeField] private InteractionUi _ui;
        [SerializeField] private ClickMarker _marker;

        private PendingInteraction<Interactable> _pending;
        private InteractionPointResolver _resolver;
        private InputAction _cancel;
        private int _requestFrame;
        private Quaternion? _faceRotation;
        private Vector3 _popupOrigin;
        private NpcFacing _talkPartner; // faces the player while the talk popup is open

        public bool HasPending => _pending != null && _pending.HasPending;
        public Interactable PendingTarget => _pending?.Target;
        public Vector3 PendingDestination => _pending?.Destination ?? default;
        public bool IsTurning => _faceRotation.HasValue;
        public bool IsPopupOpen => _ui != null && _ui.IsPopupOpen;

        private void Awake()
        {
            if (_mover == null) _mover = GetComponent<PlayerMover>();
            if (_settings == null)
            {
                Debug.LogError($"{nameof(PlayerInteractor)} on '{name}' has no {nameof(InteractionSettings)}.", this);
                enabled = false;
                return;
            }

            _pending = new PendingInteraction<Interactable>(_settings.ArrivalTolerance);
            _resolver = new InteractionPointResolver(new UnityNavMeshSampler(), _settings.InteractionPointSnap, _settings.FallbackRange);
        }

        private void OnEnable()
        {
            if (_actions == null) return; // Esc just won't close the popup
            _cancel = _actions.FindAction(CancelActionPath, throwIfNotFound: true);
            _cancel.Enable();
        }

        private void OnDisable() => _cancel?.Disable();

        /// <summary>Start walking to <paramref name="target"/>; replaces any pending interaction.</summary>
        public bool Interact(Interactable target)
        {
            if (!enabled || target == null) return false;
            CancelAndDismiss();

            if (!_resolver.TryResolve(target.InteractionPointPosition, target.transform.position, out Vector3 destination))
            {
                Debug.LogWarning($"'{target.name}' has no reachable spot within {_settings.FallbackRange} m.", target);
                return false;
            }

            if (!_mover.MoveTo(destination)) return false;
            _pending.Request(target, destination);
            _requestFrame = Time.frameCount;
            if (_marker != null) _marker.Show(destination);
            return true;
        }

        /// <summary>The player clicked somewhere that isn't an interactable.</summary>
        public void CancelAndDismiss()
        {
            if (_pending == null) return;
            _pending.Cancel();
            _faceRotation = null;
            ClosePopup();
        }

        /// <summary>The only way the popup closes, so a talk partner always turns back.</summary>
        private void ClosePopup()
        {
            if (_ui != null) _ui.HidePopup();
            if (_talkPartner != null) _talkPartner.ReturnToRest();
            _talkPartner = null;
        }

        private void Update()
        {
            if (_cancel != null && _cancel.WasPressedThisFrame()) ClosePopup();

            // Skip the request frame: the agent's path (and so HasArrived) isn't valid until the next one.
            if (_pending.HasPending && Time.frameCount > _requestFrame)
            {
                float distance = InteractionMath.FlatDistance(transform.position, _pending.Destination);
                PendingOutcome outcome = _pending.Tick(_mover.HasArrived, distance, out Interactable done);
                if (outcome == PendingOutcome.Arrived) Run(done);
                else if (outcome == PendingOutcome.Failed && done != null)
                    Debug.Log($"Couldn't reach '{done.name}' (stopped {distance:F1} m short).", done);
            }

            if (IsPopupOpen && InteractionMath.HasWalkedAway(_popupOrigin, transform.position, _settings.PopupWalkAwayDistance))
                ClosePopup();

            TurnTowardsTarget();
        }

        private void Run(Interactable target)
        {
            if (target == null) return; // destroyed while we walked
            _mover.Agent.ResetPath();

            string body;
            if (target.Action == InteractionKind.Talk)
            {
                if (InteractionMath.TryFlatLookRotation(transform.position, target.transform.position, out Quaternion face))
                    _faceRotation = face;
                _talkPartner = target.GetComponent<NpcFacing>();
                if (_talkPartner != null) _talkPartner.FaceTowards(transform.position);
                body = InteractionMath.TalkPlaceholder(_settings.TalkPlaceholderFormat, target.DisplayName);
            }
            else
            {
                body = target.Definition != null ? target.Definition.InspectText : string.Empty;
            }

            _popupOrigin = transform.position;
            if (_ui != null) _ui.ShowPopup(target.DisplayName, body);
        }

        private void TurnTowardsTarget()
        {
            if (!_faceRotation.HasValue) return;
            float step = _mover.Agent.angularSpeed * Time.deltaTime;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, _faceRotation.Value, step);
            if (Quaternion.Angle(transform.rotation, _faceRotation.Value) < 0.5f) _faceRotation = null;
        }
    }
}
