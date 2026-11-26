namespace RpgGame.Characters
{
    /// <summary>
    /// Plain C# rules for the character Animator: the parameter names the Animator Controller uses
    /// and how agent speed maps to them. Kept engine-free so it is EditMode-testable.
    /// </summary>
    public static class LocomotionAnimation
    {
        /// <summary>Float parameter: current ground speed in m/s. Idle &lt;-&gt; Walk switch on it.</summary>
        public const string SpeedParameter = "Speed";

        /// <summary>Float parameter: playback multiplier for the Walk state, so feet keep up with the agent.</summary>
        public const string WalkRateParameter = "WalkRate";

        public const string IdleState = "Idle";
        public const string WalkState = "Walk";

        /// <summary>Above this speed (m/s) the Animator is in Walk; below it, Idle.</summary>
        public const float WalkThreshold = 0.1f;

        public const float MinWalkRate = 0.5f;
        public const float MaxWalkRate = 2.5f;

        /// <summary>
        /// Walk clip playback rate for a given agent speed. <paramref name="clipSpeed"/> is the ground speed
        /// (m/s) the walk clip looks right at, at the character's scale.
        /// </summary>
        public static float WalkRate(float agentSpeed, float clipSpeed)
        {
            if (clipSpeed <= 0f || agentSpeed <= WalkThreshold) return 1f;
            float rate = agentSpeed / clipSpeed;
            return rate < MinWalkRate ? MinWalkRate : rate > MaxWalkRate ? MaxWalkRate : rate;
        }
    }
}
