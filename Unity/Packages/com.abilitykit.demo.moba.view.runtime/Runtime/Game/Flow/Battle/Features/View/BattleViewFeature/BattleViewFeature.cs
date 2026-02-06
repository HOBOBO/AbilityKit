using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Flow.Battle.View;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Game.Flow.Battle.ViewEvents.Snapshot;
using AbilityKit.Game.Flow.Battle.ViewEvents.Triggering;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;
using AbilityKit.Ability.World.Abstractions;
using UnityEngine;
using EC = AbilityKit.Ability.EC;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleViewFeature : IGamePhaseFeature, IViewFeatureModulesHost
    {
        private BattleContext _ctx;
        private IBattleEntityQuery _query;
        private BattleViewBinder _binder;
        private BattleVfxManager _vfx;
        private EC.Entity _vfxNode;

        private ViewTimeline _timeline;

        private readonly List<IViewSubFeature<BattleViewFeature>> _subFeatures = new List<IViewSubFeature<BattleViewFeature>>(8);
        private ModuleHost<FeatureModuleContext<BattleViewFeature>, IViewSubFeature<BattleViewFeature>> _moduleHost;

        private BattleFloatingTextSystem _floatingTexts;
        private BattleAreaViewSystem _areaViews;

        private IBattleViewEventSink _eventSink;

        private BattleSnapshotViewAdapter _snapshotAdapter;

        private BattleTriggerEventViewAdapter _triggerAdapter;

        private BattleEventBus _events;
    }
}
