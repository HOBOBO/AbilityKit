using System;
using System.Diagnostics;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Observability;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services.Observability;

namespace AbilityKit.Demo.Moba.Services
{
    public readonly struct MobaBattleDiagnosticEventDraft
    {
        public MobaBattleDiagnosticEventDraft(
            BattleDiagnosticEventKind kind,
            BattleDiagnosticEventChannel channel,
            BattleDiagnosticEventOutcome outcome = BattleDiagnosticEventOutcome.None,
            long sourceActorId = 0,
            long targetActorId = 0,
            int configId = 0,
            long rootContextId = 0,
            long contextId = 0,
            BattleDiagnosticRuntimeHandle skillRuntime = default,
            long attackId = 0,
            int payloadVersion = 1,
            string summary = "",
            BattleDiagnosticEventPayload payload = default,
            BattleDiagnosticDefinitionKind definitionKind = BattleDiagnosticDefinitionKind.Unknown,
            BattleDiagnosticRuntimeObjectReference subjectObject = default)
        {
            Kind = kind;
            Channel = channel;
            Outcome = outcome;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            ConfigId = configId;
            DefinitionKind = definitionKind == BattleDiagnosticDefinitionKind.Unknown
                ? BattleDiagnosticDefinitionKinds.FromEventKind(kind)
                : definitionKind;
            RootContextId = rootContextId;
            ContextId = contextId;
            SkillRuntime = skillRuntime;
            AttackId = attackId;
            PayloadVersion = payloadVersion;
            Summary = summary ?? string.Empty;
            Payload = payload;
            SubjectObject = subjectObject;
        }

        public BattleDiagnosticEventKind Kind { get; }
        public BattleDiagnosticEventChannel Channel { get; }
        public BattleDiagnosticEventOutcome Outcome { get; }
        public long SourceActorId { get; }
        public long TargetActorId { get; }
        public int ConfigId { get; }
        public BattleDiagnosticDefinitionKind DefinitionKind { get; }
        public ObservationDefinitionRef Definition =>
            new ObservationDefinitionRef((int)DefinitionKind, ConfigId);
        public long RootContextId { get; }
        public long ContextId { get; }
        public ObservationTraceRef Trace =>
            new ObservationTraceRef(RootContextId, ContextId);
        public BattleDiagnosticRuntimeHandle SkillRuntime { get; }
        public long AttackId { get; }
        public int PayloadVersion { get; }
        public string Summary { get; }
        public BattleDiagnosticEventPayload Payload { get; }
        public BattleDiagnosticRuntimeObjectReference SubjectObject { get; }
    }

    public interface IMobaBattleDiagnosticEventSink
    {
        bool TryCollect(in MobaBattleDiagnosticEventDraft draft);
    }

    public interface IMobaBattleDiagnosticEventGate
    {
        bool IsEnabled(BattleDiagnosticEventChannel channel);
    }

    public interface IMobaBattleDiagnosticCapturePolicy
    {
        BattleDiagnosticCaptureMode CaptureMode { get; }
        int StateSampleIntervalFrames { get; }
        bool IsEventEnabled(BattleDiagnosticEventChannel channel);
        bool ShouldSampleState(int frame);
    }

    internal interface IMobaBattleDiagnosticCaptureModeObserver
    {
        void OnCaptureModeChanged(
            BattleDiagnosticCaptureMode previous,
            BattleDiagnosticCaptureMode current,
            int frame);
    }

    public static class MobaBattleDiagnosticEventSinkExtensions
    {
        public static bool IsEnabled(
            this IMobaBattleDiagnosticEventSink sink,
            BattleDiagnosticEventChannel channel)
        {
            if (sink == null || channel == BattleDiagnosticEventChannel.None) return false;
            return !(sink is IMobaBattleDiagnosticEventGate gate) || gate.IsEnabled(channel);
        }
    }

    public interface IMobaBattleDiagnosticCaptureControl : IMobaBattleDiagnosticCapturePolicy
    {
        new BattleDiagnosticCaptureMode CaptureMode { get; set; }
        BattleDiagnosticEventChannel EnabledChannels { get; set; }
        new int StateSampleIntervalFrames { get; set; }
        long LastSequence { get; }
        bool IsFrozen { get; }
        void SetFrozen(bool frozen);
        void Clear();
    }

