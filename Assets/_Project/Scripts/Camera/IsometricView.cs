using UnityEngine;

namespace RpgGame.CameraControl
{
    /// <summary>Fixed isometric framing: pitch (looking down) and yaw around the vertical axis.</summary>
    public static class IsometricView
    {
        public const float DefaultPitch = 30f;
        public const float DefaultYaw = 45f;

        public static Quaternion Rotation(float pitchDegrees, float yawDegrees) =>
            Quaternion.Euler(pitchDegrees, yawDegrees, 0f);

        /// <summary>
        /// World-space offset from the followed target to the camera, so that a camera with
        /// <see cref="Rotation"/> placed at target + offset looks straight at the target.
        /// </summary>
        public static Vector3 FollowOffset(float pitchDegrees, float yawDegrees, float distance) =>
            Rotation(pitchDegrees, yawDegrees) * Vector3.back * distance;
    }
}
