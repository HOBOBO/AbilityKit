using MemoryPack;
using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Core.Pooling;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Motion;
using AbilityKit.Demo.Moba.Services.StateSync;

namespace AbilityKit.Demo.Moba.Rollback
{
    /// <summary>
    /// 捕获和恢复 Motion 组件的权威纯数据；Transform 与 MoveInput 由生成式 ECS Provider 持有。
    /// v5 是当前唯一正式布局，碰撞触发资格只在此处恢复，不补发历史事件。
    /// </summary>
    [MobaRollbackProvider(DefaultKey)]
    public sealed class MobaActorMotionRollbackProvider : IRollbackStateProvider, IMobaStateRecoveryProvider,
        IRollbackSnapshotPreflightProvider
    {
        /// <summary>
        /// Actor Motion 权威状态的稳定 Provider 键。
        /// </summary>
        public const int DefaultKey = 10001;
        internal const int CurrentVersion = 5;

        private static readonly ObjectPool<List<MobaActorMotionRollbackEntry>> s_entryListPool = Pools.GetPool(
            createFunc: () => new List<MobaActorMotionRollbackEntry>(16),
            onRelease: list => list.Clear(),
            defaultCapacity: 8,
            maxSize: 64,
            collectionCheck: false);

        private readonly MobaActorRegistry _registry;

        public MobaActorMotionRollbackProvider(MobaActorRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public int Key => DefaultKey;

        public string Name => "ActorMotion";

        public byte[] ExportState(FrameIndex frame)
        {
            return Export(frame);
        }

        public void ImportState(FrameIndex frame, byte[] payload)
        {
            Import(frame, payload);
        }

        public void AddStateHash(FrameIndex frame, ref MobaStateHashBuilder hash)
        {
            var entries = s_entryListPool.Get();
            try
            {
                foreach (var kv in _registry.Entries)
                {
                    var actorId = kv.Key;
                    var e = kv.Value;
                    if (e == null) continue;
                    if (!e.hasMotion) continue;
                    entries.Add(CreateEntry(actorId, e));
                }

                entries.Sort((a, b) => a.ActorId.CompareTo(b.ActorId));
                hash.AddInt(Key);
                hash.AddInt(entries.Count);

                for (int i = 0; i < entries.Count; i++)
                {
                    var it = entries[i];
                    AddEntryHash(it, ref hash);
                }
            }
            finally
            {
                s_entryListPool.Release(entries);
            }
        }

        public byte[] Export(FrameIndex frame)
        {
            var entries = s_entryListPool.Get();
            try
            {
                foreach (var kv in _registry.Entries)
                {
                    var actorId = kv.Key;
                    var e = kv.Value;
                    if (e == null) continue;
                    if (!e.hasMotion) continue;
                    entries.Add(CreateEntry(actorId, e));
                }

                entries.Sort((a, b) => a.ActorId.CompareTo(b.ActorId));
                var payloadEntries = entries.Count == 0 ? Array.Empty<MobaActorMotionRollbackEntry>() : entries.ToArray();
                return MemoryPackSerializer.Serialize(new MobaActorMotionRollbackPayload(CurrentVersion, payloadEntries));
            }
            finally
            {
                s_entryListPool.Release(entries);
            }
        }

        /// <summary>
        /// 单份导入前校验版本、全部条目和现有 Motion 组件，失败时不写入任何 Actor。
        /// </summary>
        public void ValidateImport(FrameIndex frame, byte[] payload) => Read(payload, null);

        /// <summary>
        /// 全快照预检可读取目标实体名单；基础组件骨架由既有 Command 恢复。
        /// </summary>
        public void ValidateSnapshot(RollbackSnapshotPreflightContext context)
        {
            HashSet<int> projected = null;
            if (context.TryGetPayload(MobaEntitasEntityRollbackProvider.DefaultKey, out var entities))
                projected = MobaEntitasEntityRollbackProvider.ReadSnapshotActorIds(entities.ToArray());
            Read(context.RequirePayload(Key).ToArray(), projected);
        }

        /// <summary>
        /// 完成整份数据预检后静默写回 Motion 状态，不调用碰撞、激活或结束回调。
        /// </summary>
        public void Import(FrameIndex frame, byte[] payload)
        {
            var p = Read(payload, null);
            for (int i = 0; i < p.Entries.Length; i++)
            {
                var it = p.Entries[i];
                if (_registry.TryGet(it.ActorId, out var e) && e != null)
                {
                    SyncMotionKinematics(e, in it);
                }
            }
        }

        private MobaActorMotionRollbackPayload Read(byte[] payload, HashSet<int> projected)
        {
            if (payload == null || payload.Length == 0 || payload.Length > 16 * 1024 * 1024)
                throw new InvalidOperationException("Missing or oversized ActorMotion rollback payload.");
            var state = MemoryPackSerializer.Deserialize<MobaActorMotionRollbackPayload>(payload);
            if (state.Version != CurrentVersion || state.Entries == null ||
                !payload.AsSpan().SequenceEqual(MemoryPackSerializer.Serialize(state)))
                throw new InvalidOperationException("Unsupported or invalid ActorMotion rollback payload; v5 is required.");
            var ids = new HashSet<int>();
            foreach (var entry in state.Entries)
            {
                ValidateEntry(in entry);
                if (!ids.Add(entry.ActorId))
                    throw new InvalidOperationException("ActorMotion contains a duplicate actor identity.");
                if (projected != null && !projected.Contains(entry.ActorId))
                    throw new InvalidOperationException("ActorMotion actor is outside the target entity roster.");
                if (_registry.TryGet(entry.ActorId, out var actor))
                {
                    if (!actor.hasMotion)
                        throw new InvalidOperationException("ActorMotion requires the same surviving Motion component membership.");
                }
                else if (projected == null)
                    throw new InvalidOperationException("ActorMotion target is missing after structural restoration.");
            }
            foreach (var pair in _registry.Entries)
                if (pair.Value != null && pair.Value.hasMotion &&
                    (projected == null || projected.Contains(pair.Key)) && !ids.Contains(pair.Key))
                    throw new InvalidOperationException("ActorMotion target membership is incomplete.");
            return state;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void ValidateEntry(in MobaActorMotionRollbackEntry entry)
        {
            if (entry.ActorId <= 0)
                throw new InvalidOperationException("ActorMotion requires a positive actor identity.");
            if (!Finite(entry.MotionTime) || entry.MotionTime < 0)
                throw new InvalidOperationException($"ActorMotion actor {entry.ActorId} requires finite nonnegative MotionTime.");
            if (!Finite(entry.MotionVelocity.X) || !Finite(entry.MotionVelocity.Y) || !Finite(entry.MotionVelocity.Z))
                throw new InvalidOperationException($"ActorMotion actor {entry.ActorId} requires finite MotionVelocity coordinates.");
            entry.HitTrigger.Validate();
        }

        private static MobaActorMotionRollbackEntry CreateEntry(int actorId, global::ActorEntity entity)
        {
            /*
             * 组件资格是未来碰撞能否触发的权威数据，包含已经消费后的 default。
             * 不从 Continuous 的初始描述重新推断，避免覆盖其它来源后来设置的资格。
             */
            var motionState = entity.motion.State;
            var entry = new MobaActorMotionRollbackEntry(
                actorId,
                motionState.Velocity,
                motionState.Time,
                MobaMotionHitTriggerRollbackState.From(entity.motion.HitTriggerRuntime));
            /*
             * 捕获和 hash 入口同样拒绝导入时已明确不支持的值，避免产出不可恢复的检查点。
             * 校验只发生在显式恢复适配入口，不限制普通业务对组件的赋值方式。
             */
            ValidateEntry(in entry);
            return entry;
        }

        /*
         * 与位置/朝向同步共同恢复组件纯数据。Pipeline 的来源名单与次序仍归联合生命周期，
         * 此处只还原检查点中的碰撞资格，不调用 HitTrigger 服务。
         */
        private static void SyncMotionKinematics(global::ActorEntity e, in MobaActorMotionRollbackEntry entry)
        {
            if (e == null || !e.hasMotion) return;

            var m = e.motion;
            var state = m.State;
            state.Velocity = entry.MotionVelocity;
            state.Time = entry.MotionTime;
            /*
             * 标准 Actor 投影可在下一次 Motion Tick 前读取输出速度，零时间步也不会清空它。
             * Pipeline 每次求解都将同一速度写入 State 与 Output，因此从恢复的权威值重建，
             * 不另存重复字段，也不调用求解、推进来源或补发碰撞通知。
             */
            var output = m.Output;
            output.NewVelocity = state.Velocity;
            e.ReplaceMotion(m.Pipeline, state, output, m.Solver, m.Policy, m.Events, m.Initialized, entry.HitTrigger.ToRuntime());
        }

        private static void AddEntryHash(in MobaActorMotionRollbackEntry entry, ref MobaStateHashBuilder hash)
        {
            hash.AddInt(entry.ActorId);
            hash.AddFloat(entry.MotionVelocity.X);
            hash.AddFloat(entry.MotionVelocity.Y);
            hash.AddFloat(entry.MotionVelocity.Z);
            hash.AddFloat(entry.MotionTime);
            var hit = entry.HitTrigger;
            hash.AddInt(hit.TriggerId); hash.AddInt(hit.SourceActorId); hash.AddInt(hit.SourceConfigId);
            hash.AddLong(hit.EffectContextId); hash.AddInt(hit.EffectConfigId); hash.AddInt(hit.ScopeTriggerId);
            hash.AddInt(hit.ScopeSourceActorId); hash.AddInt(hit.ScopeTargetActorId); hash.AddBool(hit.IsRoot);
            hash.AddInt(hit.CurrentActionIndex); hash.AddLong(hit.CurrentActionContextId); hash.AddLong(hit.CurrentActionId);
            hash.AddLong(hit.SkillRuntimeId); hash.AddInt(hit.SkillRuntimeGeneration); hash.AddLong(hit.SkillRuntimeRootContextId);
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaActorMotionRollbackPayload
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly MobaActorMotionRollbackEntry[] Entries;

        [MemoryPackConstructor]
        public MobaActorMotionRollbackPayload(int version, MobaActorMotionRollbackEntry[] entries)
        {
            Version = version;
            Entries = entries;
        }
    }

    /// <summary>
    /// ActorMotion v5 的单个 Motion 权威状态快照，来源对象及 Pipeline 不在此载荷内。
    /// </summary>
    [MemoryPackable]
    public readonly partial struct MobaActorMotionRollbackEntry
    {
        [MemoryPackOrder(0)] public readonly int ActorId;
        [MemoryPackOrder(1)] public readonly Vec3 MotionVelocity;
        [MemoryPackOrder(2)] public readonly float MotionTime;
        /// <summary>
        /// 检查点中的碰撞触发资格；default 明确表示资格为空或已消费。
        /// </summary>
        [MemoryPackOrder(3)] public readonly MobaMotionHitTriggerRollbackState HitTrigger;

        public MobaActorMotionRollbackEntry(
            int actorId,
            Vec3 motionVelocity,
            float motionTime,
            MobaMotionHitTriggerRollbackState hitTrigger = default)
        {
            // MemoryPack writes this unmanaged value including its alignment padding.
            // Initialize the complete value so equal fields always produce equal bytes.
            this = default;
            ActorId = actorId;
            MotionVelocity = motionVelocity;
            MotionTime = motionTime;
            HitTrigger = hitTrigger;
        }

    }

    /// <summary>
    /// 碰撞触发资格的强类型纯数据投影，完整保存执行作用域及技能运行时身份。
    /// 不保存命中事件或运行时引用，也不会在恢复时执行 Trigger。
    /// </summary>
    [MemoryPackable]
    public readonly partial struct MobaMotionHitTriggerRollbackState
    {
        /// <summary>
        /// 未来碰撞应执行的 Trigger，零表示空资格。
        /// </summary>
        [MemoryPackOrder(0)] public readonly int TriggerId;
        /// <summary>
        /// 效果来源 Actor 身份。
        /// </summary>
        [MemoryPackOrder(1)] public readonly int SourceActorId;
        /// <summary>
        /// 来源配置身份。
        /// </summary>
        [MemoryPackOrder(2)] public readonly int SourceConfigId;
        /// <summary>
        /// 冻结的效果执行上下文。
        /// </summary>
        [MemoryPackOrder(3)] public readonly long EffectContextId;
        /// <summary>
        /// 冻结的效果配置。
        /// </summary>
        [MemoryPackOrder(4)] public readonly int EffectConfigId;
        /// <summary>
        /// 原执行作用域的 Trigger。
        /// </summary>
        [MemoryPackOrder(5)] public readonly int ScopeTriggerId;
        /// <summary>
        /// 原执行作用域的来源 Actor。
        /// </summary>
        [MemoryPackOrder(6)] public readonly int ScopeSourceActorId;
        /// <summary>
        /// 原执行作用域的目标 Actor。
        /// </summary>
        [MemoryPackOrder(7)] public readonly int ScopeTargetActorId;
        /// <summary>
        /// 原作用域是否是根效果。
        /// </summary>
        [MemoryPackOrder(8)] public readonly bool IsRoot;
        /// <summary>
        /// 原作用域动作下标。
        /// </summary>
        [MemoryPackOrder(9)] public readonly int CurrentActionIndex;
        /// <summary>
        /// 原动作上下文身份。
        /// </summary>
        [MemoryPackOrder(10)] public readonly long CurrentActionContextId;
        /// <summary>
        /// 原动作身份。
        /// </summary>
        [MemoryPackOrder(11)] public readonly long CurrentActionId;
        /// <summary>
        /// 所属技能运行时身份。
        /// </summary>
        [MemoryPackOrder(12)] public readonly long SkillRuntimeId;
        /// <summary>
        /// 技能运行时世代。
        /// </summary>
        [MemoryPackOrder(13)] public readonly int SkillRuntimeGeneration;
        /// <summary>
        /// 技能运行时根执行上下文。
        /// </summary>
        [MemoryPackOrder(14)] public readonly long SkillRuntimeRootContextId;

        /// <summary>
        /// 从明确的字段构造快照，整体清零以固定非托管布局中的对齐填充字节。
        /// </summary>
        public MobaMotionHitTriggerRollbackState(int triggerId, int sourceActorId, int sourceConfigId,
            long effectContextId, int effectConfigId, int scopeTriggerId, int scopeSourceActorId,
            int scopeTargetActorId, bool isRoot, int currentActionIndex, long currentActionContextId,
            long currentActionId, long skillRuntimeId, int skillRuntimeGeneration, long skillRuntimeRootContextId)
        {
            this = default;
            TriggerId = triggerId; SourceActorId = sourceActorId; SourceConfigId = sourceConfigId;
            EffectContextId = effectContextId; EffectConfigId = effectConfigId; ScopeTriggerId = scopeTriggerId;
            ScopeSourceActorId = scopeSourceActorId; ScopeTargetActorId = scopeTargetActorId; IsRoot = isRoot;
            CurrentActionIndex = currentActionIndex; CurrentActionContextId = currentActionContextId;
            CurrentActionId = currentActionId; SkillRuntimeId = skillRuntimeId;
            SkillRuntimeGeneration = skillRuntimeGeneration; SkillRuntimeRootContextId = skillRuntimeRootContextId;
        }

        internal static MobaMotionHitTriggerRollbackState From(MobaMotionHitTriggerRuntime value)
        {
            var scope = value.ExecutionScope;
            var handle = value.SkillRuntimeHandle;
            return new MobaMotionHitTriggerRollbackState(value.TriggerId, value.SourceActorId, value.SourceConfigId,
                scope.EffectContextId, scope.EffectConfigId, scope.TriggerId, scope.SourceActorId, scope.TargetActorId,
                scope.IsRoot, scope.CurrentActionIndex, scope.CurrentActionContextId, scope.CurrentActionId,
                handle.RuntimeId, handle.Generation, handle.RootContextId);
        }

        internal MobaMotionHitTriggerRuntime ToRuntime() => new MobaMotionHitTriggerRuntime(
            TriggerId, SourceActorId, SourceConfigId,
            new MobaEffectExecutionScopeSnapshot(EffectContextId, EffectConfigId, ScopeTriggerId,
                ScopeSourceActorId, ScopeTargetActorId, IsRoot, CurrentActionIndex, CurrentActionContextId, CurrentActionId),
            new MobaSkillCastRuntimeHandle(SkillRuntimeId, SkillRuntimeGeneration, SkillRuntimeRootContextId));

        internal void Validate()
        {
            if (Equals(default(MobaMotionHitTriggerRollbackState))) return;
            if (!ToRuntime().IsValid ||
                (SkillRuntimeId != 0 || SkillRuntimeGeneration != 0 || SkillRuntimeRootContextId != 0) &&
                !new MobaSkillCastRuntimeHandle(SkillRuntimeId, SkillRuntimeGeneration, SkillRuntimeRootContextId).IsValid)
                throw new InvalidOperationException("Invalid Motion hit-trigger qualification data.");
        }
    }
}
