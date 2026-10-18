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

            if (!entity.hasActorDespawnRequest)
            {
                entity.AddActorDespawnRequest(frame, frame, reason, sourceActorId, sourceContextId);
                return true;
            }

            var existing = entity.actorDespawnRequest;
            var existingPriority = GetReasonPriority(existing.Reason);
            var incomingPriority = GetReasonPriority(reason);
            var selectedReason = incomingPriority > existingPriority ? reason : existing.Reason;
            var selectedSourceActorId = existing.SourceActorId;
            var selectedSourceContextId = existing.SourceContextId;

            // Each source field is independently monotonic: a later request may
            // complete missing provenance without replacing an existing value.
            if (selectedSourceActorId == 0 && sourceActorId != 0)
                selectedSourceActorId = sourceActorId;
            if (selectedSourceContextId == 0L && sourceContextId != 0L)
                selectedSourceContextId = sourceContextId;

            entity.ReplaceActorDespawnRequest(
                existing.RequestFrame <= frame ? existing.RequestFrame : frame,
                existing.MinConfirmedFrame >= frame ? existing.MinConfirmedFrame : frame,
                selectedReason,
                selectedSourceActorId,
                selectedSourceContextId);
            return true;
        }

        private static int GetReasonPriority(ActorDespawnReason reason)
        {
            switch (reason)
            {
                case ActorDespawnReason.RollbackCleanup: return 100;
                case ActorDespawnReason.SceneCleanup: return 90;
                case ActorDespawnReason.HeroReplaced: return 80;
                case ActorDespawnReason.SummonReplacedByLimit: return 70;
                case ActorDespawnReason.SummonKilled: return 60;
                case ActorDespawnReason.SummonOwnerDead: return 50;
                case ActorDespawnReason.SummonTimeout: return 40;
                case ActorDespawnReason.SummonManualRemove: return 35;
                case ActorDespawnReason.ProjectileHitOrExit: return 30;
                case ActorDespawnReason.ProjectileExpired: return 20;
                case ActorDespawnReason.ProjectileLauncherCompleted: return 10;
                case ActorDespawnReason.Unknown:
                default: return 0;
            }
        }
    }
}
