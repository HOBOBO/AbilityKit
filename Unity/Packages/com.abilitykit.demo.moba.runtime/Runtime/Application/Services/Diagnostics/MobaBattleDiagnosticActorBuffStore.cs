using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Diagnostics;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(IBattleDiagnosticActorBuffStore), WorldLifetime.Scoped)]
    [WorldService(typeof(IBattleDiagnosticActorBuffReadStore), WorldLifetime.Scoped)]
    [WorldService(typeof(IBattleDiagnosticBuffSnapshotSource), WorldLifetime.Scoped)]
    public sealed class MobaBattleDiagnosticActorBuffStore :
        IBattleDiagnosticActorBuffStore,
        IBattleDiagnosticBuffSnapshotSource,
        IService
    {
        private readonly BattleDiagnosticActorBuffStore _store;

        public MobaBattleDiagnosticActorBuffStore(MobaBattleDiagnosticEventCollector collector)
        {
            if (collector == null) throw new ArgumentNullException(nameof(collector));
            _store = new BattleDiagnosticActorBuffStore(collector.Scope);
        }

        public BattleDiagnosticSessionScope Scope => _store.Scope;
        public long Revision => _store.Revision;
        public int SnapshotFrame => _store.SnapshotFrame;
        public bool IsFrozen => _store.IsFrozen;

        public bool TryReplaceSnapshot(
            int frame,
            System.Collections.Generic.IReadOnlyList<long> actorIds,
            System.Collections.Generic.IReadOnlyList<BattleDiagnosticActorBuff> buffs)
        {
            return _store.TryReplaceSnapshot(frame, actorIds, buffs);
        }

        public BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorBuff> CaptureBuffSnapshot()
        {
            return _store.CaptureBuffSnapshot();
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticActorBuff> QueryActorBuffs(
            long requestId,
            int frame,
            long actorId)
        {
            return _store.QueryActorBuffs(requestId, frame, actorId);
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
