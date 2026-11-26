namespace RpgGame.Characters
{
    /// <summary>
    /// Plain C# rules for the character Animator: the parameter names the Animator Controller uses
    /// and how agent speed maps to them. Kept engine-free so it is EditMode-testable.
    /// </summary>
    public static class LocomotionAnimation
    {
        /// <summary>Float parameter: current ground speed in m/s. Idle &lt;-&gt; Run switch on it.</summary>
        public const string SpeedParameter = "Speed";

        /// <summary>Float parameter: playback multiplier for the Run state, so feet keep up with the agent.</summary>
        public const string RunRateParameter = "RunRate";

        public const string IdleState = "Idle";

        /// <summary>The moving state. It plays the pack's run clip (owner decision, #11: 3.5 m/s is a run, not a walk).</summary>
        public const string RunState = "Run";

        /// <summary>Above this speed (m/s) the Animator is in Run; below it, Idle.</summary>
        public const float MoveThreshold = 0.1f;

        public const float MinRunRate = 0.5f;
        public const float MaxRunRate = 2.5f;

        /// <summary>
        /// Run clip playback rate for a given agent speed. <paramref name="clipSpeed"/> is the ground speed
        /// (m/s) the run clip's feet move at, at the character's scale: at that speed the rate is 1 and the feet don't slide.
        /// </summary>
        public static float RunRate(float agentSpeed, float clipSpeed)
        {
            if (clipSpeed <= 0f || agentSpeed <= MoveThreshold) return 1f;
            float rate = agentSpeed / clipSpeed;
            return rate < MinRunRate ? MinRunRate : rate > MaxRunRate ? MaxRunRate : rate;
        }
    }
}
