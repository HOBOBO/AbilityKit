using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Diagnostics;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(IBattleDiagnosticActorEffectStore), WorldLifetime.Scoped)]
    [WorldService(typeof(IBattleDiagnosticActorEffectReadStore), WorldLifetime.Scoped)]
    [WorldService(typeof(IBattleDiagnosticEffectSnapshotSource), WorldLifetime.Scoped)]
    public sealed class MobaBattleDiagnosticActorEffectStore :
        IBattleDiagnosticActorEffectStore,
        IBattleDiagnosticEffectSnapshotSource,
        IService
    {
        private readonly BattleDiagnosticActorEffectStore _store;

        public MobaBattleDiagnosticActorEffectStore(MobaBattleDiagnosticEventCollector collector)
        {
            if (collector == null) throw new ArgumentNullException(nameof(collector));
            _store = new BattleDiagnosticActorEffectStore(collector.Scope);
        }

        public BattleDiagnosticSessionScope Scope => _store.Scope;
        public long Revision => _store.Revision;
        public int SnapshotFrame => _store.SnapshotFrame;
        public bool IsFrozen => _store.IsFrozen;

        public bool TryReplaceSnapshot(
            int frame,
            System.Collections.Generic.IReadOnlyList<long> actorIds,
            System.Collections.Generic.IReadOnlyList<BattleDiagnosticActorEffect> effects)
        {
            return _store.TryReplaceSnapshot(frame, actorIds, effects);
        }

        public BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorEffect> CaptureEffectSnapshot()
        {
            return _store.CaptureEffectSnapshot();
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticActorEffect> QueryActorEffects(
            long requestId,
            int frame,
            long actorId)
        {
            return _store.QueryActorEffects(requestId, frame, actorId);
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
