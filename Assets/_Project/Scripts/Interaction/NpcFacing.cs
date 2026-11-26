using UnityEngine;

namespace RpgGame.Interaction
{
    /// <summary>
    /// Lets a person turn to face whoever talks to them, then turn back to how they stood.
    /// Rotates about Y only, at <see cref="InteractionSettings.NpcTurnSpeed"/>. Logic in <see cref="FacingState"/>.
    /// </summary>
    public sealed class NpcFacing : MonoBehaviour
    {
        [SerializeField] private InteractionSettings _settings;

        private FacingState _state;
        private float _yaw;
        private Vector3 _restEuler;

        public float RestYaw => _state.RestYaw;
        public float CurrentYaw => _yaw;
        public bool IsFacingSomeone => _state.IsFacingSomeone;
        public bool IsSettled => _state.IsSettled(_yaw);

        private void Awake()
        {
            _restEuler = transform.eulerAngles;
            _yaw = _restEuler.y;
            _state = new FacingState(_yaw);
            if (_settings == null)
            {
                Debug.LogError($"{nameof(NpcFacing)} on '{name}' has no {nameof(InteractionSettings)}.", this);
                enabled = false;
            }
        }

        public void FaceTowards(Vector3 worldPosition)
        {
            if (FacingState.TryYawTowards(transform.position, worldPosition, out float yaw)) _state.Face(yaw);
        }

        public void ReturnToRest() => _state.ReturnToRest();

        private void Update()
        {
            if (_state.IsSettled(_yaw)) return;
            _yaw = _state.Step(_yaw, _settings.NpcTurnSpeed, Time.deltaTime);
            transform.rotation = Quaternion.Euler(_restEuler.x, _yaw, _restEuler.z);
        }
    }
}