    [WorldService(typeof(MobaBattleDiagnosticEventCollector), WorldLifetime.Scoped)]
    public sealed class MobaBattleDiagnosticEventCollector :
        IMobaBattleDiagnosticEventSink,
        IMobaBattleDiagnosticEventGate,
        IObservationSink<MobaBattleDiagnosticEventDraft>,
        IMobaBattleDiagnosticCaptureControl,
        IService
    {
        private readonly Func<int> _frameProvider;
        private readonly Func<long> _timestampProvider;
        private long _lastSequence;
        private long _collectFailureCount;
        private string _lastCollectError = string.Empty;
        private int _stateSampleIntervalFrames;
        private BattleDiagnosticCaptureMode _captureMode;
        private IMobaRuntimeObjectKeyResolver _runtimeObjectKeys;
        private IMobaBattleDiagnosticCaptureModeObserver _captureModeObserver;

        [WorldInject(required: false)] private IFrameTime _frameTime = null;

        public MobaBattleDiagnosticEventCollector()
            : this(
                new BattleDiagnosticSessionScope(
                    Guid.NewGuid().ToString("N"),
                    "local",
                    0),
                BattleDiagnosticCaptureOptions.RecommendedDefault)
        {
        }

        public MobaBattleDiagnosticEventCollector(
            BattleDiagnosticSessionScope scope,
            int capacity = BattleDiagnosticEventRingStore.DefaultCapacity,
            Func<int> frameProvider = null,
            Func<long> timestampProvider = null)
            : this(
                scope,
                new BattleDiagnosticCaptureOptions(
                    BattleDiagnosticCaptureMode.Full,
                    BattleDiagnosticEventChannel.All,
                    eventCapacity: capacity),
                frameProvider,
                timestampProvider)
        {
        }

        public MobaBattleDiagnosticEventCollector(
            BattleDiagnosticSessionScope scope,
            BattleDiagnosticCaptureOptions options,
            Func<int> frameProvider = null,
            Func<long> timestampProvider = null)
        {
            Store = new BattleDiagnosticEventRingStore(
                scope,
                options.EventCapacity,
                options.RetainedReadViewCount);
            StateStore = new BattleDiagnosticStateStore(scope);
            MetricStore = new BattleDiagnosticMetricRingStore(scope, options.MetricCapacity);
            _frameProvider = frameProvider;
            _timestampProvider = timestampProvider ?? Stopwatch.GetTimestamp;
            CaptureMode = options.Mode;
            EnabledChannels = options.EnabledChannels;
            StateSampleIntervalFrames = options.StateSampleIntervalFrames;
        }

        public BattleDiagnosticSessionScope Scope => Store.Scope;
        public BattleDiagnosticEventRingStore Store { get; }
        public IBattleDiagnosticStateStore StateStore { get; }
        public BattleDiagnosticMetricRingStore MetricStore { get; }
        public BattleDiagnosticCaptureMode CaptureMode
        {
            get => _captureMode;
            set
            {
                if (!Enum.IsDefined(typeof(BattleDiagnosticCaptureMode), value))
                    throw new ArgumentOutOfRangeException(nameof(value));
                var previous = _captureMode;
                if (previous == value) return;
                _captureMode = value;
                try
                {
                    _captureModeObserver?.OnCaptureModeChanged(
                        previous,
                        value,
                        ResolveFrame());
                }
                catch
                {
                    // Capture mode changes must not fail because diagnostics backfill failed.
                }
            }
        }
        public BattleDiagnosticEventChannel EnabledChannels { get; set; }
        public int StateSampleIntervalFrames
        {
            get => _stateSampleIntervalFrames;
            set => _stateSampleIntervalFrames = value < 1 ? 1 : value;
        }
        public long LastSequence => _lastSequence;
        public long CollectFailureCount => _collectFailureCount;
        public string LastCollectError => _lastCollectError;
        public bool IsFrozen => Store.IsFrozen || StateStore.IsFrozen || MetricStore.IsFrozen;
        internal int CurrentFrame => ResolveFrame();

        internal bool IsMetricHistoryEnabled => CaptureMode == BattleDiagnosticCaptureMode.Full;

        internal bool TryRecordMetric(
            int frame,
            long monotonicTimestamp,
            BattleDiagnosticMetricCategory category,
            BattleDiagnosticMetricValueKind valueKind,
            string metric,
            double value,
            string dimension)
        {
            if (!IsMetricHistoryEnabled) return false;
            try
            {
                var sample = new BattleDiagnosticMetricSample(
                    Scope,
                    MetricStore.LastSequence + 1L,
                    frame,
                    monotonicTimestamp,
                    category,
                    valueKind,
                    metric,
                    value,
                    dimension);
                return MetricStore.TryAppend(in sample);
            }
            catch
            {
                return false;
            }
        }

        public bool IsEnabled(BattleDiagnosticEventChannel channel)
        {
            return IsEventEnabled(channel);
        }

        public bool IsEventEnabled(BattleDiagnosticEventChannel channel)
        {
            return (CaptureMode == BattleDiagnosticCaptureMode.Events ||
                    CaptureMode == BattleDiagnosticCaptureMode.Full) &&
                   channel != BattleDiagnosticEventChannel.None &&
                   (EnabledChannels & channel) != 0;
        }

        public bool ShouldSampleState(int frame)
        {
            return CaptureMode == BattleDiagnosticCaptureMode.Full &&
                   BattleDiagnosticFrames.IsValid(frame) &&
                   frame % StateSampleIntervalFrames == 0;
        }

        public void SetFrozen(bool frozen)
        {
            Store.SetFrozen(frozen);
            StateStore.SetFrozen(frozen);
            if (frozen) MetricStore.Freeze();
            else MetricStore.Resume();
        }

        public void Clear()
        {
            Store.Clear();
            StateStore.Clear();
            MetricStore.Clear();
        }

        public bool TryCollect(in MobaBattleDiagnosticEventDraft draft)
        {
            if (!IsEventEnabled(draft.Channel))
            {
                return false;
            }

            try
            {
                var sequence = _lastSequence + 1L;
                var frame = ResolveFrame();
                var sourceActorGeneration = ResolveGeneration(
                    MobaRuntimeObjectKind.Actor,
                    draft.SourceActorId,
                    frame);
                var targetActorGeneration = ResolveGeneration(
                    MobaRuntimeObjectKind.Actor,
                    draft.TargetActorId,
                    frame);
                var subjectGeneration = draft.SubjectObject.IsResolved
                    ? draft.SubjectObject.Generation
                    : ResolveGeneration(
                        (MobaRuntimeObjectKind)draft.SubjectObject.Kind,
                        draft.SubjectObject.RuntimeId,
                        frame);
                var diagnosticEvent = new BattleDiagnosticEvent(
                    Scope,
                    frame,
                    sequence,
                    _timestampProvider(),
                    draft.Kind,
                    draft.Channel,
                    draft.Outcome,
                    draft.SourceActorId,
                    draft.TargetActorId,
                    draft.ConfigId,
                    draft.RootContextId,
                    draft.ContextId,
                    draft.SkillRuntime,
                    draft.AttackId,
                    draft.PayloadVersion,
                    draft.Summary,
                    draft.Payload,
                    draft.DefinitionKind,
                    sourceActorGeneration,
                    targetActorGeneration,
                    draft.SubjectObject.Kind,
                    draft.SubjectObject.RuntimeId,
                    subjectGeneration);

                if (!Store.TryAppend(diagnosticEvent))
                {
                    RecordCollectFailure("Event store rejected the event.");
                    return false;
                }

                _lastSequence = sequence;
                _lastCollectError = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                RecordCollectFailure(ex.GetType().Name + ": " + ex.Message);
                return false;
            }
        }

        bool IObservationSink<MobaBattleDiagnosticEventDraft>.IsEnabled =>
            (CaptureMode == BattleDiagnosticCaptureMode.Events ||
             CaptureMode == BattleDiagnosticCaptureMode.Full) &&
            EnabledChannels != BattleDiagnosticEventChannel.None;

        bool IObservationSink<MobaBattleDiagnosticEventDraft>.TryWrite(
            in MobaBattleDiagnosticEventDraft value)
        {
            return TryCollect(in value);
        }

        internal void AttachRuntimeObjectKeyResolver(IMobaRuntimeObjectKeyResolver resolver)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            if (_runtimeObjectKeys != null && !ReferenceEquals(_runtimeObjectKeys, resolver))
                throw new InvalidOperationException("A runtime object key resolver is already attached.");
            _runtimeObjectKeys = resolver;
        }

