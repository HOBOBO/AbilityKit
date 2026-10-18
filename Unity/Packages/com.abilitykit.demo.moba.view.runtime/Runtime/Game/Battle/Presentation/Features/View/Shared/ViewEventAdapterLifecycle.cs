using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Game.Flow.Battle.ViewEvents.Snapshot;
using AbilityKit.Game.Flow.Battle.ViewEvents.Triggering;

namespace AbilityKit.Game.Flow
{
    internal sealed class ViewEventAdapterLifecycle
    {
        private readonly ViewEventAdapterFactory _adapters;
        private readonly ViewEventSourceModePolicy _modePolicy;
        private IBattleTriggerViewEventSource _triggerSource;
        private IBattleViewEventSink _triggerSink;
        private FrameSnapshotDispatcher _snapshotDispatcher;
        private IBattleViewEventSink _snapshotSink;
        private BattleContext _snapshotContext;
        private long _snapshotBindingGeneration;

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

            Refresh(runtime);
        }

        public void Detach(IViewFeatureRuntime runtime)
        {
            if (runtime == null) return;

            runtime.SnapshotAdapter?.Dispose();
            runtime.SnapshotAdapter = null;

            runtime.TriggerAdapter?.Dispose();
            runtime.TriggerAdapter = null;
            _triggerSource = null;
            _triggerSink = null;
            _snapshotDispatcher = null;
            _snapshotSink = null;
            _snapshotContext = null;
            _snapshotBindingGeneration = 0;
        }

        public void Refresh(IViewFeatureRuntime runtime)
        {
            if (runtime == null) return;
            var mode = _modePolicy.Resolve(runtime.Context?.Plan.Sync.ViewEventSourceMode);
            var source = _modePolicy.ShouldUseTriggerAdapter(mode)
                ? runtime.Context?.TriggerViewEventSource
                : null;
            var sink = runtime.EventSink;
            if (!ReferenceEquals(_triggerSource, source) ||
                !ReferenceEquals(_triggerSink, sink) ||
                (source != null && sink != null && runtime.TriggerAdapter == null))
            {
                runtime.TriggerAdapter?.Dispose();
                runtime.TriggerAdapter = null;
                _triggerSource = source;
                _triggerSink = sink;
                if (source != null && sink != null)
                {
                    var subscription = source.Subscribe(sink);
                    if (subscription != null)
                        runtime.TriggerAdapter = _adapters.CreateTrigger(subscription);
                }
            }

            var snapshots = _modePolicy.ShouldUseSnapshotAdapter(mode) &&
                runtime.Context != null &&
                runtime.Context.TryGetFrameSnapshots(out var current)
                ? current
                : null;
            if (ReferenceEquals(_snapshotDispatcher, snapshots) &&
                ReferenceEquals(_snapshotSink, sink) &&
                ReferenceEquals(_snapshotContext, runtime.Context) &&
                _snapshotBindingGeneration == GetSnapshotBindingGeneration(runtime.Context)) return;
            runtime.SnapshotAdapter?.Dispose();
            runtime.SnapshotAdapter = null;
            _snapshotDispatcher = snapshots;
            _snapshotSink = sink;
            _snapshotContext = runtime.Context;
            _snapshotBindingGeneration = GetSnapshotBindingGeneration(runtime.Context);
            if (snapshots != null && sink != null)
            {
                var context = _snapshotContext;
                var generation = _snapshotBindingGeneration;
                runtime.SnapshotAdapter = _adapters.CreateSnapshot(
                    snapshots,
                    sink,
                    () => context != null &&
                          context.IsCurrentSnapshotRouting(generation, snapshots) &&
                          ReferenceEquals(runtime.Context, context));
            }
        }

        private static long GetSnapshotBindingGeneration(BattleContext context) =>
            context == null ? 0 : context.SnapshotRoutingGeneration;
    }

    internal sealed class ViewEventAdapterFactory
    {
        public BattleTriggerEventViewAdapter CreateTrigger(System.IDisposable subscription)
        {
            return new BattleTriggerEventViewAdapter(subscription);
        }

        public BattleSnapshotViewAdapter CreateSnapshot(
            FrameSnapshotDispatcher snapshots,
            IBattleViewEventSink sink,
            System.Func<bool> isCurrent = null)
        {
            return new BattleSnapshotViewAdapter(snapshots, sink, isCurrent);
        }
    }

}
