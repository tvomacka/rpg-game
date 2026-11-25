namespace RpgGame.Movement
{
    /// <summary>When does a NavMeshAgent count as "there"? Kept pure so it is testable.</summary>
    public static class Arrival
    {
        public static bool HasArrived(bool pathPending, float remainingDistance, float stoppingDistance, float tolerance = 0.05f)
        {
            if (pathPending) return false;
            return remainingDistance <= stoppingDistance + tolerance;
        }
    }
}