        internal void DetachRuntimeObjectKeyResolver(IMobaRuntimeObjectKeyResolver resolver)
        {
            if (ReferenceEquals(_runtimeObjectKeys, resolver)) _runtimeObjectKeys = null;
        }

        internal void AttachCaptureModeObserver(IMobaBattleDiagnosticCaptureModeObserver observer)
        {
            if (observer == null) throw new ArgumentNullException(nameof(observer));
            if (_captureModeObserver != null && !ReferenceEquals(_captureModeObserver, observer))
                throw new InvalidOperationException("A capture mode observer is already attached.");
            _captureModeObserver = observer;
        }

        internal void DetachCaptureModeObserver(IMobaBattleDiagnosticCaptureModeObserver observer)
        {
            if (ReferenceEquals(_captureModeObserver, observer)) _captureModeObserver = null;
        }

        private int ResolveGeneration(MobaRuntimeObjectKind kind, long runtimeId, int frame)
        {
            if (kind == MobaRuntimeObjectKind.Unknown || runtimeId == 0L) return 0;
            var resolver = _runtimeObjectKeys;
            return resolver != null && resolver.TryResolve(kind, runtimeId, frame, out var key)
                ? key.Generation
                : 0;
        }

        public void Dispose()
        {
        }

        private void RecordCollectFailure(string error)
        {
            _collectFailureCount++;
            _lastCollectError = error ?? string.Empty;
        }

