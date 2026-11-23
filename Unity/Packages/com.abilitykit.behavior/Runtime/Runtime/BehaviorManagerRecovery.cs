using System;
using System.Collections.Generic;
using System.IO;

namespace AbilityKit.Ability.Behavior
{
    /// <summary>
    /// 可选运行依赖准入。依赖自身无遗漏的跨帧状态，或其权威状态已由所属领域恢复。
    /// 实现须只读检查当前具体类型和装配；不支持的派生类型不能自动继承准入。
    /// </summary>
    public interface IBehaviorRecoveryDependency
    {
        /// <summary>
        /// 验证该依赖可用于回滚链路；不得修改业务状态。
        /// </summary>
        void ValidateRecoveryDependency();
    }

    /// <summary>
    /// 同会话、同一批存活行为的可选恢复能力。不重建行为或补发生命周期事件。
    /// </summary>
    public partial class BehaviorManager
    {
        private readonly Dictionary<long, long> _instanceGenerations = new Dictionary<long, long>();
        private readonly Dictionary<long, BehaviorRuntime> _retainedRecoveryRuntimes = new Dictionary<long, BehaviorRuntime>();
        private long _lastInstanceGeneration;

        /// <summary>
        /// 只读确认回滚运行时仍由指定对象和来源持有，且可静默解绑或释放。
        /// </summary>
        public bool MatchesRecoveryRuntime(long instanceId, BehaviorEntityId ownerId, long sourceContextId, bool retained)
        {
            var index = retained ? _retainedRecoveryRuntimes : _behaviors;
            return !_bindings.ContainsKey(instanceId) && index.TryGetValue(instanceId, out var runtime) &&
                runtime.OwnerId.Equals(ownerId) && runtime.SourceContextId == sourceContextId &&
                _instanceGenerations.ContainsKey(instanceId);
        }

        /// <summary>
        /// 将指定同代运行时移出活动索引与 Tick，不退出决策、不触发生命周期事件。
        /// 仅支持没有 BehaviorBinding 的回滚运行时。
        /// </summary>
        public bool RetainForRecovery(long instanceId)
        {
            if (_bindings.ContainsKey(instanceId) || !_behaviors.TryGetValue(instanceId, out var runtime)) return false;
            _retainedRecoveryRuntimes.Add(instanceId, runtime);
            runtime.OnComplete -= OnRuntimeComplete;
            runtime.OnInterrupt -= OnRuntimeInterrupt;
            _behaviors.Remove(instanceId);
            _orderedBehaviors.Remove(runtime);
            if (_entityBehaviors.TryGetValue(runtime.OwnerId, out var owned))
            {
                owned.Remove(runtime);
                if (owned.Count == 0) _entityBehaviors.Remove(runtime.OwnerId);
            }
            return true;
        }

        /// <summary>
        /// 静默重绑已保留的原实例。调用方须在继续 Tick 前导入原状态和注册次序。
        /// </summary>
        public bool ReattachForRecovery(long instanceId)
        {
            if (_behaviors.ContainsKey(instanceId) || !_retainedRecoveryRuntimes.TryGetValue(instanceId, out var runtime))
                return false;
            _retainedRecoveryRuntimes.Remove(instanceId);
            _behaviors.Add(instanceId, runtime);
            _orderedBehaviors.Add(runtime);
            if (!_entityBehaviors.TryGetValue(runtime.OwnerId, out var owned))
                _entityBehaviors.Add(runtime.OwnerId, owned = new List<BehaviorRuntime>());
            owned.Add(runtime);
            runtime.OnComplete += OnRuntimeComplete;
            runtime.OnInterrupt += OnRuntimeInterrupt;
            return true;
        }

        /// <summary>
        /// 回收已经脱离活动索引的实例，不重新结算正常结束事件。
        /// </summary>
        public bool ReleaseRetainedForRecovery(long instanceId)
        {
            if (!_retainedRecoveryRuntimes.TryGetValue(instanceId, out var runtime)) return false;
            _retainedRecoveryRuntimes.Remove(instanceId);
            _instanceGenerations.Remove(instanceId);
            if (runtime.Decision is IDisposable decision) decision.Dispose();
            return true;
        }

