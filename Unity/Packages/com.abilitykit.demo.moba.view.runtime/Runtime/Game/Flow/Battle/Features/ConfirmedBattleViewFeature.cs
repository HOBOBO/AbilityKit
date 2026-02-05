using System;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Flow.Battle.View;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Game.Flow.Battle.ViewEvents.Snapshot;
using AbilityKit.Game.Flow.Battle.ViewEvents.Triggering;
using UnityEngine;
using EC = AbilityKit.Ability.EC;

namespace AbilityKit.Game.Flow
{
    public sealed class ConfirmedBattleViewFeature : IGamePhaseFeature
    {
        private readonly BattleContext _confirmedCtx;

        private IBattleEntityQuery _query;
        private BattleViewBinder _binder;
        private BattleVfxManager _vfx;
        private EC.Entity _vfxNode;

        private BattleFloatingTextSystem _floatingTexts;
        private BattleAreaViewSystem _areaViews;

        private IBattleViewEventSink _eventSink;

        private BattleSnapshotViewAdapter _snapshotAdapter;

        private BattleTriggerEventViewAdapter _triggerAdapter;

        public ConfirmedBattleViewFeature(BattleContext confirmedCtx)
        {
            _confirmedCtx = confirmedCtx;
        }

        public void OnAttach(in GamePhaseContext ctx)
        {
            _query = _confirmedCtx != null ? _confirmedCtx.EntityQuery : null;

            if (BattleViewFactory.VfxDb == null) BattleViewFactory.VfxDb = VfxDatabase.LoadFromResources("vfx/vfx");
            _vfx = new BattleVfxManager(BattleViewFactory.VfxDb);

            if (_confirmedCtx != null && _confirmedCtx.EntityNode.IsValid)
            {
                _vfxNode = _confirmedCtx.EntityNode.AddChild("BattleVfx__confirmed");
            }

            _binder = new BattleViewBinder(_vfx, _vfxNode);
            _floatingTexts = new BattleFloatingTextSystem();
            _areaViews = new BattleAreaViewSystem();

            _eventSink = new BattleViewEventSink(
                _confirmedCtx,
                _query,
                _binder,
                _vfx,
                _vfxNode,
                _floatingTexts,
                _areaViews);

            var mode = _confirmedCtx != null ? _confirmedCtx.Plan.ViewEventSourceMode : BattleViewEventSourceMode.SnapshotOnly;

            if ((mode == BattleViewEventSourceMode.TriggerOnly || mode == BattleViewEventSourceMode.Hybrid) && _confirmedCtx?.Session != null)
            {
                _triggerAdapter = new BattleTriggerEventViewAdapter(_confirmedCtx.Session, _eventSink);
            }

            if (_confirmedCtx?.EntityWorld != null)
            {
                _confirmedCtx.EntityWorld.EntityDestroyed += OnEntityDestroyed;
            }

            if ((mode == BattleViewEventSourceMode.SnapshotOnly || mode == BattleViewEventSourceMode.Hybrid) && _confirmedCtx?.FrameSnapshots != null)
            {
                _snapshotAdapter = new BattleSnapshotViewAdapter(_confirmedCtx.FrameSnapshots, _eventSink);
            }
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            _snapshotAdapter?.Dispose();
            _snapshotAdapter = null;

            if (_confirmedCtx?.EntityWorld != null)
            {
                _confirmedCtx.EntityWorld.EntityDestroyed -= OnEntityDestroyed;
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

            _query = null;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            if (_confirmedCtx?.EntityWorld == null) return;
            RefreshDirtyViews();
            if (_vfxNode.IsValid) _vfx?.Tick(_vfxNode);
            _floatingTexts?.Tick(deltaTime);
        }

        public void RebindAll()
        {
            if (_confirmedCtx?.EntityWorld == null) return;
            _binder?.RebindAll(_confirmedCtx.EntityWorld);
        }

        private void RefreshDirtyViews()
        {
            if (_query?.World == null) return;

            var dirty = _confirmedCtx != null ? _confirmedCtx.DirtyEntities : null;
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
            _confirmedCtx?.EntityLookup?.UnbindByEntityId(id);
            _binder?.OnDestroyed(id);
        }
    }
}
