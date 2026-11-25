namespace RpgGame.Occlusion
{
    /// <summary>
    /// Counts trigger enters/exits so "is anyone inside" stays correct when a body has several
    /// colliders, or an exit arrives without a matching enter (e.g. after a teleport).
    /// </summary>
    public sealed class OccupancyCounter
    {
        public int Count { get; private set; }
        public bool IsOccupied => Count > 0;

        public void Enter() => Count++;

        public void Exit()
        {
            if (Count > 0) Count--;
        }

        public void Reset() => Count = 0;
    }
}
