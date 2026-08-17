using System;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Hierarchy;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Flow.Battle.View;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Game.Flow.Battle.ViewEvents.Snapshot;
using AbilityKit.Game.Flow.Battle.ViewEvents.Triggering;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    internal interface IViewFeatureRuntime : IViewSharedSubFeatureHost
    {
        BattleContext Context { get; }
        IBattleEntityQuery Query { get; set; }
        new BattleViewBinder Binder { get; set; }
        BattleViewResourceProvider Resources { get; }
        BattleViewShellPool ShellPool { get; }
        BattleProjectileShellPool ProjectileShellPool { get; set; }
        BattleViewCameraController CameraController { get; }
        BattleAreaVfxPool AreaVfxPool { get; }
        BattleVfxManager Vfx { get; set; }
        EC.IEntity VfxNode { get; set; }
        long ContextVfxBindingGeneration { get; set; }
        ViewTimeline Timeline { get; set; }
        BattleFloatingTextSystem FloatingTexts { get; set; }
        BattleAreaViewSystem AreaViews { get; set; }
        IBattleViewEventSink EventSink { get; set; }
        BattleSnapshotViewAdapter SnapshotAdapter { get; set; }
        BattleTriggerEventViewAdapter TriggerAdapter { get; set; }
        IDisposable EntityDestroyedSubscription { get; set; }
        int LastAlignedFrame { get; set; }

        /// <summary>
        /// Hierarchy manager that organizes pooled and active GameObjects into
        /// the categorized <c>[Battle]</c> hierarchy. May be null when the
        /// feature is configured without hierarchy support.
        /// </summary>
        BattleViewHierarchyManager Hierarchy { get; set; }

        void OnEntityDestroyed(EC.EntityDestroyed evt);
    }
}
