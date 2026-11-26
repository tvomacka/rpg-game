namespace RpgGame.Interaction
{
    public readonly struct HoverChange<T> where T : class
    {
        public readonly T Previous;
        public readonly T Current;

        public HoverChange(T previous, T current)
        {
            Previous = previous;
            Current = current;
        }

        public bool Changed => !ReferenceEquals(Previous, Current);
    }

    /// <summary>Which object is under the pointer, and when that changes. Plain C# so it is testable.</summary>
    public sealed class HoverTracker<T> where T : class
    {
        public T Current { get; private set; }

        /// <param name="underPointer">The object under the pointer this frame, or null.</param>
        public HoverChange<T> Set(T underPointer)
        {
            var change = new HoverChange<T>(Current, underPointer);
            Current = underPointer;
            return change;
        }

        public HoverChange<T> Clear() => Set(null);
    }
}
