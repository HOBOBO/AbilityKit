using AbilityKit.Demo.Moba.Services;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Game.Flow.Battle.ViewEvents.Snapshot;
using AbilityKit.Game.Flow.Battle.ViewEvents.Triggering;

namespace AbilityKit.Game.Flow
{
    internal sealed class ViewEventAdapterLifecycle
    {
        private readonly ViewEventAdapterFactory _adapters;
        private readonly ViewEventSourceModePolicy _modePolicy;

        public ViewEventAdapterLifecycle(
            ViewEventAdapterFactory adapters = null,
            ViewEventSourceModePolicy modePolicy = null)
        {
            _adapters = adapters ?? new ViewEventAdapterFactory();
            _modePolicy = modePolicy ?? new ViewEventSourceModePolicy();
        }

        public void Attach(IViewFeatureRuntime runtime)
        {
            if (runtime == null) return;

            Detach(runtime);

            var mode = _modePolicy.Resolve(runtime.Context);

            if (_modePolicy.ShouldUseTriggerAdapter(mode) && runtime.Context?.Session != null)
            {
                runtime.TriggerAdapter = _adapters.CreateTrigger(runtime.Context.Session, runtime.EventSink);
            }

            if (_modePolicy.ShouldUseSnapshotAdapter(mode)
                && runtime.Context != null
                && runtime.Context.TryGetFrameSnapshots(out var snapshots))
            {
                runtime.SnapshotAdapter = _adapters.CreateSnapshot(snapshots, runtime.EventSink);
            }
        }

        public void Detach(IViewFeatureRuntime runtime)
        {
            if (runtime == null) return;

            runtime.SnapshotAdapter?.Dispose();
            runtime.SnapshotAdapter = null;

            runtime.TriggerAdapter?.Dispose();
            runtime.TriggerAdapter = null;
        }
    }

    internal sealed class ViewEventAdapterFactory
    {
        public BattleTriggerEventViewAdapter CreateTrigger(BattleLogicSession session, IBattleViewEventSink sink)
        {
            return new BattleTriggerEventViewAdapter(session, sink);
        }

        public BattleSnapshotViewAdapter CreateSnapshot(FrameSnapshotDispatcher snapshots, IBattleViewEventSink sink)
        {
            return new BattleSnapshotViewAdapter(snapshots, sink);
        }
    }

    public sealed class ViewEventSourceModePolicy
    {
        public BattleViewEventSourceMode Resolve(IBattleRuntimeContext context)
        {
            return context != null
                ? context.Plan.Sync.ViewEventSourceMode
                : BattleViewEventSourceMode.SnapshotOnly;
        }

        public bool ShouldUseTriggerAdapter(BattleViewEventSourceMode mode)
        {
            return mode == BattleViewEventSourceMode.TriggerOnly || mode == BattleViewEventSourceMode.Hybrid;
        }

        public bool ShouldUseSnapshotAdapter(BattleViewEventSourceMode mode)
        {
            return mode == BattleViewEventSourceMode.SnapshotOnly || mode == BattleViewEventSourceMode.Hybrid;
        }
    }
}