        /// <summary>
        /// 备份完整注册次序、分配器、核心和决策状态。确定性哈希可排除仅用于本地资格的代数。
        /// </summary>
        public byte[] CaptureRecoveryState(bool includeLocalIdentity = true)
        {
            CheckRecoveryDependencies();
            return BehaviorRecoveryCodec.Write(writer =>
            {
                writer.Write(1); writer.Write(_nextInstanceId); BehaviorRecoveryCodec.Count(writer, _orderedBehaviors.Count);
                foreach (var runtime in _orderedBehaviors)
                {
                    writer.Write(runtime.InstanceId);
                    writer.Write(includeLocalIdentity ? _instanceGenerations[runtime.InstanceId] : 0L);
                    BehaviorRecoveryCodec.Bytes(writer, Describe(runtime));
                    BehaviorRecoveryCodec.Bytes(writer, runtime.CaptureRecoveryState());
                    var decision = (IBehaviorSnapshotValidator)runtime.Decision;
                    BehaviorRecoveryCodec.Text(writer, decision.SnapshotType);
                    var snapshot = decision.CaptureSnapshot();
                    decision.ValidateSnapshot(snapshot);
                    BehaviorRecoveryCodec.Bytes(writer, snapshot);
                }
            });
        }

        /// <summary>
        /// 全量只读预检。同 ID 的替换实例、未知依赖和缺失状态均拒绝，不能部分恢复。
        /// </summary>
        public void ValidateRecoveryState(byte[] payload) => ParseRecovery(payload);

        /// <summary>
        /// 按上游结构预检选中的保留身份验证名册，不把任何保留实例重新加入活动索引。
        /// </summary>
        public void ValidateRecoveryState(byte[] payload, IReadOnlyCollection<long> retainedInstances) =>
            ParseRecovery(payload, retainedInstances);

        /// <summary>
        /// 按调用者已证明的结构撤销名单预检行为快照。被撤销的活动实例不得出现在目标载荷中。
        /// </summary>
        public void ValidateRecoveryState(byte[] payload, IReadOnlyCollection<long> retainedInstances,
            IReadOnlyCollection<long> discardedInstances) => ParseRecovery(payload, retainedInstances, discardedInstances);

        /// <summary>
        /// 静默恢复同实例状态和索引。写入异常时尝试恢复操作前状态，补偿失败继续显式传播。
        /// </summary>
        public void RestoreRecoveryState(byte[] payload)
        {
            var target = ParseRecovery(payload);
            var previous = ParseRecovery(CaptureRecoveryState());
            try { ApplyRecovery(target); }
            catch (Exception failure)
            {
                try { ApplyRecovery(previous); }
                catch (Exception compensation) { throw new AggregateException("Behavior recovery and compensation failed.", failure, compensation); }
                throw;
            }
        }

        private void CheckRecoveryDependencies()
        {
            if (_bindings.Count != 0)
                throw new InvalidOperationException("Behavior rollback does not support BehaviorBinding; provide its state ownership before enabling rollback.");
            foreach (var runtime in _orderedBehaviors)
            {
                if (runtime.DurationSeconds.HasValue && (float.IsNaN(runtime.DurationSeconds.Value) ||
                    float.IsInfinity(runtime.DurationSeconds.Value) || runtime.DurationSeconds.Value < 0f))
                    throw new InvalidOperationException($"Behavior '{runtime.InstanceId}' has an invalid rollback duration.");
                if (!(runtime.Decision is IBehaviorSnapshotValidator))
                    throw new InvalidOperationException($"Behavior '{runtime.InstanceId}' decision '{runtime.Decision.GetType().FullName}' requires snapshot and read-only validation before enabling rollback.");
                CheckDependency(runtime.Executor, typeof(DefaultExecutor));
                CheckDependency(runtime.World, typeof(DefaultWorldQuery));
            }
        }

        private static void CheckDependency(object dependency, Type builtIn)
        {
            if (dependency != null && dependency.GetType() == builtIn) return;
            if (dependency is IBehaviorRecoveryDependency supported) { supported.ValidateRecoveryDependency(); return; }
            throw new InvalidOperationException($"Behavior dependency '{dependency?.GetType().FullName ?? "null"}' has no rollback state contract. Normal execution remains available.");
        }

        private static byte[] Describe(BehaviorRuntime runtime) => BehaviorRecoveryCodec.Write(writer =>
        {
            BehaviorRecoveryCodec.Text(writer, runtime.BehaviorKind); writer.Write(runtime.SourceContextId);
            writer.Write(runtime.OwnerId.Value); writer.Write(runtime.TargetId.HasValue);
            if (runtime.TargetId.HasValue) writer.Write(runtime.TargetId.Value.Value);
            writer.Write(runtime.DurationSeconds.HasValue);
            if (runtime.DurationSeconds.HasValue) writer.Write(runtime.DurationSeconds.Value);
            writer.Write(runtime.Priority);
            BehaviorRecoveryCodec.Text(writer, runtime.Decision.GetType().AssemblyQualifiedName);
            BehaviorRecoveryCodec.Text(writer, runtime.Executor.GetType().AssemblyQualifiedName);
            BehaviorRecoveryCodec.Text(writer, runtime.World.GetType().AssemblyQualifiedName);
        });

