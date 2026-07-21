using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Demo.Moba.Components
{
    public enum ActorDespawnReason
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

    [Actor]
    public sealed class ActorDespawnRequestComponent : IComponent
    {
        public int RequestFrame;
        public int MinConfirmedFrame;
        public ActorDespawnReason Reason;
        public int SourceActorId;
        public long SourceContextId;
    }
    public static class ActorLifecycleRequests
    {
        public static bool RequestDespawn(global::ActorEntity entity, int frame, ActorDespawnReason reason, int sourceActorId = 0, long sourceContextId = 0L)
        {
            if (entity == null) return false;

            if (entity.hasActorDespawnRequest)
            {
                entity.ReplaceActorDespawnRequest(frame, frame, reason, sourceActorId, sourceContextId);
            }
            else
            {
                entity.AddActorDespawnRequest(frame, frame, reason, sourceActorId, sourceContextId);
            }

            return true;
        }
    }
}
