using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Diagnostics;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 本地诊断会话适配器：桥接 Runtime 事件 Ring Store 和状态 Store，
    /// 实现 <see cref="IBattleDiagnosticReadOnlySession"/> 提供统一只读查询表面。
    /// 本地、远端与离线适配器共享同一查询契约。
    /// </summary>
    [WorldService(typeof(IBattleDiagnosticReadOnlySession), WorldLifetime.Scoped)]
    public sealed class MobaBattleDiagnosticLocalSession :
        IBattleDiagnosticReadOnlySession,
        IBattleDiagnosticRuntimeObjectCatalogSession,
        IBattleDiagnosticMetricSession,
        IService
    {
        private readonly IBattleDiagnosticEventReadStore _eventStore;
        private readonly IBattleDiagnosticStateReadStore _stateStore;
        private readonly IBattleDiagnosticTraceReadStore _traceStore;
        private readonly IBattleDiagnosticActorAttributeReadStore _attributeStore;
        private readonly IBattleDiagnosticActorBuffReadStore _buffStore;
        private readonly IBattleDiagnosticActorTagReadStore _tagStore;
        private readonly IBattleDiagnosticActorEffectReadStore _effectStore;
        private readonly IBattleDiagnosticRuntimeObjectReadStore _runtimeObjectStore;
        private readonly IBattleDiagnosticMetricReadStore _metricStore;
        private readonly BattleDiagnosticSessionInfo _sessionInfo;

        public MobaBattleDiagnosticLocalSession(
            IBattleDiagnosticEventReadStore eventStore,
            IBattleDiagnosticStateReadStore stateStore)
            : this(eventStore, stateStore, null, null, null, null, null)
        {
        }

        public MobaBattleDiagnosticLocalSession(
            IBattleDiagnosticEventReadStore eventStore,
            IBattleDiagnosticStateReadStore stateStore,
            IBattleDiagnosticTraceReadStore traceStore)
            : this(eventStore, stateStore, traceStore, null, null, null, null)
        {
        }

        public MobaBattleDiagnosticLocalSession(
            IBattleDiagnosticEventReadStore eventStore,
            IBattleDiagnosticStateReadStore stateStore,
            IBattleDiagnosticTraceReadStore traceStore,
            IBattleDiagnosticActorAttributeReadStore attributeStore)
            : this(eventStore, stateStore, traceStore, attributeStore, null, null, null)
        {
        }

        public MobaBattleDiagnosticLocalSession(
            IBattleDiagnosticEventReadStore eventStore,
            IBattleDiagnosticStateReadStore stateStore,
            IBattleDiagnosticTraceReadStore traceStore,
            IBattleDiagnosticActorAttributeReadStore attributeStore,
            IBattleDiagnosticActorBuffReadStore buffStore)
            : this(eventStore, stateStore, traceStore, attributeStore, buffStore, null, null)
        {
        }

        public MobaBattleDiagnosticLocalSession(
            IBattleDiagnosticEventReadStore eventStore,
            IBattleDiagnosticStateReadStore stateStore,
            IBattleDiagnosticTraceReadStore traceStore,
            IBattleDiagnosticActorAttributeReadStore attributeStore,
            IBattleDiagnosticActorBuffReadStore buffStore,
            IBattleDiagnosticActorTagReadStore tagStore)
            : this(eventStore, stateStore, traceStore, attributeStore, buffStore, tagStore, null)
        {
        }

        public MobaBattleDiagnosticLocalSession(
            IBattleDiagnosticEventReadStore eventStore,
            IBattleDiagnosticStateReadStore stateStore,
            IBattleDiagnosticTraceReadStore traceStore,
            IBattleDiagnosticActorAttributeReadStore attributeStore,
            IBattleDiagnosticActorBuffReadStore buffStore,
            IBattleDiagnosticActorTagReadStore tagStore,
            IBattleDiagnosticActorEffectReadStore effectStore)
            : this(
                eventStore,
                stateStore,
                traceStore,
                attributeStore,
                buffStore,
                tagStore,
                effectStore,
                null)
        {
        }

        public MobaBattleDiagnosticLocalSession(
            IBattleDiagnosticEventReadStore eventStore,
            IBattleDiagnosticStateReadStore stateStore,
            IBattleDiagnosticTraceReadStore traceStore,
            IBattleDiagnosticActorAttributeReadStore attributeStore,
            IBattleDiagnosticActorBuffReadStore buffStore,
            IBattleDiagnosticActorTagReadStore tagStore,
            IBattleDiagnosticActorEffectReadStore effectStore,
            IBattleDiagnosticRuntimeObjectReadStore runtimeObjectStore)
        {
            _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _traceStore = traceStore;
            _attributeStore = attributeStore;
            _buffStore = buffStore;
            _tagStore = tagStore;
            _effectStore = effectStore;
            _runtimeObjectStore = runtimeObjectStore;
            _metricStore = eventStore as IBattleDiagnosticMetricReadStore;
            if (eventStore.Scope != stateStore.Scope)
            {
                throw new ArgumentException("Event and state stores must use the same session scope.");
            }
            if (traceStore != null && eventStore.Scope != traceStore.Scope)
            {
                throw new ArgumentException("Event, state, and trace stores must use the same session scope.");
            }
            if (attributeStore != null && eventStore.Scope != attributeStore.Scope)
            {
                throw new ArgumentException("Event, state, and actor attribute stores must use the same session scope.");
            }
            if (buffStore != null && eventStore.Scope != buffStore.Scope)
            {
                throw new ArgumentException("Event, state, and actor buff stores must use the same session scope.");
            }
            if (tagStore != null && eventStore.Scope != tagStore.Scope)
            {
                throw new ArgumentException("Event, state, and actor tag stores must use the same session scope.");
            }
            if (effectStore != null && eventStore.Scope != effectStore.Scope)
            {
                throw new ArgumentException("Event, state, and actor effect stores must use the same session scope.");
            }
            if (runtimeObjectStore != null && eventStore.Scope != runtimeObjectStore.Scope)
            {
                throw new ArgumentException("Event, state, and runtime object stores must use the same session scope.");
            }

            var scope = eventStore.Scope;
            var capabilities = BattleDiagnosticCapabilities.WorldState |
                               BattleDiagnosticCapabilities.ActorState |
                               BattleDiagnosticCapabilities.Events;
            if (traceStore != null)
            {
                capabilities |= BattleDiagnosticCapabilities.Trace;
            }
            if (attributeStore != null)
            {
                capabilities |= BattleDiagnosticCapabilities.ActorAttributes;
            }
            if (buffStore != null)
            {
                capabilities |= BattleDiagnosticCapabilities.ActorBuffs;
            }
            if (tagStore != null)
            {
                capabilities |= BattleDiagnosticCapabilities.ActorTags;
            }
            if (effectStore != null)
            {
                capabilities |= BattleDiagnosticCapabilities.ActorEffects;
            }
            if (runtimeObjectStore != null)
            {
                capabilities |= BattleDiagnosticCapabilities.RuntimeObjects;
            }
            if (_metricStore != null)
            {
                capabilities |= BattleDiagnosticCapabilities.FrameMetrics;
            }

            _sessionInfo = new BattleDiagnosticSessionInfo(
                scope,
                "Local Battle Session",
                string.Empty,
                1,
                System.Diagnostics.Stopwatch.Frequency,
                capabilities,
                BattleDiagnosticConnectionState.Connected,
                BattleDiagnosticCaptureState.Capturing);
        }

        public MobaBattleDiagnosticLocalSession(
            MobaBattleDiagnosticEventCollector collector)
            : this(collector?.Store, collector?.StateStore)
        {
        }

        public BattleDiagnosticSessionInfo SessionInfo => _sessionInfo;
        public long EventStoreRevision => _eventStore.Revision;
        public long StateStoreRevision => _stateStore.Revision;
        public long TraceStoreRevision => _traceStore?.Revision ?? 0L;
        public long ActorAttributeStoreRevision => _attributeStore?.Revision ?? 0L;
        public long ActorBuffStoreRevision => _buffStore?.Revision ?? 0L;
        public long ActorTagStoreRevision => _tagStore?.Revision ?? 0L;
        public long ActorEffectStoreRevision => _effectStore?.Revision ?? 0L;
        public long RuntimeObjectStoreRevision => _runtimeObjectStore?.Revision ?? 0L;
        public long MetricStoreRevision => _metricStore?.Revision ?? 0L;
        public long StoreRevision => EventStoreRevision;

        public BattleDiagnosticQueryResult<BattleDiagnosticMetricSample> QueryMetrics(
            BattleDiagnosticMetricQuery query)
        {
            if (_metricStore == null)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticMetricSample>.Unavailable(
                    query.RequestId,
                    0L,
                    BattleDiagnosticDataAvailability.Unsupported,
                    "This session does not provide frame metric history.");
            }

            try
            {
                return _metricStore.QueryMetrics(query);
            }
            catch (Exception ex)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticMetricSample>.Failed(
                    query.RequestId,
                    MetricStoreRevision,
                    "QueryMetrics.Exception",
                    ex.Message);
            }
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticMetricAggregate> QueryMetricAggregates(
            BattleDiagnosticMetricAggregateQuery query)
        {
            if (_metricStore == null)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticMetricAggregate>.Unavailable(
                    query.RequestId,
                    0L,
                    BattleDiagnosticDataAvailability.Unsupported,
                    "This session does not provide frame metric history.");
            }

            try
            {
                return _metricStore.QueryMetricAggregates(query);
            }
            catch (Exception ex)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticMetricAggregate>.Failed(
                    query.RequestId,
                    MetricStoreRevision,
                    "QueryMetricAggregates.Exception",
                    ex.Message);
            }
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticWorldSummary> QueryWorld(
            long requestId,
            int frame)
        {
            if (requestId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requestId));
            }

            try
            {
                var world = _stateStore.QueryWorld(frame);
                if (!world.HasValue)
                {
                    var hasSnapshot = _stateStore.SnapshotFrame != BattleDiagnosticFrames.Invalid;
                    return BattleDiagnosticQueryResult<BattleDiagnosticWorldSummary>.Unavailable(
                        requestId,
                        StateStoreRevision,
                        hasSnapshot
                            ? BattleDiagnosticDataAvailability.NotCaptured
                            : BattleDiagnosticDataAvailability.NotProduced,
                        hasSnapshot
                            ? $"Requested frame {frame} is unavailable; latest-only snapshot is frame {_stateStore.SnapshotFrame}."
                            : "No world state has been sampled yet.");
                }

                var items = new System.Collections.Generic.List<BattleDiagnosticWorldSummary> { world.Value };
                return BattleDiagnosticQueryResult<BattleDiagnosticWorldSummary>.FromItems(
                    requestId,
                    StateStoreRevision,
                    items,
                    false);
            }
            catch (Exception ex)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticWorldSummary>.Failed(
                    requestId,
                    StateStoreRevision,
                    "QueryWorld.Exception",
                    ex.Message);
            }
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticActorSummary> QueryActors(
            long requestId,
            int frame)
        {
            if (requestId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requestId));
            }

            try
            {
                return _stateStore.QueryActors(requestId, frame);
            }
            catch (Exception ex)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticActorSummary>.Failed(
                    requestId,
                    StateStoreRevision,
                    "QueryActors.Exception",
                    ex.Message);
            }
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticEvent> QueryEvents(
            BattleDiagnosticEventQuery query)
        {
            if (query.RequestId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(query));
            }

            try
            {
                return _eventStore.Query(query);
            }
            catch (Exception ex)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticEvent>.Failed(
                    query.RequestId,
                    _eventStore.Revision,
                    "QueryEvents.Exception",
                    ex.Message);
            }
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticRuntimeObject> QueryRuntimeObject(
            long requestId,
            in BattleDiagnosticRuntimeObjectReference reference,
            int frame)
        {
            if (requestId <= 0L) throw new ArgumentOutOfRangeException(nameof(requestId));
            if (!reference.HasRuntimeId) throw new ArgumentException(
                "A runtime object reference with an ID is required.",
                nameof(reference));
            if (_runtimeObjectStore == null)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticRuntimeObject>.Unavailable(
                    requestId,
                    0L,
                    BattleDiagnosticDataAvailability.Unsupported,
                    "The local session does not provide a runtime object catalog.");
            }

            try
            {
                return _runtimeObjectStore.QueryRuntimeObject(requestId, in reference, frame);
            }
            catch (Exception ex)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticRuntimeObject>.Failed(
                    requestId,
                    RuntimeObjectStoreRevision,
                    "QueryRuntimeObject.Exception",
                    ex.Message);
            }
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticRuntimeObject> QueryRuntimeObjects(
            BattleDiagnosticRuntimeObjectQuery query)
        {
            if (!(_runtimeObjectStore is IBattleDiagnosticRuntimeObjectCatalogReadStore store))
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticRuntimeObject>.Unavailable(
                    query.RequestId,
                    RuntimeObjectStoreRevision,
                    BattleDiagnosticDataAvailability.Unsupported,
                    "The local session does not provide runtime object catalog queries.");
            }

            try
            {
                return store.QueryRuntimeObjects(query);
            }
            catch (Exception ex)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticRuntimeObject>.Failed(
                    query.RequestId,
                    RuntimeObjectStoreRevision,
                    "QueryRuntimeObjects.Exception",
                    ex.Message);
            }
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticRuntimeObjectCatalogSummary>
            QueryRuntimeObjectSummary(long requestId)
        {
            if (requestId <= 0L) throw new ArgumentOutOfRangeException(nameof(requestId));
            if (!(_runtimeObjectStore is IBattleDiagnosticRuntimeObjectCatalogReadStore store))
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticRuntimeObjectCatalogSummary>.Unavailable(
                    requestId,
                    RuntimeObjectStoreRevision,
                    BattleDiagnosticDataAvailability.Unsupported,
                    "The local session does not provide a runtime object catalog summary.");
            }

            try
            {
                return store.QueryRuntimeObjectSummary(requestId);
            }
            catch (Exception ex)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticRuntimeObjectCatalogSummary>.Failed(
                    requestId,
                    RuntimeObjectStoreRevision,
                    "QueryRuntimeObjectSummary.Exception",
                    ex.Message);
            }
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticTraceNodeSummary> QueryTrace(
            long requestId,
            long rootContextId)
        {
            if (requestId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requestId));
            }

            if (rootContextId == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rootContextId));
            }

            if (_traceStore == null)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticTraceNodeSummary>.Unavailable(
                    requestId,
                    TraceStoreRevision,
                    BattleDiagnosticDataAvailability.Unsupported,
                    "This session does not provide trace graph queries.");
            }

            try
            {
                return _traceStore.QueryTrace(requestId, rootContextId);
            }
            catch (Exception ex)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticTraceNodeSummary>.Failed(
                    requestId,
                    TraceStoreRevision,
                    "QueryTrace.Exception",
                    ex.Message);
            }
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticActorAttribute> QueryActorAttributes(
            long requestId,
            int frame,
            long actorId)
        {
            if (requestId <= 0) throw new ArgumentOutOfRangeException(nameof(requestId));
            if (actorId == 0) throw new ArgumentOutOfRangeException(nameof(actorId));

            if (_attributeStore == null)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticActorAttribute>.Unavailable(
                    requestId,
                    ActorAttributeStoreRevision,
                    BattleDiagnosticDataAvailability.Unsupported,
                    "This session does not provide actor attribute queries.");
            }

            try
            {
                return _attributeStore.QueryActorAttributes(requestId, frame, actorId);
            }
            catch (Exception ex)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticActorAttribute>.Failed(
                    requestId,
                    ActorAttributeStoreRevision,
                    "QueryActorAttributes.Exception",
                    ex.Message);
            }
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticActorAttributeModifier> QueryActorAttributeModifiers(
            long requestId,
            int frame,
            long actorId)
        {
            if (requestId <= 0) throw new ArgumentOutOfRangeException(nameof(requestId));
            if (actorId == 0) throw new ArgumentOutOfRangeException(nameof(actorId));

            if (_attributeStore == null)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticActorAttributeModifier>.Unavailable(
                    requestId,
                    ActorAttributeStoreRevision,
                    BattleDiagnosticDataAvailability.Unsupported,
                    "This session does not provide actor attribute modifier queries.");
            }

            try
            {
                return _attributeStore.QueryActorAttributeModifiers(requestId, frame, actorId);
            }
            catch (Exception ex)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticActorAttributeModifier>.Failed(
                    requestId,
                    ActorAttributeStoreRevision,
                    "QueryActorAttributeModifiers.Exception",
                    ex.Message);
            }
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticActorBuff> QueryActorBuffs(
            long requestId,
            int frame,
            long actorId)
        {
            if (requestId <= 0) throw new ArgumentOutOfRangeException(nameof(requestId));
            if (actorId == 0) throw new ArgumentOutOfRangeException(nameof(actorId));

            if (_buffStore == null)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticActorBuff>.Unavailable(
                    requestId,
                    ActorBuffStoreRevision,
                    BattleDiagnosticDataAvailability.Unsupported,
                    "This session does not provide actor buff queries.");
            }

            try
            {
                return _buffStore.QueryActorBuffs(requestId, frame, actorId);
            }
            catch (Exception ex)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticActorBuff>.Failed(
                    requestId,
                    ActorBuffStoreRevision,
                    "QueryActorBuffs.Exception",
                    ex.Message);
            }
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticActorTag> QueryActorTags(
            long requestId,
            int frame,
            long actorId)
        {
            if (requestId <= 0) throw new ArgumentOutOfRangeException(nameof(requestId));
            if (actorId == 0) throw new ArgumentOutOfRangeException(nameof(actorId));

            if (_tagStore == null)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticActorTag>.Unavailable(
                    requestId,
                    ActorTagStoreRevision,
                    BattleDiagnosticDataAvailability.Unsupported,
                    "This session does not provide actor tag queries.");
            }

            try
            {
                return _tagStore.QueryActorTags(requestId, frame, actorId);
            }
            catch (Exception ex)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticActorTag>.Failed(
                    requestId,
                    ActorTagStoreRevision,
                    "QueryActorTags.Exception",
                    ex.Message);
            }
        }

        public BattleDiagnosticQueryResult<BattleDiagnosticActorEffect> QueryActorEffects(
            long requestId,
            int frame,
            long actorId)
        {
            if (requestId <= 0) throw new ArgumentOutOfRangeException(nameof(requestId));
            if (actorId == 0) throw new ArgumentOutOfRangeException(nameof(actorId));

            if (_effectStore == null)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticActorEffect>.Unavailable(
                    requestId,
                    ActorEffectStoreRevision,
                    BattleDiagnosticDataAvailability.Unsupported,
                    "This session does not provide actor effect queries.");
            }

            try
            {
                return _effectStore.QueryActorEffects(requestId, frame, actorId);
            }
            catch (Exception ex)
            {
                return BattleDiagnosticQueryResult<BattleDiagnosticActorEffect>.Failed(
                    requestId,
                    ActorEffectStoreRevision,
                    "QueryActorEffects.Exception",
                    ex.Message);
            }
        }

        public void Dispose()
        {
        }
    }
}
