using UnityEngine;

namespace RpgGame.Interaction
{
    /// <summary>Shared tuning and text for every interactable. Values live in the asset, not in code.</summary>
    [CreateAssetMenu(menuName = "RpgGame/Interaction Settings", fileName = "InteractionSettings")]
    public sealed class InteractionSettings : ScriptableObject
    {
        [Header("Walking there")]
        [Tooltip("How far an interaction point may be from the NavMesh and still be used, in metres.")]
        [Min(0.01f)] public float InteractionPointSnap = 0.5f;

        [Tooltip("No usable interaction point: walk to the nearest NavMesh point within this range of the object.")]
        [Min(0.01f)] public float FallbackRange = 2.5f;

        [Tooltip("How close to the destination counts as arrived for an interaction, in metres.")]
        [Min(0.01f)] public float ArrivalTolerance = 0.5f;

        [Header("Popup")]
        [Tooltip("The popup closes once the player is this far from where it opened, in metres.")]
        [Min(0.1f)] public float PopupWalkAwayDistance = 1.5f;

        [Tooltip("Talk line until dialogue exists (M4). {0} is the person's display name.")]
        public string TalkPlaceholderFormat;

        [Header("Talking")]
        [Tooltip("How fast a person turns to face the player, and back afterwards, in degrees per second.")]
        [Min(1f)] public float NpcTurnSpeed = 240f;

        [Header("Hover")]
        public Color HighlightColor = new(1f, 0.85f, 0.35f);
        [Range(0f, 1f)] public float HighlightAmount = 0.45f;
        public Texture2D HoverCursor;
        public Vector2 HoverCursorHotspot = new(16f, 16f);
        [Tooltip("Hover label offset above the object's top, in pixels.")]
        public float LabelOffsetPixels = 24f;
    }
}
