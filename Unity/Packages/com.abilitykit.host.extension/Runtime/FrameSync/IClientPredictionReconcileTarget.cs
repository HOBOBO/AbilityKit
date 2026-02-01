using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host.Extensions.FrameSync
{
    public interface IClientPredictionReconcileTarget
    {
        void OnAuthoritativeStateHash(WorldId worldId, FrameIndex frame, WorldStateHash hash);
    }

    public interface IClientPredictionReconcileControl
    {
        void ResetReconcile(WorldId worldId);

        void SetReconcileEnabled(WorldId worldId, bool enabled);

        bool TryGetReconcileEnabled(WorldId worldId, out bool enabled);
    }
}
