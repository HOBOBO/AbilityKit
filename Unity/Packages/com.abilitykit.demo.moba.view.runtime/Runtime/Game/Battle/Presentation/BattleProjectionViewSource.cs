using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Network.Battle.Projection;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Game.Flow
{
    public interface IBattleProjectionViewSource
    {
        WorldId WorldId { get; }

        // Return false while the source world is unavailable. The view stays registered.
        bool TryGetProjection(out IActorProjectionProducer producer, out int frame);
    }

    public interface IBattleProjectionViewEpochSource
    {
        // Increment when the source world is rebuilt, even if its producer is reused.
        long ProjectionEpoch { get; }
    }

    public interface IBattleProjectionViewEventSource
    {
        // Map positional events into the instance's world offset before forwarding.
        // The returned subscription must stop forwarding after disposal.
        IDisposable SubscribeEvents(IBattleViewEventSink sink,
            BattleProjectionViewEventContext context);
    }

    public readonly struct BattleProjectionViewEventContext
    {
        public readonly string InstanceId;
        public readonly WorldId SourceWorldId;
        public readonly Vec3 WorldOffset;
        public readonly long ProjectionEpoch;
        public readonly int CurrentFrame;

        public BattleProjectionViewEventContext(string instanceId, WorldId sourceWorldId,
            Vec3 worldOffset, long projectionEpoch, int currentFrame)
        {
            InstanceId = instanceId;
            SourceWorldId = sourceWorldId;
            WorldOffset = worldOffset;
            ProjectionEpoch = projectionEpoch;
            CurrentFrame = currentFrame;
        }
    }

    public readonly struct BattleProjectionViewInfo
    {
        public readonly string InstanceId;
        public readonly BattleProjectionViewRole Role;
        public readonly WorldId SourceWorldId;
        public readonly Vec3 WorldOffset;
        public readonly BattleProjectionViewCapabilities Capabilities;

        public BattleProjectionViewInfo(string instanceId, BattleProjectionViewRole role,
            WorldId sourceWorldId, Vec3 worldOffset,
            BattleProjectionViewCapabilities capabilities = BattleProjectionViewCapabilities.Actors)
        {
            InstanceId = instanceId;
            Role = role;
            SourceWorldId = sourceWorldId;
            WorldOffset = worldOffset;
            Capabilities = capabilities;
        }
    }
}
