using System;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Flow.Battle.View;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Game.Flow.Battle.ViewEvents.Snapshot;
using AbilityKit.Game.Flow.Battle.ViewEvents.Triggering;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;
using AbilityKit.Ability.World.Abstractions;
using UnityEngine;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    public sealed partial class ConfirmedBattleViewFeature : IGamePhaseFeature, IViewFeatureModulesHost
    {
        private readonly BattleContext _confirmedCtx;

        private IBattleEntityQuery _query;
        private BattleViewBinder _binder;
        private BattleVfxManager _vfx;
        private EC.IEntity _vfxNode;

        private ViewTimeline _timeline;

        private readonly System.Collections.Generic.List<IViewSubFeature<ConfirmedBattleViewFeature>> _subFeatures = new System.Collections.Generic.List<IViewSubFeature<ConfirmedBattleViewFeature>>(8);
        private ModuleHost<FeatureModuleContext<ConfirmedBattleViewFeature>, IViewSubFeature<ConfirmedBattleViewFeature>> _moduleHost;

        private BattleFloatingTextSystem _floatingTexts;
        private BattleAreaViewSystem _areaViews;

        private IBattleViewEventSink _eventSink;

        private BattleSnapshotViewAdapter _snapshotAdapter;

        private BattleTriggerEventViewAdapter _triggerAdapter;

        private BattleEventBus _events;

        public ConfirmedBattleViewFeature(BattleContext confirmedCtx)
        {
            _confirmedCtx = confirmedCtx;
            _query = confirmedCtx?.EntityQuery;
        }
    }
}
