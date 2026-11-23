using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.Behavior;
using AbilityKit.Demo.Moba.Services.Behavior;
using AbilityKit.Moba.Behavior;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// Brain 的可选回滚入口：值恢复与正常激活分离，运行依赖保持同实例。
    /// </summary>
    public sealed partial class MobaBrainService
    {
        internal void RetainExpiredBrain(global::ActorEntity actor)
        {
            var runtimeId = actor.hasActorBrain ? actor.actorBrain.BehaviorInstanceId : 0L;
            if (runtimeId > 0 && !_behaviors.RetainForRecovery(runtimeId))
                throw new InvalidOperationException($"Cannot retain Brain runtime {runtimeId}.");
            _failedCreations.Remove(actor.actorId.Value);
        }

        internal void ReattachExpiredBrain(global::ActorEntity actor)
        {
            var runtimeId = actor.hasActorBrain ? actor.actorBrain.BehaviorInstanceId : 0L;
            if (runtimeId > 0 && !_behaviors.ReattachForRecovery(runtimeId))
                throw new InvalidOperationException($"Missing retained Brain runtime {runtimeId}.");
        }

        internal void ReleaseExpiredBrain(global::ActorEntity actor)
        {
            var runtimeId = actor.hasActorBrain ? actor.actorBrain.BehaviorInstanceId : 0L;
            if (runtimeId > 0) _behaviors.ReleaseRetainedForRecovery(runtimeId);
        }

        internal void ValidateCreatedActorBrainRollback(global::ActorEntity actor, bool retained)
        {
            if (!actor.hasActorBrain || actor.actorBrain.BehaviorInstanceId == 0) return;
            var brain = actor.actorBrain;
            if (!_behaviors.MatchesRecoveryRuntime(brain.BehaviorInstanceId, new BehaviorEntityId(actor.actorId.Value),
                    brain.BrainId, retained))
                throw new InvalidOperationException($"Actor {actor.actorId.Value} does not own rollback Brain runtime {brain.BehaviorInstanceId}.");
        }

        internal void DiscardCreatedActorBrain(global::ActorEntity actor)
        {
            ValidateCreatedActorBrainRollback(actor, retained: false);
            var runtimeId = actor.hasActorBrain ? actor.actorBrain.BehaviorInstanceId : 0L;
            if (runtimeId > 0 && (!_behaviors.RetainForRecovery(runtimeId) || !_behaviors.ReleaseRetainedForRecovery(runtimeId)))
                throw new InvalidOperationException($"Cannot discard rollback Brain runtime {runtimeId}.");
            _failedCreations.Remove(actor.actorId.Value);
        }

        internal BrainRecoveryState CaptureRollbackState(bool includeLocalIdentity = true)
        {
            var entries = new List<BrainRecoveryActor>();
            foreach (var pair in _registry.Entries.OrderBy(p => p.Key))
            {
                var actor = pair.Value;
                if (actor == null) throw new InvalidOperationException($"Brain actor '{pair.Key}' has no entity.");
                var entry = new BrainRecoveryActor
                { ActorId = pair.Key, EntityVersion = _registry.GetEntityVersion(pair.Key), HasBrain = actor.hasActorBrain };
                if (actor.hasActorBrain)
                {
                    var brain = actor.actorBrain;
                    entry.BrainId = brain.BrainId; entry.OwnerActorId = brain.OwnerActorId;
                    entry.SourceKind = brain.SourceKind; entry.SourceId = brain.SourceId;
                    entry.RuntimeId = brain.BehaviorInstanceId;
                    DescribeDefinition(entry);
                }
                entries.Add(entry);
            }
            var failures = _failedCreations.OrderBy(p => p.Key).Select(p => new BrainRecoveryFailure
            {
                ActorId = p.Key, BrainId = p.Value.BrainId, OwnerActorId = p.Value.OwnerActorId,
                SourceKind = p.Value.SourceKind, SourceId = p.Value.SourceId,
                DriverKind = p.Value.DriverKind, DecisionName = p.Value.DecisionName
            }).ToArray();
            var value = new BrainRecoveryState
            { Version = 3, Actors = entries.ToArray(), Failures = failures, Manager = _behaviors.CaptureRecoveryState(includeLocalIdentity) };
            ValidateRollbackState(value, null, validateManager: includeLocalIdentity);
            return value;
        }

        internal void ValidateRollbackState(BrainRecoveryState value, IReadOnlyDictionary<int, int> projected,
            bool validateManager = true, int? restoreFrame = null, ISet<int> discardActors = null)
        {
            if (value == null || value.Version != 3 || value.Actors == null || value.Failures == null || value.Manager == null)
                throw new InvalidOperationException("Incomplete Brain state; v3 is required.");
            var expected = projected ?? _registry.Entries.ToDictionary(p => p.Key, p => _registry.GetEntityVersion(p.Key));
            if (value.Actors.Length != expected.Count) throw new InvalidOperationException("Brain target actor roster differs.");
            var runtimeIds = new HashSet<long>();
            var retainedRuntimeIds = new HashSet<long>();
            var previousActor = 0;
            foreach (var entry in value.Actors)
            {
                if (entry == null || entry.ActorId <= previousActor || entry.EntityVersion <= 0 ||
                    !expected.TryGetValue(entry.ActorId, out var version) || version != entry.EntityVersion)
                    throw new InvalidOperationException("Brain actor anchors are invalid, duplicated or replaced.");
                previousActor = entry.ActorId;
                var exists = _registry.TryGet(entry.ActorId, out var actor) && actor != null;
                var retained = !exists && restoreFrame.HasValue &&
                               _registry.TryGetRetainedForRecovery(entry.ActorId, restoreFrame.Value, out actor);
                exists |= retained;
                if (exists && _registry.GetEntityVersion(entry.ActorId) != entry.EntityVersion)
                    throw new InvalidOperationException($"Brain actor '{entry.ActorId}' entity generation differs.");
                if (!exists && (projected == null || entry.HasBrain))
                    throw new InvalidOperationException($"Brain actor '{entry.ActorId}' cannot be rebound without reconstructing its controller.");
                if (!entry.HasBrain)
                {
                    if (entry.BrainId != 0 || entry.OwnerActorId != 0 || entry.SourceKind != 0 || entry.SourceId != 0 ||
                        entry.RuntimeId != 0 || entry.DriverKind != null || entry.DecisionName != null ||
                        (exists && actor.hasActorBrain && actor.actorBrain.BehaviorInstanceId != 0))
                        throw new InvalidOperationException("Absent Brain contains state or would discard a live runtime.");
                    continue;
                }
                if (entry.BrainId <= 0 || entry.OwnerActorId <= 0 || entry.RuntimeId < 0)
                    throw new InvalidOperationException("Invalid Brain selection.");
                var currentDefinition = new BrainRecoveryActor { BrainId = entry.BrainId };
                DescribeDefinition(currentDefinition);
                if (entry.DriverKind != currentDefinition.DriverKind || entry.DecisionName != currentDefinition.DecisionName)
                    throw new InvalidOperationException($"Brain '{entry.BrainId}' definition changed.");
                if (entry.RuntimeId == 0)
                {
                    if (actor.hasActorBrain && actor.actorBrain.BehaviorInstanceId != 0)
                        throw new InvalidOperationException("Brain rollback cannot remove a surviving runtime.");
                    continue;
                }
                if (!runtimeIds.Add(entry.RuntimeId) || !actor.hasActorBrain ||
                    actor.actorBrain.BehaviorInstanceId != entry.RuntimeId || actor.actorBrain.BrainId != entry.BrainId ||
                    actor.actorBrain.OwnerActorId != entry.OwnerActorId || actor.actorBrain.SourceKind != entry.SourceKind ||
                    actor.actorBrain.SourceId != entry.SourceId || !retained && !_behaviors.GetEntityBehaviors(entry.ActorId).Any(b =>
                        b.InstanceId == entry.RuntimeId && b.SourceContextId == entry.BrainId))
                    throw new InvalidOperationException($"Brain runtime '{entry.RuntimeId}' is missing, replaced or has different provenance.");
                if (retained) retainedRuntimeIds.Add(entry.RuntimeId);
            }
            var discardRuntimes = new HashSet<long>();
            if (discardActors != null)
                foreach (var id in discardActors)
                {
                    if (expected.ContainsKey(id) || !_registry.TryGet(id, out var actor))
                        throw new InvalidOperationException($"Brain rollback cannot discard target or missing actor {id}.");
                    ValidateCreatedActorBrainRollback(actor, retained: false);
                    var runtimeId = actor.hasActorBrain ? actor.actorBrain.BehaviorInstanceId : 0;
                    if (runtimeId > 0 && (runtimeIds.Contains(runtimeId) || !discardRuntimes.Add(runtimeId)))
                        throw new InvalidOperationException($"Brain runtime {runtimeId} overlaps the projected survivors or discards.");
                }
            if (runtimeIds.Count != _behaviors.TotalCount + retainedRuntimeIds.Count - discardRuntimes.Count)
                throw new InvalidOperationException("Brain manager contains orphaned or additional runtimes; cross-roster recovery is unsupported.");
            if (validateManager) _behaviors.ValidateRecoveryState(value.Manager, retainedRuntimeIds, discardRuntimes);
            var previousFailure = 0;
            foreach (var failed in value.Failures)
            {
                if (failed == null || failed.ActorId <= previousFailure || failed.BrainId <= 0 ||
                    failed.OwnerActorId <= 0 || failed.DriverKind == null || failed.DecisionName == null)
                    throw new InvalidOperationException("Invalid Brain creation suppression state.");
                previousFailure = failed.ActorId;
            }
        }

        internal void RestoreRollbackState(BrainRecoveryState value)
        {
            ValidateRollbackState(value, null);
            /*
             * Manager 自带局部补偿；成功前不改选择和创建抑制，不进入激活、准入或输出消费链。
             */
            _behaviors.RestoreRecoveryState(value.Manager);
            foreach (var entry in value.Actors)
            {
                _registry.TryGet(entry.ActorId, out var actor);
                if (entry.HasBrain)
                    actor.ReplaceActorBrain(entry.BrainId, entry.OwnerActorId, entry.SourceKind, entry.SourceId, entry.RuntimeId);
                else if (actor.hasActorBrain) actor.RemoveActorBrain();
            }
            _failedCreations.Clear();
            foreach (var entry in value.Failures)
                _failedCreations.Add(entry.ActorId, new BrainCreationIdentity(entry));
        }

        private void DescribeDefinition(BrainRecoveryActor entry)
        {
            if (_catalog != null && _catalog.TryGet(entry.BrainId, out var definition))
            { entry.DriverKind = definition.DriverKind; entry.DecisionName = definition.DecisionName ?? string.Empty; }
            else { entry.DriverKind = string.Empty; entry.DecisionName = string.Empty; }
        }

        /// <summary>
        /// 服务装配的只读标准查询，依赖由世界内原领域恢复，不持有自有跨帧状态。
        /// 不将任意可继承查询或外部适配器视为无状态。
        /// </summary>
        private sealed class BrainWorldQuery : MobaWorldQuery, IBehaviorRecoveryDependency
        {
            internal BrainWorldQuery(MobaBrainService owner) : base(
                new MobaBrainEntityManager(owner._registry, id => owner._combatRules == null || owner._combatRules.CanTurn(id)),
                new MobaBrainBuffManager(owner._registry, () => owner._effectiveTags),
                new MobaBrainAttributeSystem(owner._registry), false,
                id => owner._tagResults?.GetResultFlags(id),
                (id, gate) => owner._tagGates != null && owner._tagGates.TryIsBlocked(id, gate, out var blocked) ? blocked : null)
            { }

            /// <summary>
            /// 本封闭实现仅查询已恢复的领域状态，无需额外状态写入。
            /// </summary>
            public void ValidateRecoveryDependency() { }
        }
    }

    /// <summary>
    /// Brain v3 的完整值包络，不包含运行依赖的对象图。
    /// </summary>
    [MemoryPackable]
    internal sealed partial class BrainRecoveryState
    {
        /// <summary>
        /// 值格式版本，仅接受 v3。
        /// </summary>
        public int Version;
        /// <summary>
        /// 按 ActorId 升序的完整选择及实体锚点名单。
        /// </summary>
        public BrainRecoveryActor[] Actors;
        /// <summary>
        /// Manager 按注册次序编码的核心及决策状态。
        /// </summary>
        public byte[] Manager;
        /// <summary>
        /// 完整创建失败抑制，不在恢复时重新尝试创建。
        /// </summary>
        public BrainRecoveryFailure[] Failures;
    }

    /// <summary>
    /// Actor 的 Brain 选择值及同代运行时定位信息。
    /// </summary>
    [MemoryPackable]
    internal sealed partial class BrainRecoveryActor
    {
        /// <summary>
        /// 世界内稳定角色身份。
        /// </summary>
        public int ActorId;
        /// <summary>
        /// 实体注册代数，用于拒绝池复用后的替换实例。
        /// </summary>
        public int EntityVersion;
        /// <summary>
        /// Brain 组件存在性。
        /// </summary>
        public bool HasBrain;
        /// <summary>
        /// 选择的目录定义键。
        /// </summary>
        public int BrainId;
        /// <summary>
        /// 选择记录中的所有者。
        /// </summary>
        public int OwnerActorId;
        /// <summary>
        /// 激活来源种类。
        /// </summary>
        public int SourceKind;
        /// <summary>
        /// 激活来源身份。
        /// </summary>
        public int SourceId;
        /// <summary>
        /// 当前会话的行为句柄，零表示没有 Behavior 运行时。
        /// </summary>
        public long RuntimeId;
        /// <summary>
        /// 冻结的驱动注册键。
        /// </summary>
        public string DriverKind;
        /// <summary>
        /// 冻结的决策配置名。
        /// </summary>
        public string DecisionName;
    }

    /// <summary>
    /// 创建失败的完整抑制身份，不能作为普通诊断缓存清空。
    /// </summary>
    [MemoryPackable]
    internal sealed partial class BrainRecoveryFailure
    {
        /// <summary>
        /// 被抑制角色的稳定身份。
        /// </summary>
        public int ActorId;
        /// <summary>
        /// 尝试创建的定义键。
        /// </summary>
        public int BrainId;
        /// <summary>
        /// 尝试创建时的所有者。
        /// </summary>
        public int OwnerActorId;
        /// <summary>
        /// 尝试创建时的来源种类。
        /// </summary>
        public int SourceKind;
        /// <summary>
        /// 尝试创建时的来源身份。
        /// </summary>
        public int SourceId;
        /// <summary>
        /// 尝试创建时的驱动键；缺少定义时为空串。
        /// </summary>
        public string DriverKind;
        /// <summary>
        /// 尝试创建时的配置名；缺少定义时为空串。
        /// </summary>
        public string DecisionName;
    }
}
