using System;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Game.Flow.Battle.ViewEvents.Snapshot;
using AbilityKit.Game.Flow.Battle.ViewEvents.Triggering;
using AbilityKit.Game.Flow.Snapshot;
using UnityEngine;
using EC = AbilityKit.Ability.EC;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleViewFeature : IGamePhaseFeature
    {
        private BattleContext _ctx;
        private IBattleEntityQuery _query;
        private BattleViewBinder _binder;
        private BattleVfxManager _vfx;
        private EC.Entity _vfxNode;

        private BattleFloatingTextSystem _floatingTexts;
        private BattleAreaViewSystem _areaViews;

        private IBattleViewEventSink _eventSink;

        private BattleSnapshotViewAdapter _snapshotAdapter;

        private BattleTriggerEventViewAdapter _triggerAdapter;

        public void OnAttach(in GamePhaseContext ctx)
        {
            ctx.Root.TryGetComponent(out _ctx);
            _query = _ctx != null ? _ctx.EntityQuery : null;

            if (BattleViewFactory.VfxDb == null) BattleViewFactory.VfxDb = VfxDatabase.LoadFromResources("vfx/vfx");
            _vfx = new BattleVfxManager(BattleViewFactory.VfxDb);

            if (_ctx != null && _ctx.EntityNode.IsValid)
            {
                _vfxNode = _ctx.EntityNode.AddChild("BattleVfx");
            }

            _binder = new BattleViewBinder(_vfx, _vfxNode);
            _floatingTexts = new BattleFloatingTextSystem();
            _areaViews = new BattleAreaViewSystem();

            _eventSink = new BattleViewEventSink(
                _ctx,
                _query,
                _binder,
                _vfx,
                _vfxNode,
                _floatingTexts,
                _areaViews);

            var mode = _ctx != null ? _ctx.Plan.ViewEventSourceMode : BattleViewEventSourceMode.SnapshotOnly;

            if ((mode == BattleViewEventSourceMode.TriggerOnly || mode == BattleViewEventSourceMode.Hybrid) && _ctx?.Session != null)
            {
                _triggerAdapter = new BattleTriggerEventViewAdapter(_ctx.Session, _eventSink);
            }

            if (_ctx?.EntityWorld != null)
            {
                _ctx.EntityWorld.EntityDestroyed += OnEntityDestroyed;
            }

            if ((mode == BattleViewEventSourceMode.SnapshotOnly || mode == BattleViewEventSourceMode.Hybrid) && _ctx?.FrameSnapshots != null)
            {
                _snapshotAdapter = new BattleSnapshotViewAdapter(_ctx.FrameSnapshots, _eventSink);
            }
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            _snapshotAdapter?.Dispose();
            _snapshotAdapter = null;

            if (_ctx?.EntityWorld != null)
            {
                _ctx.EntityWorld.EntityDestroyed -= OnEntityDestroyed;
            }

            _floatingTexts?.Clear();
            _areaViews?.Clear();

            _binder?.Clear();
            _binder = null;
            _floatingTexts = null;
            _areaViews = null;
            _vfx = null;
            _vfxNode = default;
            _eventSink = null;

            _triggerAdapter?.Dispose();
            _triggerAdapter = null;

            _ctx = null;
            _query = null;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            if (_ctx?.EntityWorld == null) return;
            if (_vfxNode.IsValid) _vfx?.Tick(_vfxNode);
            _floatingTexts?.Tick(deltaTime);
        }

        private void RefreshDirtyViews()
        {
            if (_query?.World == null) return;

            var dirty = _ctx != null ? _ctx.DirtyEntities : null;
            if (dirty == null || dirty.Count == 0) return;

            for (int i = 0; i < dirty.Count; i++)
            {
                var id = dirty[i];
                if (!_query.World.IsAlive(id)) continue;
                _binder?.Sync(_query.World.Wrap(id));
            }

            dirty.Clear();
        }

        private void OnEntityDestroyed(EC.EntityId id)
        {
            _ctx?.EntityLookup?.UnbindByEntityId(id);
            _binder?.OnDestroyed(id);
        }
    }
}
