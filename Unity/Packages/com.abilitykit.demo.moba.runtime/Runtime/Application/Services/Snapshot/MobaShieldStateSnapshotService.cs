using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Services
{
    [MobaSnapshotEmitter(86)]
    [WorldService(typeof(MobaShieldStateSnapshotService))]
    public sealed class MobaShieldStateSnapshotService : IService, IMobaSnapshotEmitter
    {
        private readonly MobaLogicWorldRunGateService _phase;
        private readonly MobaActorRegistry _registry;
        private readonly MobaShieldService _shields;
        private readonly MobaSnapshotBuffer<MobaShieldStateSnapshotEntry> _entries =
            new MobaSnapshotBuffer<MobaShieldStateSnapshotEntry>(16, 256);
        private FrameIndex _lastFrame = new FrameIndex(-999999);

        public MobaShieldStateSnapshotService(
            MobaLogicWorldRunGateService phase,
            MobaActorRegistry registry,
            MobaShieldService shields)
        {
            _phase = phase ?? throw new ArgumentNullException(nameof(phase));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _shields = shields ?? throw new ArgumentNullException(nameof(shields));
        }

        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            if (!_phase.InGame || frame.Value == _lastFrame.Value)
            {
                snapshot = default;
                return false;
            }
            _lastFrame = frame;
            _entries.Clear();
            foreach (var actorId in _registry.CopyActorIdsInOrder())
            {
                if (actorId <= 0) continue;
                var state = _shields.GetPresentationSnapshot(actorId);
                if (!state.HasShield) continue;
                _entries.Add(new MobaShieldStateSnapshotEntry
                {
                    ActorId = actorId,
                    CurrentShield = ToFloat(state.TotalVisibleValue),
                    Capacity = ToFloat(state.VisibleCapacity),
                    PersonalShield = ToFloat(state.TotalPersonalValue),
                    SharedShield = ToFloat(state.TotalSharedValue),
                    ActiveLayerCount = state.LayerCount,
                    HasShared = state.HasShared,
                });
            }
            snapshot = new WorldStateSnapshot(
                AbilityKit.Protocol.Moba.MobaOpCodes.Snapshot.ShieldState,
                MobaShieldStateSnapshotCodec.Serialize(_entries.ToArrayClearAndTrim()));
            return true;
        }

        public void Dispose()
        {
            _entries.ClearAndTrim();
            _lastFrame = new FrameIndex(-999999);
        }

        private static float ToFloat(AbilityKit.Deterministic.Fixed64 value) =>
            MobaResourceFixedConvert.ToSingle(value);
    }
}
