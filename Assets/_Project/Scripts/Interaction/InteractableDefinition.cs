using UnityEngine;

namespace RpgGame.Interaction
{
    public enum InteractionKind
    {
        Inspect,
        Talk,
    }

    /// <summary>
    /// What an interactable is and what clicking it does. One asset per object, so later milestones
    /// can attach clues (M3) and dialogue (M4) to the same asset.
    /// </summary>
    [CreateAssetMenu(menuName = "RpgGame/Interactable", fileName = "Interactable")]
    public sealed class InteractableDefinition : ScriptableObject
    {
        [Tooltip("Shown in the hover label and as the popup title.")]
        public string DisplayName;

        public InteractionKind Action = InteractionKind.Inspect;

        [Tooltip("Inspect only: the text the popup shows.")]
        [TextArea(2, 6)] public string InspectText;
    }
}
