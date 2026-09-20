using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Game.Flow.Battle.ViewEvents.Snapshot;
using AbilityKit.Game.Flow.Battle.ViewEvents.Triggering;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Game.Flow
{
    internal sealed class ConfirmedViewEventPipeline : IDisposable
    {
        public FrameSnapshotDispatcher Snapshots { get; private set; }
        public DebugBattleViewEventSink EventSink { get; private set; }
        public BattleSnapshotViewAdapter SnapshotViewAdapter { get; private set; }
        public BattleTriggerEventViewBridge TriggerBridge { get; private set; }

        public ConfirmedViewEventPipeline(
            FrameSnapshotDispatcher snapshots,
            DebugBattleViewEventSink eventSink,
            BattleSnapshotViewAdapter snapshotViewAdapter,
            BattleTriggerEventViewBridge triggerBridge)
        {
            Snapshots = snapshots;
            EventSink = eventSink;
            SnapshotViewAdapter = snapshotViewAdapter;
            TriggerBridge = triggerBridge;
        }

        public void Dispose()
        {
            SnapshotViewAdapter?.Dispose();
            SnapshotViewAdapter = null;

            TriggerBridge?.Dispose();
            TriggerBridge = null;

            EventSink = null;

            Snapshots?.Dispose();
            Snapshots = null;
        }
    }

    internal static class ConfirmedViewEventPipelineFactory
    {
        public static ConfirmedViewEventPipeline Create(
            IWorld confirmedWorld,
            BattleViewEventSourceMode mode,
            int maxDebugLines)
        {
            var snapshots = new FrameSnapshotDispatcher();
            RegisterDebugDecoders(snapshots);

            var sink = new DebugBattleViewEventSink(maxDebugLines);
            var snapshotAdapter = CreateSnapshotAdapter(mode, snapshots, sink);
            var triggerBridge = CreateTriggerBridge(mode, confirmedWorld, sink);

            return new ConfirmedViewEventPipeline(snapshots, sink, snapshotAdapter, triggerBridge);
        }

        private static void RegisterDebugDecoders(FrameSnapshotDispatcher snapshots)
        {
            AbilityKit.Game.Flow.Snapshot.BattleSnapshotRegistry.RegisterAll(
                dispatcherDecoders: snapshots,
                pipelineDecoders: snapshots,
                pipeline: new NullSnapshotPipelineStageRegistry(),
                cmd: new NullSnapshotCmdHandlerRegistry());

            AbilityKit.Game.Flow.Snapshot.SharedSnapshotRegistry.RegisterAll(
                dispatcherDecoders: snapshots,
                pipelineDecoders: snapshots,
                pipeline: new NullSnapshotPipelineStageRegistry(),
                cmd: new NullSnapshotCmdHandlerRegistry());
        }

        private static BattleSnapshotViewAdapter CreateSnapshotAdapter(
            BattleViewEventSourceMode mode,
            FrameSnapshotDispatcher snapshots,
            DebugBattleViewEventSink sink)
        {
            if (mode != BattleViewEventSourceMode.SnapshotOnly && mode != BattleViewEventSourceMode.Hybrid)
            {
                return null;
            }

            return new BattleSnapshotViewAdapter(snapshots, sink);
        }

        private static BattleTriggerEventViewBridge CreateTriggerBridge(
            BattleViewEventSourceMode mode,
            IWorld confirmedWorld,
            DebugBattleViewEventSink sink)
        {
            if (mode != BattleViewEventSourceMode.TriggerOnly && mode != BattleViewEventSourceMode.Hybrid)
            {
                return null;
            }

            if (confirmedWorld?.Services == null || !confirmedWorld.Services.TryResolve(out IEventBus bus) || bus == null)
            {
                return null;
            }

            return new BattleTriggerEventViewBridge(bus, sink);
        }

        private readonly struct NullDisposable : IDisposable
        {
            public void Dispose() { }
        }

        private sealed class NullSnapshotPipelineStageRegistry : ISnapshotPipelineStageRegistry
        {
            public IDisposable AddPipelineStage<T>(int opCode, int order, Action<object, ISnapshotEnvelope, T> handler)
            {
                return new NullDisposable();
            }
        }

        private sealed class NullSnapshotCmdHandlerRegistry : ISnapshotCmdHandlerRegistry
        {
            public void RegisterCmdHandler<T>(int opCode, Action<object, ISnapshotEnvelope, T> handler)
            {
                // Intentionally ignore cmd handlers.
            }
        }
    }
}