        private ManagerRecovery ParseRecovery(byte[] payload, IReadOnlyCollection<long> retainedInstances = null,
            IReadOnlyCollection<long> discardedInstances = null)
        {
            CheckRecoveryDependencies();
            var retained = retainedInstances == null ? null : new HashSet<long>(retainedInstances);
            var discarded = discardedInstances == null ? null : new HashSet<long>(discardedInstances);
            if (discarded != null)
                foreach (var id in discarded)
                    if (!_behaviors.ContainsKey(id) || retained?.Contains(id) == true)
                        throw new InvalidOperationException($"Behavior '{id}' is not an active rollback discard candidate.");
            return BehaviorRecoveryCodec.Read(payload, reader =>
            {
                if (reader.ReadInt32() != 1) throw new InvalidOperationException("Unsupported behavior manager version.");
                var value = new ManagerRecovery { NextId = reader.ReadInt64() };
                var count = BehaviorRecoveryCodec.Count(reader);
                if (count != _behaviors.Count + (retained?.Count ?? 0) - (discarded?.Count ?? 0))
                    throw new InvalidOperationException("Behavior rollback requires the same surviving runtime roster.");
                var seen = new HashSet<long>();
                for (var i = 0; i < count; i++)
                {
                    var id = reader.ReadInt64(); var generation = reader.ReadInt64();
                    if (!seen.Add(id) || discarded?.Contains(id) == true ||
                        !(_behaviors.TryGetValue(id, out var runtime) || retained != null && retained.Remove(id) &&
                            _retainedRecoveryRuntimes.TryGetValue(id, out runtime)) ||
                        !_instanceGenerations.TryGetValue(id, out var liveGeneration) || generation != liveGeneration ||
                        id <= 0 || id >= value.NextId)
                        throw new InvalidOperationException($"Behavior '{id}' is missing, replaced or incompatible with allocator.");
                    var description = BehaviorRecoveryCodec.Bytes(reader);
                    if (!description.AsSpan().SequenceEqual(Describe(runtime)))
                        throw new InvalidOperationException($"Behavior '{id}' frozen dependency description differs.");
                    var core = BehaviorRecoveryCodec.Bytes(reader);
                    runtime.ValidateRecoveryState(core);
                    var decision = (IBehaviorSnapshotValidator)runtime.Decision;
                    if (!string.Equals(BehaviorRecoveryCodec.Text(reader), decision.SnapshotType, StringComparison.Ordinal))
                        throw new InvalidOperationException($"Behavior '{id}' snapshot type differs.");
                    var state = BehaviorRecoveryCodec.Bytes(reader);
                    decision.ValidateSnapshot(state);
                    value.Entries.Add((runtime, core, state));
                }
                if (retained != null && retained.Count != 0)
                    throw new InvalidOperationException("Behavior rollback omits a projected retained runtime.");
                if (value.NextId <= 0 || value.NextId == long.MaxValue) throw new InvalidOperationException("Invalid behavior allocator.");
                return value;
            });
        }

        private void ApplyRecovery(ManagerRecovery value)
        {
            foreach (var entry in value.Entries)
            {
                ((IBehaviorSnapshotValidator)entry.Runtime.Decision).RestoreSnapshot(entry.Decision);
                entry.Runtime.RestoreRecoveryState(entry.Core);
            }
            _nextInstanceId = value.NextId;
            _orderedBehaviors.Clear(); _entityBehaviors.Clear();
            foreach (var entry in value.Entries)
            {
                var runtime = entry.Runtime;
                _orderedBehaviors.Add(runtime);
                if (!_entityBehaviors.TryGetValue(runtime.OwnerId, out var list))
                    _entityBehaviors.Add(runtime.OwnerId, list = new List<BehaviorRuntime>());
                list.Add(runtime);
            }
        }

        /// <summary>
        /// 已验证的目标名单和值载荷，运行时引用只指向当前同代实例。
        /// </summary>
        private sealed class ManagerRecovery
        {
            internal long NextId;
            internal readonly List<(BehaviorRuntime Runtime, byte[] Core, byte[] Decision)> Entries =
                new List<(BehaviorRuntime, byte[], byte[])>();
        }
    }
}
