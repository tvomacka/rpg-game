using UnityEngine;

namespace RpgGame.Interaction
{
    /// <summary>
    /// Something the player can hover and click: a prop to inspect or a person to talk to.
    /// Needs a collider on itself or a child so the pointer raycast can find it.
    /// What it is and does comes from its <see cref="InteractableDefinition"/>.
    /// </summary>
    public sealed class Interactable : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        [SerializeField] private InteractableDefinition _definition;
        [Tooltip("Where the player stands to use this. Empty: the nearest NavMesh point within range.")]
        [SerializeField] private Transform _interactionPoint;

        private Renderer[] _renderers = new Renderer[0];
        private MaterialPropertyBlock _block;

        public InteractableDefinition Definition => _definition;
        public string DisplayName => _definition != null ? _definition.DisplayName : name;
        public InteractionKind Action => _definition != null ? _definition.Action : InteractionKind.Inspect;
        public Transform InteractionPoint => _interactionPoint;
        public Vector3? InteractionPointPosition => _interactionPoint != null ? _interactionPoint.position : null;
        public bool IsHighlighted { get; private set; }

        /// <summary>Top centre of the object's visible bounds: where the hover label sits.</summary>
        public Vector3 LabelAnchor
        {
            get
            {
                if (_renderers.Length == 0) return transform.position;
                Bounds b = _renderers[0].bounds;
                for (int i = 1; i < _renderers.Length; i++) b.Encapsulate(_renderers[i].bounds);
                return new Vector3(b.center.x, b.max.y, b.center.z);
            }
        }

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            _block = new MaterialPropertyBlock();
            if (_definition == null)
                Debug.LogWarning($"{nameof(Interactable)} on '{name}' has no {nameof(InteractableDefinition)}.", this);
        }

        private void OnDisable() => SetHighlighted(false, default, 0f);

        public void SetHighlighted(bool on, Color tint, float amount)
        {
            IsHighlighted = on;
            foreach (Renderer r in _renderers)
            {
                if (r == null) continue;
                if (!on || r.sharedMaterial == null || !r.sharedMaterial.HasProperty(BaseColorId))
                {
                    r.SetPropertyBlock(null);
                    continue;
                }

                _block.Clear();
                _block.SetColor(BaseColorId, HighlightColor(r.sharedMaterial.GetColor(BaseColorId), tint, amount));
                r.SetPropertyBlock(_block);
            }
        }

        /// <summary>
        /// Tinted towards <paramref name="tint"/> and brightened. Textured art has a white base colour that the texture
        /// is multiplied by, so a plain tint barely shows on it; going above 1 lights the texture up instead.
        /// </summary>
        public static Color HighlightColor(Color baseColor, Color tint, float amount)
        {
            Color c = Color.Lerp(baseColor, tint, amount) * (1f + amount);
            c.a = baseColor.a;
            return c;
        }

        private void OnDrawGizmosSelected()
        {
            if (_interactionPoint == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(_interactionPoint.position, 0.3f);
            Gizmos.DrawLine(_interactionPoint.position, transform.position);
        }
    }
}
