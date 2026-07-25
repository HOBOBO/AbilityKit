using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Diagnostics;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(IBattleDiagnosticActorAttributeStore), WorldLifetime.Scoped)]
    [WorldService(typeof(IBattleDiagnosticActorAttributeReadStore), WorldLifetime.Scoped)]
    [WorldService(typeof(IBattleDiagnosticAttributeSnapshotSource), WorldLifetime.Scoped)]
    public sealed class MobaBattleDiagnosticActorAttributeStore :
        IBattleDiagnosticActorAttributeStore,
        IBattleDiagnosticAttributeSnapshotSource,
        IService
    {
        private readonly BattleDiagnosticActorAttributeStore _store;

        public MobaBattleDiagnosticActorAttributeStore(
            MobaBattleDiagnosticEventCollector collector)
        {
            if (collector == null) throw new ArgumentNullException(nameof(collector));
            _store = new BattleDiagnosticActorAttributeStore(collector.Scope);
        }

        public BattleDiagnosticSessionScope Scope => _store.Scope;
        public long Revision => _store.Revision;
        public int SnapshotFrame => _store.SnapshotFrame;
        public bool IsFrozen => _store.IsFrozen;

        public bool TryReplaceSnapshot(
            int frame,
            System.Collections.Generic.IReadOnlyList<long> actorIds,
            System.Collections.Generic.IReadOnlyList<BattleDiagnosticActorAttribute> attributes,
            System.Collections.Generic.IReadOnlyList<BattleDiagnosticActorAttributeModifier> modifiers)
        {
            return _store.TryReplaceSnapshot(frame, actorIds, attributes, modifiers);
        }

        public BattleDiagnosticAttributeTrackSnapshot CaptureAttributeSnapshot()
        {
            return _store.CaptureAttributeSnapshot();
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticActorAttribute> QueryActorAttributes(
            long requestId,
            int frame,
            long actorId)
        {
            return _store.QueryActorAttributes(requestId, frame, actorId);
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticActorAttributeModifier> QueryActorAttributeModifiers(
            long requestId,
            int frame,
            long actorId)
        {
            return _store.QueryActorAttributeModifiers(requestId, frame, actorId);
        }

        public void SetFrozen(bool frozen)
        {
            _store.SetFrozen(frozen);
        }

        public void Clear()
        {
            _store.Clear();
        }

        public void Dispose()
        {
        }
    }
}
