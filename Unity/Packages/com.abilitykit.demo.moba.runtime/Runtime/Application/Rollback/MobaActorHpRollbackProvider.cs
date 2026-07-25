using System;
using MemoryPack;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Attributes.Core;
using AbilityKit.Core.Pooling;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Demo.Moba.Rollback
{
    /// <summary>
    /// HP（血量基础值）回滚状态提供者。
    ///
    /// 在预测回滚模型下，客户端预测一帧后 HP 基础值可能变化；回滚时需要恢复到快照时的基础值。
    /// 本 provider 记录和恢复每个 actor 的 HP BaseValue（不含 modifier 叠加层）。
    ///
    /// 设计决策（2026-07-24）：
    /// - 只回滚 BaseValue，不回滚 modifier 列表。原因：AttributeGroup 没有 SetValue（直接设当前值）API，
    ///   当前值 = BaseValue × modifier 叠加。modifier 由 Buff 系统管理，Buff 的回滚是独立 provider 的职责。
    /// - 在 RemoteDriven（服务端权威 + 客户端预测）模式下，回滚后重新模拟 + 权威帧覆盖能最终纠正
    ///   modifier 层的临时不一致。
    /// - 如果未来需要精确的 modifier 回滚，可扩展为记录 BaseValue + modifier 快照，但需要
    ///   AttributeGroup 支持 RestoreSnapshot API 或在 provider 内部做 modifier diff。
    /// </summary>
    public sealed class MobaActorHpRollbackProvider : IRollbackStateProvider
    {
        public const int DefaultKey = 10002;

        private static readonly ObjectPool<List<MobaActorHpRollbackEntry>> s_entryListPool = Pools.GetPool(
            createFunc: () => new List<MobaActorHpRollbackEntry>(16),
            onRelease: list => list.Clear(),
            defaultCapacity: 8,
            maxSize: 64,
            collectionCheck: false);

        private readonly MobaActorRegistry _registry;

        public MobaActorHpRollbackProvider(MobaActorRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public int Key => DefaultKey;

        public string Name => "ActorHp";

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
                    if (e == null) continue;
                    if (!e.hasAttributeGroup) continue;

                    var group = e.attributeGroup.Group;
                    if (group == null) continue;

                    var hpInst = group.GetOrCreate(MobaAttributeIds.HP);
                    entries.Add(new MobaActorHpRollbackEntry(actorId, hpInst.BaseValue));
                }

                entries.Sort((a, b) => a.ActorId.CompareTo(b.ActorId));
                var payloadEntries = entries.Count == 0 ? Array.Empty<MobaActorHpRollbackEntry>() : entries.ToArray();
                return MemoryPackSerializer.Serialize(new MobaActorHpRollbackPayload(1, payloadEntries));
            }
            finally
            {
                s_entryListPool.Release(entries);
            }
        }

        public void ImportState(FrameIndex frame, byte[] payload)
        {
            if (payload == null || payload.Length == 0) return;

            var p = MemoryPackSerializer.Deserialize<MobaActorHpRollbackPayload>(payload);
            if (p.Entries == null || p.Entries.Length == 0) return;

            for (int i = 0; i < p.Entries.Length; i++)
            {
                var it = p.Entries[i];
                if (_registry.TryGet(it.ActorId, out var e) && e != null && e.hasAttributeGroup)
                {
                    var group = e.attributeGroup.Group;
                    if (group != null)
                    {
                        group.SetBase(MobaAttributeIds.HP, it.BaseHp);
                    }
                }
            }
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaActorHpRollbackPayload
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly MobaActorHpRollbackEntry[] Entries;

        [MemoryPackConstructor]
        public MobaActorHpRollbackPayload(int version, MobaActorHpRollbackEntry[] entries)
        {
            Version = version;
            Entries = entries;
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaActorHpRollbackEntry
    {
        [MemoryPackOrder(0)] public readonly int ActorId;
        [MemoryPackOrder(1)] public readonly float BaseHp;

        public MobaActorHpRollbackEntry(int actorId, float baseHp)
        {
            ActorId = actorId;
            BaseHp = baseHp;
        }
    }
}