        private int ResolveFrame()
        {
            if (_frameProvider != null)
            {
                return _frameProvider();
            }

            return _frameTime != null ? _frameTime.Frame.Value : 0;
        }
    }

    [WorldService(typeof(IMobaBattleDiagnosticEventSink), WorldLifetime.Scoped)]
    [WorldService(typeof(IMobaBattleDiagnosticEventGate), WorldLifetime.Scoped)]
    [WorldService(typeof(IMobaBattleDiagnosticCaptureControl), WorldLifetime.Scoped)]
    [WorldService(typeof(IMobaBattleDiagnosticCapturePolicy), WorldLifetime.Scoped)]
    [WorldService(typeof(IBattleDiagnosticEventReadStore), WorldLifetime.Scoped)]
    [WorldService(typeof(IBattleDiagnosticEventSnapshotSource), WorldLifetime.Scoped)]
    [WorldService(typeof(IBattleDiagnosticStateStore), WorldLifetime.Scoped)]
    [WorldService(typeof(IBattleDiagnosticStateReadStore), WorldLifetime.Scoped)]
    [WorldService(typeof(IBattleDiagnosticStateSnapshotSource), WorldLifetime.Scoped)]
    [WorldService(typeof(IBattleDiagnosticMetricReadStore), WorldLifetime.Scoped)]
    [WorldService(typeof(IBattleDiagnosticMetricSnapshotSource), WorldLifetime.Scoped)]
    [WorldService(typeof(IBattleDiagnosticMetricSink), WorldLifetime.Scoped)]
    public sealed class MobaBattleDiagnosticCollectorPorts :
        IMobaBattleDiagnosticEventSink,
        IMobaBattleDiagnosticEventGate,
        IObservationSink<MobaBattleDiagnosticEventDraft>,
        IMobaBattleDiagnosticCaptureControl,
        IBattleDiagnosticEventReadStore,
        IBattleDiagnosticEventSnapshotSource,
        IBattleDiagnosticStateStore,
        IBattleDiagnosticStateSnapshotSource,
        IBattleDiagnosticMetricReadStore,
        IBattleDiagnosticMetricSnapshotSource,
        IBattleDiagnosticMetricSink,
        IService
    {
        private readonly MobaBattleDiagnosticEventCollector _collector;

        [WorldInject(required: false)]
        private IBattleDiagnosticActorAttributeStore _attributeStore = null;

        [WorldInject(required: false)]
        private IBattleDiagnosticActorBuffStore _buffStore = null;

        [WorldInject(required: false)]
        private IBattleDiagnosticActorTagStore _tagStore = null;

        [WorldInject(required: false)]
        private IBattleDiagnosticActorEffectStore _effectStore = null;

        [WorldInject(required: false)]
        private IMobaRuntimeObjectCatalogControl _objectCatalog = null;

        public MobaBattleDiagnosticCollectorPorts(
            MobaBattleDiagnosticEventCollector collector)
        {
            _collector = collector ?? throw new ArgumentNullException(nameof(collector));
        }

        public BattleDiagnosticSessionScope Scope => _collector.Scope;
        public long Revision => _collector.StateStore.Revision;
        public int ActorCount => _collector.StateStore.ActorCount;
        public int SnapshotFrame => _collector.StateStore.SnapshotFrame;
        public bool IsFrozen =>
            _collector.Store.IsFrozen ||
            _collector.StateStore.IsFrozen ||
            _collector.MetricStore.IsFrozen ||
            (_attributeStore?.IsFrozen ?? false) ||
            (_buffStore?.IsFrozen ?? false) ||
            (_tagStore?.IsFrozen ?? false) ||
            (_effectStore?.IsFrozen ?? false);

        long IBattleDiagnosticEventReadStore.Revision =>
            _collector.Store.Revision;

        long IBattleDiagnosticMetricReadStore.Revision =>
            _collector.MetricStore.Revision;

        bool IBattleDiagnosticMetricSink.IsEnabled => _collector.IsMetricHistoryEnabled;

        public BattleDiagnosticEventChannel EnabledChannels
        {
            get => _collector.EnabledChannels;
            set => _collector.EnabledChannels = value;
        }

        public BattleDiagnosticCaptureMode CaptureMode
        {
            get => _collector.CaptureMode;
            set => _collector.CaptureMode = value;
        }

        public int StateSampleIntervalFrames
        {
            get => _collector.StateSampleIntervalFrames;
            set => _collector.StateSampleIntervalFrames = value;
        }

        public long LastSequence => _collector.LastSequence;

        public BattleDiagnosticEventTrackSnapshot CaptureEventSnapshot()
        {
            return _collector.Store.CaptureEventSnapshot();
        }

        public BattleDiagnosticStateTrackSnapshot CaptureStateSnapshot()
        {
            return ((IBattleDiagnosticStateSnapshotSource)_collector.StateStore).CaptureStateSnapshot();
        }

        public bool TryCollect(in MobaBattleDiagnosticEventDraft draft)
        {
            return _collector.TryCollect(in draft);
        }

        public BattleDiagnosticMetricTrackSnapshot CaptureMetricSnapshot()
        {
            return _collector.MetricStore.CaptureMetricSnapshot();
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticMetricSample> QueryMetrics(
            BattleDiagnosticMetricQuery query)
        {
            return _collector.MetricStore.QueryMetrics(query);
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticMetricAggregate> QueryMetricAggregates(
            BattleDiagnosticMetricAggregateQuery query)
        {
            return _collector.MetricStore.QueryMetricAggregates(query);
        }

        public bool TryRecordMetric(
            int frame,
            long monotonicTimestamp,
            BattleDiagnosticMetricCategory category,
            BattleDiagnosticMetricValueKind valueKind,
            string metric,
            double value,
            string dimension = "")
        {
            return _collector.TryRecordMetric(
                frame,
                monotonicTimestamp,
                category,
                valueKind,
                metric,
                value,
                dimension);
        }

        bool IObservationSink<MobaBattleDiagnosticEventDraft>.IsEnabled =>
            ((IObservationSink<MobaBattleDiagnosticEventDraft>)_collector).IsEnabled;

        bool IObservationSink<MobaBattleDiagnosticEventDraft>.TryWrite(
            in MobaBattleDiagnosticEventDraft value)
        {
            return _collector.TryCollect(in value);
        }

        public bool IsEnabled(BattleDiagnosticEventChannel channel)
        {
            return _collector.IsEventEnabled(channel);
        }

        public bool IsEventEnabled(BattleDiagnosticEventChannel channel)
        {
            return _collector.IsEventEnabled(channel);
        }

        public bool ShouldSampleState(int frame)
        {
            return _collector.ShouldSampleState(frame);
        }

        public bool TryReplaceSnapshot(
            BattleDiagnosticWorldSummary world,
            System.Collections.Generic.IReadOnlyList<BattleDiagnosticActorSummary> actors)
        {
            return _collector.StateStore.TryReplaceSnapshot(world, actors);
        }

        public bool TryReplaceWorld(BattleDiagnosticWorldSummary world)
        {
            return _collector.StateStore.TryReplaceWorld(world);
        }

        public bool TryReplaceActors(
            System.Collections.Generic.IReadOnlyList<BattleDiagnosticActorSummary> actors)
        {
            return _collector.StateStore.TryReplaceActors(actors);
        }

        public void SetFrozen(bool frozen)
        {
            _collector.Store.SetFrozen(frozen);
            _collector.StateStore.SetFrozen(frozen);
            if (frozen) _collector.MetricStore.Freeze();
            else _collector.MetricStore.Resume();
            _attributeStore?.SetFrozen(frozen);
            _buffStore?.SetFrozen(frozen);
            _tagStore?.SetFrozen(frozen);
            _effectStore?.SetFrozen(frozen);
            _objectCatalog?.SetFrozen(frozen);
        }

        public void Clear()
        {
            _collector.Store.Clear();
            _collector.StateStore.Clear();
            _collector.MetricStore.Clear();
            _attributeStore?.Clear();
            _buffStore?.Clear();
            _tagStore?.Clear();
            _effectStore?.Clear();
            _objectCatalog?.Clear();
        }

        BattleDiagnosticQueryResult<BattleDiagnosticEvent>
            IBattleDiagnosticEventReadStore.Query(BattleDiagnosticEventQuery query)
        {
            return _collector.Store.Query(query);
        }

        public BattleDiagnosticWorldSummary? QueryWorld(int frame)
        {
            return _collector.StateStore.QueryWorld(frame);
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticActorSummary> QueryActors(
            long requestId,
            int frame)
        {
            return _collector.StateStore.QueryActors(requestId, frame);
        }

        public void Dispose()
        {
        }
    }
}
