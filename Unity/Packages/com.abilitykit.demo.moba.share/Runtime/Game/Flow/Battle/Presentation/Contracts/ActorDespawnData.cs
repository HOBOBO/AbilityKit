namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// Platform-neutral data required to remove an actor from a presentation runtime.
    /// </summary>
    public readonly struct ActorDespawnData
    {
        public int ActorId { get; }
        public ActorDespawnReason Reason { get; }

        public ActorDespawnData(int actorId, ActorDespawnReason reason = ActorDespawnReason.Unknown)
        {
            ActorId = actorId;
            Reason = reason;
        }
    }

    /// <summary>
    /// Stable presentation-facing reasons carried by the actor despawn protocol.
    /// Unknown numeric values are preserved when mapped from the wire.
    /// </summary>
    public enum ActorDespawnReason : byte
    {
        Unknown = 0,
        ProjectileExpired = 1,
        ProjectileHitOrExit = 2,
        ProjectileLauncherCompleted = 3,
        HeroReplaced = 5,
        SummonTimeout = 10,
        SummonOwnerDead = 11,
        SummonReplacedByLimit = 12,
        SummonManualRemove = 13,
        SummonKilled = 14,
        SceneCleanup = 50,
        RollbackCleanup = 51,
    }
}
