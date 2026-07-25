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
    /// 技能冷却回滚状态提供者。
    ///
    /// 在预测回滚模型下，客户端预测施法后 CD 会被扣；回滚时需要恢复到快照时的 CD 时间戳，
    /// 否则回滚后的重模拟会"多扣"CD（让玩家在回滚前还没冷却好的技能在回滚后看起来已冷却好）。
    ///
    /// 设计（2026-07-24）：
    /// - 遍历每个 actor 的 SkillLoadout.ActiveSkills，记录每个 slot 的 CooldownEndTimeMs + CooldownDurationMs。
    /// - 回滚时直接恢复这两个字段。
    /// - ActiveSkillRuntime 是 mutable class，直接修改字段即可，无需重建。
    /// </summary>
    public sealed class MobaSkillCooldownRollbackProvider : IRollbackStateProvider
    {
        public const int DefaultKey = 10004;

        private static readonly ObjectPool<List<MobaSkillCooldownRollbackEntry>> s_entryListPool = Pools.GetPool(
            createFunc: () => new List<MobaSkillCooldownRollbackEntry>(16),
            onRelease: list => list.Clear(),
            defaultCapacity: 8,
            maxSize: 64,
            collectionCheck: false);

        private readonly MobaActorRegistry _registry;

        public MobaSkillCooldownRollbackProvider(MobaActorRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public int Key => DefaultKey;
        public string Name => "SkillCooldown";

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
                    if (e == null || !e.hasSkillLoadout) continue;

                    var skills = e.skillLoadout.ActiveSkills;
                    if (skills == null) continue;

                    for (int slot = 0; slot < skills.Length; slot++)
                    {
                        var skill = skills[slot];
                        if (skill == null) continue;
                        entries.Add(new MobaSkillCooldownRollbackEntry(
                            actorId, slot + 1, skill.SkillId,
                            skill.CooldownEndTimeMs, skill.CooldownDurationMs));
                    }
                }

                entries.Sort((a, b) =>
                {
                    int c = a.ActorId.CompareTo(b.ActorId);
                    return c != 0 ? c : a.SkillSlot.CompareTo(b.SkillSlot);
                });

                var arr = entries.Count == 0 ? Array.Empty<MobaSkillCooldownRollbackEntry>() : entries.ToArray();
                return MemoryPackSerializer.Serialize(new MobaSkillCooldownRollbackPayload(1, arr));
            }
            finally
            {
                s_entryListPool.Release(entries);
            }
        }

        public void ImportState(FrameIndex frame, byte[] payload)
        {
            if (payload == null || payload.Length == 0) return;

            var p = MemoryPackSerializer.Deserialize<MobaSkillCooldownRollbackPayload>(payload);
            if (p.Entries == null || p.Entries.Length == 0) return;

            for (int i = 0; i < p.Entries.Length; i++)
            {
                var it = p.Entries[i];
                if (!_registry.TryGet(it.ActorId, out var e) || e == null || !e.hasSkillLoadout) continue;

                var skills = e.skillLoadout.ActiveSkills;
                if (skills == null) continue;

                var slotIndex = it.SkillSlot - 1;
                if (slotIndex < 0 || slotIndex >= skills.Length) continue;

                var skill = skills[slotIndex];
                if (skill != null && skill.SkillId == it.SkillId)
                {
                    skill.CooldownEndTimeMs = it.CooldownEndTimeMs;
                    skill.CooldownDurationMs = it.CooldownDurationMs;
                }
            }
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaSkillCooldownRollbackPayload
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly MobaSkillCooldownRollbackEntry[] Entries;

        [MemoryPackConstructor]
        public MobaSkillCooldownRollbackPayload(int version, MobaSkillCooldownRollbackEntry[] entries)
        {
            Version = version;
            Entries = entries;
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaSkillCooldownRollbackEntry
    {
        [MemoryPackOrder(0)] public readonly int ActorId;
        [MemoryPackOrder(1)] public readonly int SkillSlot;
        [MemoryPackOrder(2)] public readonly int SkillId;
        [MemoryPackOrder(3)] public readonly long CooldownEndTimeMs;
        [MemoryPackOrder(4)] public readonly int CooldownDurationMs;

        public MobaSkillCooldownRollbackEntry(int actorId, int skillSlot, int skillId,
                                 long cooldownEndTimeMs, int cooldownDurationMs)
        {
            ActorId = actorId;
            SkillSlot = skillSlot;
            SkillId = skillId;
            CooldownEndTimeMs = cooldownEndTimeMs;
            CooldownDurationMs = cooldownDurationMs;
        }
    }
}
