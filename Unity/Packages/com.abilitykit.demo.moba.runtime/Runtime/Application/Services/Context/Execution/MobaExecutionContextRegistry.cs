using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Context;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// Domain execution categories. Values are stable because execution snapshots and
    /// optional observers may persist them outside the live combat world.
    /// </summary>
    public enum MobaExecutionKind : byte
    {
        None = 0,
        SkillCast = 1,
        SkillEffect = 2,
        SkillPhase = 3,
        PassiveActivation = 4,
        EffectExecution = 10,
        EffectAction = 11,
        BuffApply = 20,
        BuffTick = 21,
        BuffRemove = 22,
        ProjectileLaunch = 30,
        ProjectileHit = 31,
        AreaSpawn = 40,
        AreaEnter = 41,
        AreaExit = 42,
        AreaExpire = 43,
        AreaStay = 44,
        SummonSpawn = 50,
        SummonDeath = 51,
        UnitSpawn = 60,
        UnitDespawn = 61,
        UnitDeath = 62,
        UnitRespawn = 63,
        DamageAttack = 70,
        DamageCalc = 71,
        DamageApply = 72,
        HealApply = 73,
        PresentationPlay = 80,
        PresentationStop = 81,
    }

    /// <summary>
    /// Stable business reasons for ending an execution context.
    /// Optional projections may display these values but do not own them.
    /// </summary>
    public enum MobaExecutionEndReason : byte
    {
        None = 0,
        Completed = 1,
        Cancelled = 2,
        Expired = 3,
        Dispelled = 4,
        Dead = 5,
        Replaced = 6,
        Interrupted = 7,
        Overridden = 8,
        Failed = 9,
    }

    public readonly struct MobaExecutionContextCreateRequest
    {
        public MobaExecutionContextCreateRequest(
            MobaExecutionKind kind,
            int configId,
            int sourceActorId,
            int targetActorId,
            long parentContextId = 0L,
            long rootContextId = 0L,
            long ownerContextId = 0L,
            int frame = 0,
            int triggerId = 0,
            MobaExecutionKind originKind = MobaExecutionKind.None,
            int originConfigId = 0,
            int castFlowId = 0,
            MobaCombatExecutionFlags combatFlags = MobaCombatExecutionFlags.None)
        {
            Kind = kind;
            ConfigId = configId;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            ParentContextId = parentContextId;
            RootContextId = rootContextId;
            OwnerContextId = ownerContextId;
            Frame = frame;
            TriggerId = triggerId;
            OriginKind = originKind;
            OriginConfigId = originConfigId;
            CastFlowId = castFlowId;
            CombatFlags = combatFlags;
        }

        public MobaExecutionKind Kind { get; }
        public int ConfigId { get; }
        public int SourceActorId { get; }
        public int TargetActorId { get; }
        public long ParentContextId { get; }
        public long RootContextId { get; }
        public long OwnerContextId { get; }
        public int Frame { get; }
        public int TriggerId { get; }
        public MobaExecutionKind OriginKind { get; }
        public int OriginConfigId { get; }
        public int CastFlowId { get; }
        public MobaCombatExecutionFlags CombatFlags { get; }
    }

    public readonly struct MobaExecutionContextNode
    {
        internal MobaExecutionContextNode(
            long contextId,
            long parentContextId,
            long rootContextId,
            long ownerContextId,
            MobaExecutionKind kind,
            int configId,
            int sourceActorId,
            int targetActorId,
            int createdFrame,
            int endedFrame,
            int endReason,
            bool isEnded,
            int triggerId,
            MobaExecutionKind originKind,
            int originConfigId,
            int castFlowId,
            MobaCombatExecutionFlags combatFlags = MobaCombatExecutionFlags.None)
        {
            ContextId = contextId;
            ParentContextId = parentContextId;
            RootContextId = rootContextId;
            OwnerContextId = ownerContextId;
            Kind = kind;
            ConfigId = configId;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            CreatedFrame = createdFrame;
            EndedFrame = endedFrame;
            EndReason = endReason;
            IsEnded = isEnded;
            TriggerId = triggerId;
            OriginKind = originKind;
            OriginConfigId = originConfigId;
            CastFlowId = castFlowId;
            CombatFlags = combatFlags;
        }

        public long ContextId { get; }
        public long ParentContextId { get; }
        public long RootContextId { get; }
        public long OwnerContextId { get; }
        public MobaExecutionKind Kind { get; }
        public int ConfigId { get; }
        public int SourceActorId { get; }
        public int TargetActorId { get; }
        public int CreatedFrame { get; }
        public int EndedFrame { get; }
        public int EndReason { get; }
        public bool IsEnded { get; }
        public int TriggerId { get; }
        public MobaExecutionKind OriginKind { get; }
        public int OriginConfigId { get; }
        public int CastFlowId { get; }
        public MobaCombatExecutionFlags CombatFlags { get; }
        public MobaCombatExecutionFacts CombatFacts => new MobaCombatExecutionFacts(CombatFlags);

        internal MobaExecutionContextNode End(int frame, int reason)
        {
            return new MobaExecutionContextNode(
                ContextId,
                ParentContextId,
                RootContextId,
                OwnerContextId,
                Kind,
                ConfigId,
                SourceActorId,
                TargetActorId,
                CreatedFrame,
                frame,
                reason,
                true,
                TriggerId,
                OriginKind,
                OriginConfigId,
                CastFlowId,
                CombatFlags);
        }
    }

    /// <summary>
    /// Owns deterministic combat execution identities and their minimal causal lineage.
    /// Optional observers, including Trace, consume committed lifecycle events.
    /// </summary>
    [WorldService(typeof(MobaExecutionContextRegistry), WorldLifetime.Scoped)]
    public sealed class MobaExecutionContextRegistry :
        IService,
        IDisposable,
        IContextLifecycleSource<MobaExecutionContextNode>
    {
        public const long FirstExecutionContextId = 1L << 48;

        private readonly Dictionary<long, MobaExecutionContextNode> _nodes =
            new Dictionary<long, MobaExecutionContextNode>();
        private readonly List<ContextLifecycleEventHandler<MobaExecutionContextNode>> _lifecycleObservers =
            new List<ContextLifecycleEventHandler<MobaExecutionContextNode>>();
        private long _nextContextId = FirstExecutionContextId;
        private long _predictionFloor = FirstExecutionContextId;
        private long _lifecycleRevision;

        public Action<ContextLifecycleEvent<MobaExecutionContextNode>, Exception> ObserverException { get; set; }
        public long NextContextId => _nextContextId;
        public int Count => _nodes.Count;
        public long LifecycleRevision => _lifecycleRevision;

        public static bool IsExecutionContextId(long contextId)
        {
            return contextId >= FirstExecutionContextId;
        }

        public IDisposable Observe(
            ContextLifecycleEventHandler<MobaExecutionContextNode> observer,
            bool replayExisting = true)
        {
            if (observer == null) throw new ArgumentNullException(nameof(observer));
            _lifecycleObservers.Add(observer);
            var subscription = new LifecycleObservation(this, observer);
            if (replayExisting) ReplayCurrentState(observer);
            return subscription;
        }

        public IReadOnlyList<MobaExecutionContextNode> CaptureLifecycleSnapshot()
        {
            var nodes = new List<MobaExecutionContextNode>(_nodes.Values);
            nodes.Sort((left, right) => left.ContextId.CompareTo(right.ContextId));
            return nodes;
        }

        public MobaExecutionContextNode Create(in MobaExecutionContextCreateRequest request)
        {
            if (_nextContextId == long.MaxValue)
                throw new InvalidOperationException("Execution context ID space is exhausted.");

            var contextId = _nextContextId++;
            var parentContextId = request.ParentContextId;
            var rootContextId = request.RootContextId;
            var ownerContextId = request.OwnerContextId;

            if (parentContextId != 0L && _nodes.TryGetValue(parentContextId, out var parent))
            {
                if (rootContextId == 0L) rootContextId = parent.RootContextId;
                if (ownerContextId == 0L) ownerContextId = parent.OwnerContextId;
            }

            if (rootContextId == 0L)
                rootContextId = parentContextId != 0L ? parentContextId : contextId;
            if (ownerContextId == 0L) ownerContextId = rootContextId;

            var node = new MobaExecutionContextNode(
                contextId,
                parentContextId,
                rootContextId,
                ownerContextId,
                request.Kind,
                request.ConfigId,
                request.SourceActorId,
                request.TargetActorId,
                request.Frame,
                0,
                0,
                false,
                request.TriggerId,
                request.OriginKind,
                request.OriginConfigId,
                request.CastFlowId,
                request.CombatFlags);
            _nodes.Add(contextId, node);
            _lifecycleRevision++;
            PublishLifecycle(new ContextLifecycleEvent<MobaExecutionContextNode>(
                ContextLifecycleEventKind.Created,
                in node,
                _lifecycleRevision));
            return node;
        }

        public bool End(long contextId, int reason, int frame)
        {
            if (!_nodes.TryGetValue(contextId, out var current) || current.IsEnded) return false;
            var ended = current.End(frame, reason);
            _nodes[contextId] = ended;
            _lifecycleRevision++;
            PublishLifecycle(new ContextLifecycleEvent<MobaExecutionContextNode>(
                ContextLifecycleEventKind.Ended,
                in ended,
                _lifecycleRevision));
            return true;
        }

        public bool TryGet(long contextId, out MobaExecutionContextNode node)
        {
            return _nodes.TryGetValue(contextId, out node);
        }

        public bool TryGetChain(
            long rootContextId,
            out IReadOnlyList<MobaExecutionContextNode> nodes)
        {
            if (rootContextId == 0L)
            {
                nodes = Array.Empty<MobaExecutionContextNode>();
                return false;
            }

            var result = new List<MobaExecutionContextNode>();
            foreach (var node in _nodes.Values)
            {
                if (node.ContextId == rootContextId || node.RootContextId == rootContextId)
                    result.Add(node);
            }

            result.Sort((left, right) => left.ContextId.CompareTo(right.ContextId));
            nodes = result;
            return result.Count > 0;
        }

        public void ValidatePredictionRetraction(long nextContextId)
        {
            if (nextContextId < _predictionFloor || nextContextId > _nextContextId)
                throw new InvalidOperationException($"Invalid execution-context prediction boundary {nextContextId}.");
        }

        public int RetractPrediction(long nextContextId)
        {
            return RetractPrediction(nextContextId, publishEvent: true);
        }

        internal int RetractPredictionForRestore(long nextContextId)
        {
            return RetractPrediction(nextContextId, publishEvent: false);
        }

        private int RetractPrediction(long nextContextId, bool publishEvent)
        {
            ValidatePredictionRetraction(nextContextId);
            var removed = 0;
            for (var id = _nextContextId - 1L; id >= nextContextId; id--)
            {
                if (_nodes.Remove(id)) removed++;
            }

            if (removed > 0 && publishEvent)
            {
                var empty = default(MobaExecutionContextNode);
                _lifecycleRevision++;
                PublishLifecycle(new ContextLifecycleEvent<MobaExecutionContextNode>(
                    ContextLifecycleEventKind.PredictionRetracted,
                    in empty,
                    _lifecycleRevision,
                    nextContextId));
            }
            return removed;
        }

        public void ValidateLifecycleRestore(IReadOnlyList<MobaExecutionContextNode> nodes)
        {
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));
            var ids = new HashSet<long>();
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (!ids.Add(node.ContextId) ||
                    !_nodes.TryGetValue(node.ContextId, out var current) ||
                    !HasSameIdentity(in current, in node) ||
                    (!node.IsEnded && (node.EndedFrame != 0 || node.EndReason != 0)))
                {
                    throw new InvalidOperationException(
                        $"Execution-context lifecycle identity {node.ContextId} is missing or invalid.");
                }
            }
        }

        /// <summary>
        /// Restores lifecycle fields only. Legacy gameplay observers stay silent; read-only
        /// lifecycle observers receive one reconciliation signal and can rebuild their projection.
        /// </summary>
        public void RestoreLifecycle(IReadOnlyList<MobaExecutionContextNode> nodes)
        {
            ValidateLifecycleRestore(nodes);
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                _nodes[node.ContextId] = node;
            }

            _lifecycleRevision++;
            var empty = default(MobaExecutionContextNode);
            PublishLifecycle(new ContextLifecycleEvent<MobaExecutionContextNode>(
                ContextLifecycleEventKind.Reconciled,
                in empty,
                _lifecycleRevision));
        }

        public void Clear()
        {
            var hadNodes = _nodes.Count > 0;
            _nodes.Clear();
            _predictionFloor = _nextContextId;
            if (!hadNodes) return;

            _lifecycleRevision++;
            var empty = default(MobaExecutionContextNode);
            PublishLifecycle(new ContextLifecycleEvent<MobaExecutionContextNode>(
                ContextLifecycleEventKind.Cleared,
                in empty,
                _lifecycleRevision));
        }

        public void Dispose()
        {
            Clear();
            _lifecycleObservers.Clear();
            ObserverException = null;
        }

        private void ReplayCurrentState(
            ContextLifecycleEventHandler<MobaExecutionContextNode> observer)
        {
            var nodes = CaptureLifecycleSnapshot();
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                NotifyLifecycleObserver(
                    observer,
                    new ContextLifecycleEvent<MobaExecutionContextNode>(
                        ContextLifecycleEventKind.Created,
                        in node,
                        _lifecycleRevision,
                        isReplay: true));
                if (!node.IsEnded) continue;
                NotifyLifecycleObserver(
                    observer,
                    new ContextLifecycleEvent<MobaExecutionContextNode>(
                        ContextLifecycleEventKind.Ended,
                        in node,
                        _lifecycleRevision,
                        isReplay: true));
            }
        }

        private void PublishLifecycle(
            in ContextLifecycleEvent<MobaExecutionContextNode> contextEvent)
        {
            if (_lifecycleObservers.Count == 0) return;
            var observers = _lifecycleObservers.ToArray();
            for (var i = 0; i < observers.Length; i++)
                NotifyLifecycleObserver(observers[i], in contextEvent);
        }

        private void NotifyLifecycleObserver(
            ContextLifecycleEventHandler<MobaExecutionContextNode> observer,
            in ContextLifecycleEvent<MobaExecutionContextNode> contextEvent)
        {
            try
            {
                observer(in contextEvent);
            }
            catch (Exception ex)
            {
                try
                {
                    ObserverException?.Invoke(contextEvent, ex);
                }
                catch
                {
                    // Observation diagnostics must not change committed context state.
                }
            }
        }

        private void RemoveLifecycleObserver(
            ContextLifecycleEventHandler<MobaExecutionContextNode> observer)
        {
            if (observer != null) _lifecycleObservers.Remove(observer);
        }

        private sealed class LifecycleObservation : IDisposable
        {
            private MobaExecutionContextRegistry _owner;
            private ContextLifecycleEventHandler<MobaExecutionContextNode> _observer;

            public LifecycleObservation(
                MobaExecutionContextRegistry owner,
                ContextLifecycleEventHandler<MobaExecutionContextNode> observer)
            {
                _owner = owner;
                _observer = observer;
            }

            public void Dispose()
            {
                var owner = _owner;
                var observer = _observer;
                _owner = null;
                _observer = null;
                owner?.RemoveLifecycleObserver(observer);
            }
        }

        private static bool HasSameIdentity(
            in MobaExecutionContextNode current,
            in MobaExecutionContextNode restored)
        {
            return current.ContextId == restored.ContextId &&
                   current.ParentContextId == restored.ParentContextId &&
                   current.RootContextId == restored.RootContextId &&
                   current.OwnerContextId == restored.OwnerContextId &&
                   current.Kind == restored.Kind &&
                   current.ConfigId == restored.ConfigId &&
                   current.SourceActorId == restored.SourceActorId &&
                   current.TargetActorId == restored.TargetActorId &&
                   current.CreatedFrame == restored.CreatedFrame &&
                   current.TriggerId == restored.TriggerId &&
                   current.OriginKind == restored.OriginKind &&
                   current.OriginConfigId == restored.OriginConfigId &&
                   current.CastFlowId == restored.CastFlowId &&
                   current.CombatFlags == restored.CombatFlags;
        }
    }
}
