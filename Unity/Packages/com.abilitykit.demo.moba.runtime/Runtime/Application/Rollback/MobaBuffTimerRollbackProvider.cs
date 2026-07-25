using System;
using MemoryPack;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Core.Pooling;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Demo.Moba.Rollback
{
    /// <summary>
    /// Buff 计时器回滚状态提供者。
    ///
    /// 在预测回滚模型下，客户端预测推进时 Buff 的 Remaining/IntervalRemaining 计时器会减少；
    /// 回滚时需要恢复到快照时的计时器值，否则回滚后的重模拟会"少扣"Buff 时间。
    ///
    /// 设计决策（2026-07-24）：
    /// - 只回滚计时器（Remaining + IntervalRemainingSeconds + StackCount），不重建 Buff 列表。
    ///   原因：完整重建需要序列化 BuffRuntime 的所有字段（Continuous / ModifierBindings /
    ///   TagRequirements / SkillRuntimeHandle 等），结构复杂且可能引入循环引用。
    /// - 计时器回滚覆盖了最常见的回滚场景："Buff 还剩 2 秒"——回滚后应该还是 2 秒。
    /// - Buff 列表本身（哪些 Buff 在飞）由 BuffLifecycleExecutor 管理；如果回滚后 Buff 列表
    ///   需要也恢复，需要额外的 Buff 列表快照 provider（后续工作）。
    /// </summary>
    public sealed class MobaBuffTimerRollbackProvider : IRollbackStateProvider
    {
        public const int DefaultKey = 10003;

        private static readonly ObjectPool<List<MobaBuffTimerRollbackEntry>> s_entryListPool = Pools.GetPool(
            createFunc: () => new List<MobaBuffTimerRollbackEntry>(16),
            onRelease: list => list.Clear(),
            defaultCapacity: 8,
            maxSize: 64,
            collectionCheck: false);

        private readonly MobaActorRegistry _registry;

        public MobaBuffTimerRollbackProvider(MobaActorRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public int Key => DefaultKey;
        public string Name => "BuffTimer";

        public byte[] Export(FrameIndex frame)
        {
            return ExportState(frame);
        }

        public void Import(FrameIndex frame, byte[] payload)
        {
            ImportState(frame, payload);
        }
 
        public byte[] ExportState(FrameIndex frame)
        {
            var entries = s_entryListPool.Get();
            try
            {
                foreach (var kv in _registry.Entries)
                {
                    var actorId = kv.Key;
                    var e = kv.Value;
                    if (e == null || !e.hasBuffs) continue;

                    var active = e.buffs.Active;
                    if (active == null || active.Count == 0) continue;

                    for (int i = 0; i < active.Count; i++)
                    {
                        var buff = active[i];
                        if (buff == null) continue;
                        entries.Add(new MobaBuffTimerRollbackEntry(actorId, buff.BuffId, buff.Remaining,
                                                       buff.IntervalRemainingSeconds, buff.StackCount));
                    }
                }

                entries.Sort((a, b) =>
                {
                    int c = a.ActorId.CompareTo(b.ActorId);
                    return c != 0 ? c : a.BuffId.CompareTo(b.BuffId);
                });

                var arr = entries.Count == 0 ? Array.Empty<MobaBuffTimerRollbackEntry>() : entries.ToArray();
                return MemoryPackSerializer.Serialize(new MobaBuffTimerRollbackPayload(1, arr));
            }
            finally
            {
                s_entryListPool.Release(entries);
            }
        }

        public void ImportState(FrameIndex frame, byte[] payload)
        {
            if (payload == null || payload.Length == 0) return;

            var p = MemoryPackSerializer.Deserialize<MobaBuffTimerRollbackPayload>(payload);
            if (p.Entries == null || p.Entries.Length == 0) return;

            for (int i = 0; i < p.Entries.Length; i++)
            {
                var it = p.Entries[i];
                if (!_registry.TryGet(it.ActorId, out var e) || e == null || !e.hasBuffs) continue;

                var active = e.buffs.Active;
                if (active == null) continue;

                // 找到匹配的 Buff（同 BuffId），恢复计时器
                for (int j = 0; j < active.Count; j++)
                {
                    var buff = active[j];
                    if (buff != null && buff.BuffId == it.BuffId)
                    {
                        buff.Remaining = it.Remaining;
                        buff.IntervalRemainingSeconds = it.IntervalRemainingSeconds;
                        buff.StackCount = it.StackCount;
                        break;
                    }
                }
            }
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaBuffTimerRollbackPayload
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly MobaBuffTimerRollbackEntry[] Entries;

        [MemoryPackConstructor]
        public MobaBuffTimerRollbackPayload(int version, MobaBuffTimerRollbackEntry[] entries)
        {
            Version = version;
            Entries = entries;
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaBuffTimerRollbackEntry
    {
        [MemoryPackOrder(0)] public readonly int ActorId;
        [MemoryPackOrder(1)] public readonly int BuffId;
        [MemoryPackOrder(2)] public readonly float Remaining;
        [MemoryPackOrder(3)] public readonly float IntervalRemainingSeconds;
        [MemoryPackOrder(4)] public readonly int StackCount;

        public MobaBuffTimerRollbackEntry(int actorId, int buffId, float remaining,
                              float intervalRemaining, int stackCount)
        {
            ActorId = actorId;
            BuffId = buffId;
            Remaining = remaining;
            IntervalRemainingSeconds = intervalRemaining;
            StackCount = stackCount;
        }
    }
}
