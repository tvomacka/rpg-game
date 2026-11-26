using UnityEngine;

namespace RpgGame.Interaction
{
    /// <summary>Small pure rules used by the interaction MonoBehaviours.</summary>
    public static class InteractionMath
    {
        public static float FlatDistance(Vector3 a, Vector3 b) =>
            Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        /// <summary>Rotation that faces <paramref name="to"/> from <paramref name="from"/>, turning about the vertical only.</summary>
        public static bool TryFlatLookRotation(Vector3 from, Vector3 to, out Quaternion rotation)
        {
            Vector3 dir = to - from;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f)
            {
                rotation = Quaternion.identity;
                return false;
            }

            rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            return true;
        }

        /// <summary>Has the player walked far enough from where a popup opened that it should close?</summary>
        public static bool HasWalkedAway(Vector3 popupOrigin, Vector3 playerPosition, float walkAwayDistance) =>
            FlatDistance(popupOrigin, playerPosition) > walkAwayDistance;

        public static string TalkPlaceholder(string format, string displayName) =>
            string.IsNullOrEmpty(format) ? displayName : string.Format(format, displayName);
    }
}
