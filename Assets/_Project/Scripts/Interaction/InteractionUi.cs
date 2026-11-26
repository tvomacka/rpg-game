using UnityEngine;
using UnityEngine.UI;

namespace RpgGame.Interaction
{
    /// <summary>
    /// Placeholder uGUI for interactions: a hover label that follows the hovered object, and a text popup
    /// (title + body) for Inspect and Talk. Holds no rules about when to show or hide — the callers do.
    /// </summary>
    public sealed class InteractionUi : MonoBehaviour
    {
        [SerializeField] private RectTransform _hoverLabel;
        [SerializeField] private Text _hoverText;
        [SerializeField] private GameObject _popup;
        [SerializeField] private Text _popupTitle;
        [SerializeField] private Text _popupBody;

        public bool IsHoverLabelVisible => _hoverLabel != null && _hoverLabel.gameObject.activeSelf;
        public string HoverLabelText => _hoverText != null ? _hoverText.text : null;
        public bool IsPopupOpen => _popup != null && _popup.activeSelf;
        public string PopupTitle => _popupTitle != null ? _popupTitle.text : null;
        public string PopupBody => _popupBody != null ? _popupBody.text : null;

        private void Awake()
        {
            HideHover();
            HidePopup();
        }

        public void ShowHover(string text, Vector2 screenPosition)
        {
            if (_hoverLabel == null) return;
            _hoverText.text = text;
            var canvasRect = (RectTransform)_hoverLabel.parent;
            Canvas canvas = canvasRect.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, screenPosition, uiCamera, out Vector3 world))
                _hoverLabel.position = world;
            _hoverLabel.gameObject.SetActive(true);
        }

        public void HideHover()
        {
            if (_hoverLabel != null) _hoverLabel.gameObject.SetActive(false);
        }

        public void ShowPopup(string title, string body)
        {
            if (_popup == null) return;
            _popupTitle.text = title;
            _popupBody.text = body;
            _popup.SetActive(true);
        }

        public void HidePopup()
        {
            if (_popup != null) _popup.SetActive(false);
        }
    }
}
