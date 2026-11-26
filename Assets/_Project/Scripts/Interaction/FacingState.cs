using UnityEngine;

namespace RpgGame.Interaction
{
    /// <summary>
    /// Which way a character should face, as a yaw (degrees about the vertical): someone it's talking to,
    /// or its own rest facing. Turning is a fixed rate towards the target. Plain C# so it is testable.
    /// </summary>
    public sealed class FacingState
    {
        public FacingState(float restYaw)
        {
            RestYaw = restYaw;
            TargetYaw = restYaw;
        }

        public float RestYaw { get; }
        public float TargetYaw { get; private set; }
        public bool IsFacingSomeone { get; private set; }

        public void Face(float yaw)
        {
            TargetYaw = yaw;
            IsFacingSomeone = true;
        }

        public void ReturnToRest()
        {
            TargetYaw = RestYaw;
            IsFacingSomeone = false;
        }

        /// <summary>The yaw after turning for <paramref name="deltaTime"/>; lands exactly on the target, the short way round.</summary>
        public float Step(float currentYaw, float degreesPerSecond, float deltaTime) =>
            Mathf.MoveTowardsAngle(currentYaw, TargetYaw, degreesPerSecond * deltaTime);

        public bool IsSettled(float currentYaw) => Mathf.DeltaAngle(currentYaw, TargetYaw) == 0f;

        /// <summary>Yaw that looks from <paramref name="from"/> to <paramref name="to"/> on the ground plane.</summary>
        public static bool TryYawTowards(Vector3 from, Vector3 to, out float yaw)
        {
            float dx = to.x - from.x, dz = to.z - from.z;
            if (dx * dx + dz * dz < 1e-6f)
            {
                yaw = 0f;
                return false;
            }

            yaw = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
            return true;
        }
    }
}
