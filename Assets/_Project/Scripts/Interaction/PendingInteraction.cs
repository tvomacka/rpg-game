using UnityEngine;

namespace RpgGame.Interaction
{
    public enum PendingOutcome
    {
        /// <summary>Nothing pending.</summary>
        None,
        /// <summary>Still walking.</summary>
        Walking,
        /// <summary>Reached the destination: run the action. The pending interaction is cleared.</summary>
        Arrived,
        /// <summary>Stopped short (unreachable / partial path). The pending interaction is cleared.</summary>
        Failed,
    }

    /// <summary>
    /// "Walk there, then do it." At most one interaction is pending: a new request replaces the old one,
    /// and a click elsewhere cancels it. Plain C# so the rules are EditMode-testable.
    /// </summary>
    public sealed class PendingInteraction<T> where T : class
    {
        private readonly float _arrivalTolerance;

        public PendingInteraction(float arrivalTolerance) => _arrivalTolerance = arrivalTolerance;

        public T Target { get; private set; }
        public Vector3 Destination { get; private set; }
        public bool HasPending => Target != null;

        public void Request(T target, Vector3 destination)
        {
            Target = target;
            Destination = destination;
        }

        public void Cancel()
        {
            Target = null;
            Destination = default;
        }

        /// <param name="agentStopped">The walker has finished its path (arrived or gave up).</param>
        /// <param name="distanceToDestination">Flat distance from the walker to <see cref="Destination"/>.</param>
        /// <param name="completed">The interaction that just arrived or failed, otherwise null.</param>
        public PendingOutcome Tick(bool agentStopped, float distanceToDestination, out T completed)
        {
            completed = null;
            if (!HasPending) return PendingOutcome.None;

            if (!agentStopped) return PendingOutcome.Walking;

            bool close = distanceToDestination <= _arrivalTolerance;
            completed = Target;
            Cancel();
            return close ? PendingOutcome.Arrived : PendingOutcome.Failed;
        }
    }
}
