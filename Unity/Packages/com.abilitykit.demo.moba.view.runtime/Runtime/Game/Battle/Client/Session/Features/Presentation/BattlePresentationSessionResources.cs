using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Owns the confirmed presentation resources for one battle session.
    /// World-bound event routing remains owned by the simulation runtime.
    /// </summary>
    internal sealed class BattlePresentationSessionResources
    {
        private BattleContext _confirmedContext;
        private ConfirmedViewSnapshotRuntime _confirmedSnapshotRuntime;
        private ConfirmedBattleViewFeature _confirmedFeature;

        internal BattleContext ConfirmedContext => _confirmedContext;
        internal ConfirmedBattleViewFeature ConfirmedFeature => _confirmedFeature;
        internal FrameSnapshotDispatcher ConfirmedSnapshots =>
            _confirmedSnapshotRuntime?.Snapshots;

        internal void EnsureConfirmedViewInstalled(
            BattleContext sourceContext,
            GameFlowDomain flow,
            WorldId authWorldId,
            bool enabled,
            Action<IEntity> destroyEntityTree)
        {
            if (!enabled || flow == null || _confirmedFeature != null) return;

            var runtime = ConfirmedViewSideRuntimeFactory.Create(
                sourceContext,
                authWorldId,
                destroyEntityTree);

            _confirmedContext = runtime.Context;
            _confirmedSnapshotRuntime = runtime.SnapshotRuntime;
            _confirmedFeature = runtime.Feature;

            flow.Attach(runtime.Feature);
        }

        internal void DisposeConfirmedView(
            GameFlowDomain flow,
            Action<IEntity> destroyEntityTree)
        {
            SessionSimRuntimeDisposer.ExecuteCleanupSteps(
                "Failed to dispose confirmed presentation resources.",
                () => DetachConfirmedFeature(flow),
                DisposeConfirmedSnapshotRuntime,
                () => DisposeConfirmedContext(destroyEntityTree));
        }

        private void DetachConfirmedFeature(GameFlowDomain flow)
        {
            var feature = _confirmedFeature;
            if (flow != null && feature != null) flow.Detach(feature);
            if (ReferenceEquals(_confirmedFeature, feature)) _confirmedFeature = null;
        }

        private void DisposeConfirmedSnapshotRuntime()
        {
            var runtime = _confirmedSnapshotRuntime;
            runtime?.Dispose();
            if (ReferenceEquals(_confirmedSnapshotRuntime, runtime))
                _confirmedSnapshotRuntime = null;
        }

        private void DisposeConfirmedContext(Action<IEntity> destroyEntityTree)
        {
            var context = _confirmedContext;
            ConfirmedViewContextDisposer.Dispose(context, destroyEntityTree);
            if (ReferenceEquals(_confirmedContext, context)) _confirmedContext = null;
        }
    }
}
