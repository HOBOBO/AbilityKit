using System;
using System.Diagnostics;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Diagnostics;

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
            BattleDiagnosticEventPayload payload = default)
        {
            Kind = kind;
            Channel = channel;
            Outcome = outcome;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            ConfigId = configId;
            RootContextId = rootContextId;
            ContextId = contextId;
            SkillRuntime = skillRuntime;
            AttackId = attackId;
            PayloadVersion = payloadVersion;
            Summary = summary ?? string.Empty;
            Payload = payload;
        }

        public BattleDiagnosticEventKind Kind { get; }
        public BattleDiagnosticEventChannel Channel { get; }
        public BattleDiagnosticEventOutcome Outcome { get; }
        public long SourceActorId { get; }
        public long TargetActorId { get; }
        public int ConfigId { get; }
        public long RootContextId { get; }
        public long ContextId { get; }
        public BattleDiagnosticRuntimeHandle SkillRuntime { get; }
        public long AttackId { get; }
        public int PayloadVersion { get; }
        public string Summary { get; }
        public BattleDiagnosticEventPayload Payload { get; }
    }

    public interface IMobaBattleDiagnosticEventSink
    {
        bool TryCollect(in MobaBattleDiagnosticEventDraft draft);
    }

    public interface IMobaBattleDiagnosticCaptureControl
    {
        BattleDiagnosticEventChannel EnabledChannels { get; set; }
        long LastSequence { get; }
        bool IsFrozen { get; }
        void SetFrozen(bool frozen);
        void Clear();
    }

    [WorldService(typeof(MobaBattleDiagnosticEventCollector), WorldLifetime.Scoped)]
    public sealed class MobaBattleDiagnosticEventCollector :
        IMobaBattleDiagnosticEventSink,
        IMobaBattleDiagnosticCaptureControl,
        IService
    {
        private readonly Func<int> _frameProvider;
        private readonly Func<long> _timestampProvider;
        private long _lastSequence;
        private long _collectFailureCount;
        private string _lastCollectError = string.Empty;

        [WorldInject(required: false)] private IFrameTime _frameTime = null;

        public MobaBattleDiagnosticEventCollector()
            : this(
                new BattleDiagnosticSessionScope(
                    Guid.NewGuid().ToString("N"),
                    "local",
                    0),
                BattleDiagnosticEventRingStore.DefaultCapacity)
        {
        }

        public MobaBattleDiagnosticEventCollector(
            BattleDiagnosticSessionScope scope,
            int capacity = BattleDiagnosticEventRingStore.DefaultCapacity,
            Func<int> frameProvider = null,
            Func<long> timestampProvider = null)
        {
            Store = new BattleDiagnosticEventRingStore(scope, capacity);
            StateStore = new BattleDiagnosticStateStore(scope);
            _frameProvider = frameProvider;
            _timestampProvider = timestampProvider ?? Stopwatch.GetTimestamp;
            EnabledChannels = BattleDiagnosticEventChannel.All;
        }

        public BattleDiagnosticSessionScope Scope => Store.Scope;
        public BattleDiagnosticEventRingStore Store { get; }
        public IBattleDiagnosticStateStore StateStore { get; }
        public BattleDiagnosticEventChannel EnabledChannels { get; set; }
        public long LastSequence => _lastSequence;
        public long CollectFailureCount => _collectFailureCount;
        public string LastCollectError => _lastCollectError;
        public bool IsFrozen => Store.IsFrozen || StateStore.IsFrozen;

        public void SetFrozen(bool frozen)
        {
            Store.SetFrozen(frozen);
            StateStore.SetFrozen(frozen);
        }

        public void Clear()
        {
            Store.Clear();
            StateStore.Clear();
        }

        public bool TryCollect(in MobaBattleDiagnosticEventDraft draft)
        {
            if (draft.Channel == BattleDiagnosticEventChannel.None ||
                (EnabledChannels & draft.Channel) == 0)
            {
                return false;
            }

            try
            {
                var sequence = _lastSequence + 1L;
                var diagnosticEvent = new BattleDiagnosticEvent(
                    Scope,
                    ResolveFrame(),
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
                    draft.Payload);

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
    [WorldService(typeof(IMobaBattleDiagnosticCaptureControl), WorldLifetime.Scoped)]
    [WorldService(typeof(IBattleDiagnosticEventReadStore), WorldLifetime.Scoped)]
    [WorldService(typeof(IBattleDiagnosticEventSnapshotSource), WorldLifetime.Scoped)]
    [WorldService(typeof(IBattleDiagnosticStateStore), WorldLifetime.Scoped)]
    [WorldService(typeof(IBattleDiagnosticStateReadStore), WorldLifetime.Scoped)]
    [WorldService(typeof(IBattleDiagnosticStateSnapshotSource), WorldLifetime.Scoped)]
    public sealed class MobaBattleDiagnosticCollectorPorts :
        IMobaBattleDiagnosticEventSink,
        IMobaBattleDiagnosticCaptureControl,
        IBattleDiagnosticEventReadStore,
        IBattleDiagnosticEventSnapshotSource,
        IBattleDiagnosticStateStore,
        IBattleDiagnosticStateSnapshotSource,
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
            (_attributeStore?.IsFrozen ?? false) ||
            (_buffStore?.IsFrozen ?? false) ||
            (_tagStore?.IsFrozen ?? false) ||
            (_effectStore?.IsFrozen ?? false);

        long IBattleDiagnosticEventReadStore.Revision =>
            _collector.Store.Revision;

        public BattleDiagnosticEventChannel EnabledChannels
        {
            get => _collector.EnabledChannels;
            set => _collector.EnabledChannels = value;
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
            _attributeStore?.SetFrozen(frozen);
            _buffStore?.SetFrozen(frozen);
            _tagStore?.SetFrozen(frozen);
            _effectStore?.SetFrozen(frozen);
        }

        public void Clear()
        {
            _collector.Store.Clear();
            _collector.StateStore.Clear();
            _attributeStore?.Clear();
            _buffStore?.Clear();
            _tagStore?.Clear();
            _effectStore?.Clear();
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
