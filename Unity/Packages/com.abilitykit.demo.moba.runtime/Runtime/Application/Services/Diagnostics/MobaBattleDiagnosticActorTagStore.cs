using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Diagnostics;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(IBattleDiagnosticActorTagStore), WorldLifetime.Scoped)]
    [WorldService(typeof(IBattleDiagnosticActorTagReadStore), WorldLifetime.Scoped)]
    [WorldService(typeof(IBattleDiagnosticTagSnapshotSource), WorldLifetime.Scoped)]
    public sealed class MobaBattleDiagnosticActorTagStore :
        IBattleDiagnosticActorTagStore,
        IBattleDiagnosticTagSnapshotSource,
        IService
    {
        private readonly BattleDiagnosticActorTagStore _store;

        public MobaBattleDiagnosticActorTagStore(MobaBattleDiagnosticEventCollector collector)
        {
            if (collector == null) throw new ArgumentNullException(nameof(collector));
            _store = new BattleDiagnosticActorTagStore(collector.Scope);
        }

        public BattleDiagnosticSessionScope Scope => _store.Scope;
        public long Revision => _store.Revision;
        public int SnapshotFrame => _store.SnapshotFrame;
        public bool IsFrozen => _store.IsFrozen;

        public bool TryReplaceSnapshot(
            int frame,
            System.Collections.Generic.IReadOnlyList<long> actorIds,
            System.Collections.Generic.IReadOnlyList<BattleDiagnosticActorTag> tags)
        {
            return _store.TryReplaceSnapshot(frame, actorIds, tags);
        }

        public BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorTag> CaptureTagSnapshot()
        {
            return _store.CaptureTagSnapshot();
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticActorTag> QueryActorTags(
            long requestId,
            int frame,
            long actorId)
        {
            return _store.QueryActorTags(requestId, frame, actorId);
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
