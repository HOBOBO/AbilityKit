using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Network.Battle.Projection;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    [Flags]
    public enum BattleProjectionViewCapabilities
    {
        Actors = 0,
        Vfx = 1 << 0,
        AreaEffects = 1 << 1,
        FloatingText = 1 << 2,
        Events = 1 << 3,
        Camera = 1 << 4,
        Full = Vfx | AreaEffects | FloatingText | Events | Camera,
    }

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
        public readonly Vector3 WorldOffset;
        public readonly long ProjectionEpoch;
        public readonly int CurrentFrame;

        public BattleProjectionViewEventContext(string instanceId, WorldId sourceWorldId,
            Vector3 worldOffset, long projectionEpoch, int currentFrame)
        {
            InstanceId = instanceId;
            SourceWorldId = sourceWorldId;
            WorldOffset = worldOffset;
            ProjectionEpoch = projectionEpoch;
            CurrentFrame = currentFrame;
        }
    }

    public enum BattleProjectionViewRole
    {
        Prediction,
        Auxiliary,
    }

    public readonly struct BattleProjectionViewInfo
    {
        public readonly string InstanceId;
        public readonly BattleProjectionViewRole Role;
        public readonly WorldId SourceWorldId;
        public readonly Vector3 WorldOffset;
        public readonly BattleProjectionViewCapabilities Capabilities;

        public BattleProjectionViewInfo(string instanceId, BattleProjectionViewRole role,
            WorldId sourceWorldId, Vector3 worldOffset,
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
