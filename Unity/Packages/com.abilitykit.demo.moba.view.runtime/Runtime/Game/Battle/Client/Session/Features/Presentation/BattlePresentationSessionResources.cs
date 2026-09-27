using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Network.Battle.Projection;
using AbilityKit.World.ECS;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Owns confirmed and projected presentation resources for one battle session.
    /// World-bound event routing remains owned by the simulation runtime.
    /// </summary>
    internal sealed class BattlePresentationSessionResources
    {
        private BattleContext _confirmedContext;
        private ConfirmedViewSnapshotRuntime _confirmedSnapshotRuntime;
        private ConfirmedBattleViewFeature _confirmedFeature;
        private Dictionary<string, ProjectionViewInstance> _projectionViews;

        private sealed class ProjectionViewInstance
        {
            internal BattleProjectionViewInfo Info;
            internal IBattleProjectionViewSource Source;
            internal IActorProjectionProducer LastProducer;
            internal long? LastEpoch;
            internal IDisposable EventSubscription;
            internal bool SourceFaulted;
            internal BattleContext Context;
            internal ProjectedBattleViewFeature Feature;
            internal PredictionViewBridge Bridge;
            internal GameFlowDomain Flow;
            internal Action<IEntity> DestroyEntityTree;
        }

        internal BattleContext ConfirmedContext => _confirmedContext;
        internal ConfirmedBattleViewFeature ConfirmedFeature => _confirmedFeature;
        internal FrameSnapshotDispatcher ConfirmedSnapshots =>
            _confirmedSnapshotRuntime?.Snapshots;
        internal int ProjectionViewCount => _projectionViews?.Count ?? 0;
        internal int PredictionViewCount
        {
            get
            {
                var count = 0;
                if (_projectionViews == null) return 0;
                foreach (var view in _projectionViews.Values)
                    if (view.Info.Role == BattleProjectionViewRole.Prediction) count++;
                return count;
            }
        }

        internal bool TryGetProjectionView(string instanceId, out BattleProjectionViewInfo info)
        {
            if (instanceId != null && _projectionViews != null &&
                _projectionViews.TryGetValue(instanceId, out var instance))
            {
                info = instance.Info;
                return true;
            }
            info = default;
            return false;
        }

        internal void GetProjectionViews(List<BattleProjectionViewInfo> buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            buffer.Clear();
            if (_projectionViews == null) return;
            foreach (var instance in _projectionViews.Values) buffer.Add(instance.Info);
        }

        internal void RebindProjectionViews()
        {
            if (_projectionViews == null) return;
            foreach (var instance in _projectionViews.Values) instance.Feature.RebindAll();
        }

        internal bool TryGetProjectionContext(string instanceId, out BattleContext context)
        {
            if (instanceId != null && _projectionViews != null &&
                _projectionViews.TryGetValue(instanceId, out var instance))
            {
                context = instance.Context;
                return true;
            }
            context = null;
            return false;
        }

        internal bool AddProjectionView(string instanceId, BattleProjectionViewRole role,
            IBattleProjectionViewSource source, BattleContext sourceContext,
            GameFlowDomain flow, Vector3 offset,
            Action<IEntity> destroyEntityTree,
            BattleProjectionViewCapabilities capabilities = BattleProjectionViewCapabilities.Actors)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
                throw new ArgumentException("A projected view requires an instance ID.", nameof(instanceId));
            if (_projectionViews != null && _projectionViews.ContainsKey(instanceId)) return false;
            if (flow == null || sourceContext == null || source == null) return false;
            if ((capabilities & BattleProjectionViewCapabilities.Events) != 0 &&
                !(source is IBattleProjectionViewEventSource)) return false;

            var context = ConfirmedViewContextFactory.Create(sourceContext, source.WorldId);
            var feature = new ProjectedBattleViewFeature(context, role + ":" + instanceId,
                capabilities);
            var instance = new ProjectionViewInstance
            {
                Info = new BattleProjectionViewInfo(instanceId, role, source.WorldId, offset,
                    feature.Capabilities),
                Source = source,
                Context = context,
                Feature = feature,
                Bridge = new PredictionViewBridge(context.EntityWorld, context.EntityLookup, context, offset),
                Flow = flow,
                DestroyEntityTree = destroyEntityTree,
            };
            try
            {
                flow.Attach(instance.Feature);
                _projectionViews ??= new Dictionary<string, ProjectionViewInstance>(StringComparer.Ordinal);
                _projectionViews.Add(instanceId, instance);
                SyncProjectionView(instance);
                return true;
            }
            catch
            {
                ConfirmedViewContextDisposer.Dispose(context, destroyEntityTree);
                throw;
            }
        }

        internal bool RemoveProjectionView(string instanceId)
        {
            if (instanceId == null || _projectionViews == null ||
                !_projectionViews.TryGetValue(instanceId, out var instance))
                return false;
            ResetProjection(instance);
            instance.Flow.Detach(instance.Feature);
            _projectionViews.Remove(instanceId);
            ConfirmedViewContextDisposer.Dispose(instance.Context, instance.DestroyEntityTree);
            if (_projectionViews.Count == 0) _projectionViews = null;
            return true;
        }

        internal void SyncProjectionViews()
        {
            if (_projectionViews == null) return;
            foreach (var instance in _projectionViews.Values)
                SyncProjectionView(instance);
        }

        private static void SyncProjectionView(ProjectionViewInstance instance)
        {
            try
            {
                if (!instance.Source.TryGetProjection(out var producer, out var frame) || producer == null)
                {
                    ResetProjection(instance);
                    instance.SourceFaulted = false;
                    return;
                }
                var epoch = (instance.Source as IBattleProjectionViewEpochSource)?.ProjectionEpoch ?? 0;
                if (!ReferenceEquals(instance.LastProducer, producer) ||
                    instance.LastEpoch != epoch)
                {
                    ResetProjection(instance);
                    instance.LastProducer = producer;
                    instance.LastEpoch = epoch;
                }
                instance.Context.LastFrame = frame;
                instance.Bridge.SyncAllActors(producer);
                if ((instance.Info.Capabilities & BattleProjectionViewCapabilities.Events) != 0 &&
                    instance.EventSubscription == null &&
                    instance.Source is IBattleProjectionViewEventSource eventSource &&
                    instance.Feature.EventSink != null)
                {
                    var eventContext = new BattleProjectionViewEventContext(
                        instance.Info.InstanceId, instance.Info.SourceWorldId,
                        instance.Info.WorldOffset, epoch, frame);
                    instance.EventSubscription = eventSource.SubscribeEvents(
                            instance.Feature.EventSink, eventContext)
                        ?? throw new InvalidOperationException("Projection event source returned no subscription.");
                }
                instance.SourceFaulted = false;
            }
            catch (Exception error)
            {
                if (!instance.SourceFaulted)
                    Log.Exception(error, "[BattleProjectionView:" + instance.Info.InstanceId + "] source update failed");
                instance.SourceFaulted = true;
                ResetProjection(instance);
            }
        }

        private static void ResetProjection(ProjectionViewInstance instance)
        {
            if (instance.LastProducer == null && instance.LastEpoch == null &&
                instance.EventSubscription == null) return;
            instance.EventSubscription?.Dispose();
            instance.EventSubscription = null;
            instance.Feature.ClearTransientPresentation();
            instance.Bridge.ClearActors();
            instance.LastProducer = null;
            instance.LastEpoch = null;
        }

        internal void DisposeProjectionViews()
        {
            if (_projectionViews == null) return;
            var ids = new List<string>(_projectionViews.Keys);
            var cleanup = new Action[ids.Count];
            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                cleanup[i] = () => RemoveProjectionView(id);
            }
            SessionSimRuntimeDisposer.ExecuteCleanupSteps(
                "Failed to dispose projected views.", cleanup);
        }

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
