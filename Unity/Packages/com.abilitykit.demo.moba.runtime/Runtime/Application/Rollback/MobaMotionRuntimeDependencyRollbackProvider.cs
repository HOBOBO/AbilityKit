#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Combat.MotionSystem.Collision;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Motion;
using AbilityKit.Demo.Moba.Services.StateSync;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Rollback
{
    /// <summary>
    /// 唯一保存标准 Motion 运行依赖描述和 Actor 独占的 Pipeline.Policy 完整值。
    /// 不拥有速度、时间、碰撞资格、来源名单或结构命令；未知依赖只在显式恢复入口拒绝。
    /// </summary>
    [MobaRollbackProvider(DefaultKey)]
    public sealed class MobaMotionRuntimeDependencyRollbackProvider : IRollbackStateProvider,
        IRollbackSnapshotPreflightProvider, IMobaStagedStateRecoveryProvider
    {
        /// <summary>
        /// 独立运行依赖载荷的固定键；版本一不接受旧的缺失载荷布局。
        /// </summary>
        public const int DefaultKey = 10047;
        private const int Version = 1;
        private const int Limit = 16 * 1024 * 1024;
        private readonly MobaActorRegistry _actors;
        private readonly MobaMotionRuntimeDependencyService _dependencies;

        /*
         * 仅供同程序集故障验收，不是恢复事件或生产业务回调。
         */
        internal Action<int>? BindingRestoredForTesting;

        /// <summary>
        /// 绑定正常业务的 Actor 注册表和世界依赖服务，不额外建立帧调度器。
        /// </summary>
        public MobaMotionRuntimeDependencyRollbackProvider(MobaActorRegistry actors,
            MobaMotionRuntimeDependencyService dependencies)
        {
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
        }

        /// <summary>
        /// 返回运行依赖的唯一恢复键。
        /// </summary>
        public int Key => DefaultKey;

        /// <summary>
        /// 返回可读状态名称。
        /// </summary>
        public string Name => "MotionRuntimeDependencies";

        /// <summary>
        /// 捕获有序、独立的纯值载荷；普通业务未启用回滚时不调用此入口。
        /// </summary>
        public byte[] Export(FrameIndex frame)
        {
            ValidateExclusivePolicies();
            var entries = new List<Entry>();
            foreach (var id in _actors.CopyActorIdsInOrder())
                if (_actors.TryGet(id, out var actor) && actor.hasMotion)
                    entries.Add(Capture(id, actor));
            return Encode(entries);
        }

        /// <summary>
        /// 对齐检查点的值捕获入口。
        /// </summary>
        public byte[] ExportState(FrameIndex frame) => Export(frame);

        /// <summary>
        /// 全快照预检同时核验 Motion 名单、实体代数及必要载荷；任何导入前拒绝不支持的重建。
        /// </summary>
        public void ValidateSnapshot(RollbackSnapshotPreflightContext context)
        {
            var target = Decode(context.RequirePayload(Key).ToArray());
            var versions = MobaEntitasEntityRollbackProvider.ReadSnapshotActorEntityVersions(
                context.RequirePayload(MobaEntitasEntityRollbackProvider.DefaultKey).ToArray());
            var motion = MemoryPackSerializer.Deserialize<MobaActorMotionRollbackPayload>(
                context.RequirePayload(MobaActorMotionRollbackProvider.DefaultKey).ToArray());
            if (motion.Version != MobaActorMotionRollbackProvider.CurrentVersion || motion.Entries == null ||
                motion.Entries.Length != target.Count)
                throw new InvalidOperationException("Motion dependency and ActorMotion target membership differ.");
            var ids = new HashSet<int>();
            foreach (var value in motion.Entries) ids.Add(value.ActorId);
            foreach (var entry in target)
                if (!ids.Remove(entry.Id) || !versions.TryGetValue(entry.Id, out var version) || version != entry.EntityVersion)
                    throw new InvalidOperationException("Motion dependency target identity differs from ECS or ActorMotion.");
            Prepare(target, new HashSet<int>(versions.Keys), context.Frame.Value);
        }

        /// <summary>
        /// 单独检查点入口要求目标 Motion 仍同代存活，不能凭空扩展 Command 重建范围。
        /// </summary>
        public void PrepareRestore(FrameIndex frame, byte[] payload) => Prepare(Decode(payload), null);

        /// <summary>
        /// 全目标准备完成后静默恢复策略值；局部失败只补偿本 Provider 的写入。
        /// 不承诺协调器中其它 Provider 已导入的数据也会被自动撤销。
        /// </summary>
        public void Import(FrameIndex frame, byte[] payload)
        {
            var prepared = Prepare(Decode(payload), null);
            var applied = 0;
            try
            {
                for (; applied < prepared.Count; applied++)
                {
                    var value = prepared[applied];
                    if (value.Pipeline != null)
                    {
                        if (value.Target.HasPolicy) value.RestoredPolicy!.RestoreState(value.Target.Policy);
                        /*
                         * 核心旧属性尚未标注可空，但 null 正是本载荷显式支持的无策略状态。
                         */
                        value.Pipeline.Policy = value.RestoredPolicy!;
                    }
                    BindingRestoredForTesting?.Invoke(value.Target.Id);
                }
            }
            catch (Exception failure)
            {
                Exception? compensation = null;
                for (var i = Math.Min(applied, prepared.Count - 1); i >= 0; i--)
                {
                    var value = prepared[i];
                    if (value.Pipeline == null) continue;
                    try
                    {
                        if (value.OriginalPolicy != null) value.OriginalPolicy.RestoreState(value.OriginalState);
                        value.Pipeline.Policy = value.OriginalPolicy!;
                    }
                    catch (Exception ex) { compensation ??= ex; }
                }
                if (compensation != null) throw new AggregateException("Motion dependency import and local compensation failed.", failure, compensation);
                throw;
            }
        }

        /// <summary>
        /// 对齐检查点的静默恢复入口。
        /// </summary>
        public void ImportState(FrameIndex frame, byte[] payload) => Import(frame, payload);

        /// <summary>
        /// 提交后核验本 Provider 的完整纯值结果。
        /// </summary>
        public void ValidateRestoredState(FrameIndex frame, byte[] payload)
        {
            if (!Export(frame).AsSpan().SequenceEqual(payload))
                throw new InvalidOperationException("Motion dependency state differs after restore.");
        }

        /// <summary>
        /// 哈希与捕获采用相同的有序值布局，不写入对象引用或来源状态。
        /// </summary>
        public void AddStateHash(FrameIndex frame, ref MobaStateHashBuilder hash)
        {
            hash.AddInt(Key);
            foreach (var value in Export(frame)) hash.AddByte(value);
        }

        private Entry Capture(int id, global::ActorEntity actor)
        {
            var motion = actor.motion;
            if (motion.Policy != null || motion.Events != null || motion.Pipeline?.Events != null)
                throw new InvalidOperationException($"Actor {id}: Motion dependency rollback requires component Policy/Events and Pipeline.Events to be null; custom sinks need their own state contract.");
            if (motion.Initialized != (motion.Pipeline != null))
                throw new InvalidOperationException($"Actor {id}: unsupported Motion initialization layout.");
            if (!motion.Initialized && motion.Solver != null)
                throw new InvalidOperationException($"Actor {id}: uninitialized Motion has an explicit dependency.");
            return new Entry
            {
                Id = id, EntityVersion = _actors.GetEntityVersion(id), Initialized = motion.Initialized,
                ComponentSolver = Mode(id, motion.Solver), PipelineSolver = Mode(id, motion.Pipeline?.Solver),
                HasPolicy = motion.Pipeline?.Policy != null,
                Policy = motion.Pipeline?.Policy?.CaptureState() ?? new MotionPipelinePolicyState { Rules = Array.Empty<MotionGroupSuppressionState>() },
            };
        }

        private int Mode(int actorId, IMotionSolver? solver)
        {
            if (solver == null) return 0;
            if (ReferenceEquals(solver, NoMotionSolver.Instance)) return 1;
            if (_dependencies.OwnsStandardSolver(solver)) return 2;
            throw new InvalidOperationException($"Actor {actorId}: Motion Solver is not the world's standard dependency, or its collision/diagnostics inputs changed; custom dependency state is unsupported.");
        }

        private void ValidateExclusivePolicies()
        {
            var owners = new HashSet<MotionPipelinePolicy>();
            var pipelines = new HashSet<MotionPipeline>();
            foreach (var pair in _actors.Entries)
            {
                var actor = pair.Value;
                if (actor == null || !actor.hasMotion) continue;
                var motion = actor.motion;
                if (motion.Pipeline != null && !pipelines.Add(motion.Pipeline) ||
                    motion.Pipeline?.Policy != null && !owners.Add(motion.Pipeline.Policy))
                    throw new InvalidOperationException($"Actor {pair.Key}: shared Motion Pipeline or Policy is unsupported for rollback.");
                if (motion.Policy != null)
                    throw new InvalidOperationException($"Actor {pair.Key}: explicit component Policy is unsupported for rollback.");
            }
        }

        private List<Prepared> Prepare(List<Entry> entries, HashSet<int>? projected, int? restoreFrame = null)
        {
            ValidateExclusivePolicies();
            var result = new List<Prepared>(entries.Count);
            var ids = new HashSet<int>();
            foreach (var entry in entries)
            {
                ids.Add(entry.Id);
                var exists = _actors.TryGet(entry.Id, out var actor) || restoreFrame.HasValue &&
                             _actors.TryGetRetainedForRecovery(entry.Id, restoreFrame.Value, out actor);
                if (projected != null && !projected.Contains(entry.Id) || !exists ||
                    !actor.hasMotion || !_actors.MatchesEntityVersion(entry.Id, entry.EntityVersion))
                    throw new InvalidOperationException($"Actor {entry.Id}: Motion dependencies require a surviving same-generation Motion owner; existing Projectile/Launcher commands do not rebuild Motion.");
                var current = Capture(entry.Id, actor);
                if (entry.Initialized != current.Initialized || entry.ComponentSolver != current.ComponentSolver ||
                    entry.PipelineSolver != current.PipelineSolver)
                    throw new InvalidOperationException($"Actor {entry.Id}: Motion dependency layout or Solver binding changed.");
                var policy = actor.motion.Pipeline?.Policy;
                result.Add(new Prepared(entry, actor.motion.Pipeline, policy, current.Policy));
            }
            foreach (var pair in _actors.Entries)
                if (pair.Value != null && pair.Value.hasMotion && (projected == null || projected.Contains(pair.Key)) && !ids.Contains(pair.Key))
                    throw new InvalidOperationException("Motion dependency target membership is incomplete.");
            return result;
        }

        /*
         * 二进制布局仅含有限值；计数先按剩余字节约束，拒绝尾随数据和不规范布尔值。
         */
        private static byte[] Encode(List<Entry> entries)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(Version);
            writer.Write(entries.Count);
            foreach (var entry in entries)
            {
                writer.Write(entry.Id); writer.Write(entry.EntityVersion); writer.Write(entry.Initialized);
                writer.Write(entry.ComponentSolver); writer.Write(entry.PipelineSolver); writer.Write(entry.HasPolicy);
                writer.Write(entry.Policy.Rules.Length);
                foreach (var rule in entry.Policy.Rules)
                {
                    writer.Write(rule.SuppressorGroupId); writer.Write(rule.SuppressedGroupIds.Length);
                    foreach (var id in rule.SuppressedGroupIds) writer.Write(id);
                }
            }
            if (stream.Length > Limit) throw new InvalidOperationException("Motion dependency payload exceeds the size limit.");
            return stream.ToArray();
        }

        private static List<Entry> Decode(byte[] payload)
        {
            if (payload == null || payload.Length < 8 || payload.Length > Limit)
                throw new InvalidOperationException("Missing or oversized Motion dependency payload.");
            using var stream = new MemoryStream(payload, false);
            using var reader = new BinaryReader(stream);
            if (reader.ReadInt32() != Version) throw new InvalidOperationException("Unsupported Motion dependency version.");
            var count = ReadCount(reader, 22);
            var entries = new List<Entry>(count);
            var previous = 0;
            for (var i = 0; i < count; i++)
            {
                var entry = new Entry
                {
                    Id = reader.ReadInt32(), EntityVersion = reader.ReadInt32(), Initialized = ReadBool(reader),
                    ComponentSolver = reader.ReadInt32(), PipelineSolver = reader.ReadInt32(), HasPolicy = ReadBool(reader),
                };
                if (entry.Id <= previous || entry.EntityVersion <= 0 || entry.ComponentSolver < 0 || entry.ComponentSolver > 2 ||
                    entry.PipelineSolver < 0 || entry.PipelineSolver > 2 ||
                    !entry.Initialized && (entry.HasPolicy || entry.ComponentSolver != 0 || entry.PipelineSolver != 0))
                    throw new InvalidOperationException("Invalid Motion dependency identity, order or binding mode.");
                previous = entry.Id;
                var rules = new MotionGroupSuppressionState[ReadCount(reader, 8)];
                if (!entry.HasPolicy && rules.Length != 0) throw new InvalidOperationException("Absent Motion Policy contains rules.");
                for (var r = 0; r < rules.Length; r++)
                {
                    rules[r].SuppressorGroupId = reader.ReadInt32();
                    var groups = new int[ReadCount(reader, 4)];
                    for (var g = 0; g < groups.Length; g++) groups[g] = reader.ReadInt32();
                    rules[r].SuppressedGroupIds = groups;
                }
                entry.Policy = new MotionPipelinePolicyState { Rules = rules };
                MotionPipelinePolicy.ValidateState(entry.Policy);
                entries.Add(entry);
            }
            if (stream.Position != stream.Length) throw new InvalidOperationException("Trailing Motion dependency payload data.");
            return entries;
        }

        private static int ReadCount(BinaryReader reader, int minimumBytes)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > (reader.BaseStream.Length - reader.BaseStream.Position) / minimumBytes)
                throw new InvalidOperationException("Invalid Motion dependency count.");
            return count;
        }

        private static bool ReadBool(BinaryReader reader)
        {
            var value = reader.ReadByte();
            if (value > 1) throw new InvalidOperationException("Invalid Motion dependency boolean.");
            return value != 0;
        }

        /// <summary>
        /// 独立值状态不持有 Actor、Solver 或 Policy 的运行时引用。
        /// </summary>
        private sealed class Entry
        {
            internal int Id, EntityVersion, ComponentSolver, PipelineSolver;
            internal bool Initialized, HasPolicy;
            internal MotionPipelinePolicyState Policy;
        }

        /// <summary>
        /// 同步调用内的准备结果和局部补偿材料，不进入历史快照。
        /// </summary>
        private sealed class Prepared
        {
            internal readonly Entry Target;
            internal readonly MotionPipeline? Pipeline;
            internal readonly MotionPipelinePolicy? OriginalPolicy, RestoredPolicy;
            internal readonly MotionPipelinePolicyState OriginalState;

            internal Prepared(Entry target, MotionPipeline? pipeline, MotionPipelinePolicy? policy, MotionPipelinePolicyState state)
            {
                Target = target;
                Pipeline = pipeline;
                OriginalPolicy = policy;
                OriginalState = state;
                RestoredPolicy = target.HasPolicy ? policy ?? new MotionPipelinePolicy() : null;
            }
        }
    }
}
