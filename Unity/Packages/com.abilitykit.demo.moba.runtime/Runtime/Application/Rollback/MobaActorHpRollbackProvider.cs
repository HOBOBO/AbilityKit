using System;
using MemoryPack;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Attributes.Core;
using AbilityKit.Core.Pooling;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Demo.Moba.Rollback
{
    /// <summary>
    /// HP 回滚状态提供者。
    ///
    /// 在预测回滚模型下，客户端预测一帧后 HP 可能变化；回滚时需要恢复到快照时的值。
    ///
    /// 设计决策：
    /// - 2026-07-24 初版只回滚 HP attribute BaseValue（不含 modifier 叠加层；modifier 由 Buff 回滚 provider 负责）。
    /// - 2026-08-15 v2：伤害/治疗实际落地在 ResourceContainer（定点 Q32.32），BaseValue 快照覆盖不到真实血量，
    ///   因此 v2 同时回滚 ResourceState.Current（raw long 存储）。BaseHp 字段保留 float（attribute 系统仍为 float 存储）。
    /// </summary>
    public sealed class MobaActorHpRollbackProvider : IRollbackStateProvider
    {
        public const int DefaultKey = 10002;
        private const int CurrentPayloadVersion = 2;

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
                    var currentRaw = 0L;
                    if (e.hasResourceContainer && e.resourceContainer.Value?.Map != null &&
                        e.resourceContainer.Value.Map.TryGetValue(ResourceType.Hp, out var hpState) && hpState != null)
                    {
                        currentRaw = hpState.Current.RawValue;
                    }

                    entries.Add(new MobaActorHpRollbackEntry(actorId, hpInst.BaseValue, currentRaw));
                }

                entries.Sort((a, b) => a.ActorId.CompareTo(b.ActorId));
                var payloadEntries = entries.Count == 0 ? Array.Empty<MobaActorHpRollbackEntry>() : entries.ToArray();
                return MemoryPackSerializer.Serialize(new MobaActorHpRollbackPayload(CurrentPayloadVersion, payloadEntries));
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
            if (p.Version != CurrentPayloadVersion)
            {
                throw new NotSupportedException(
                    $"Unsupported ActorHp rollback payload version: {p.Version}. Expected {CurrentPayloadVersion}.");
            }

            if (p.Entries == null || p.Entries.Length == 0) return;

            for (int i = 0; i < p.Entries.Length; i++)
            {
                var it = p.Entries[i];
                if (!_registry.TryGet(it.ActorId, out var e) || e == null) continue;

                if (e.hasAttributeGroup && e.attributeGroup.Group != null)
                {
                    e.attributeGroup.Group.SetBase(MobaAttributeIds.HP, it.BaseHp);
                }

                if (e.hasResourceContainer && e.resourceContainer.Value?.Map != null)
                {
                    if (e.resourceContainer.Value.Map.TryGetValue(ResourceType.Hp, out var hpState) && hpState != null)
                    {
                        hpState.Current = Fixed64.FromRaw(it.CurrentRaw);
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
        [MemoryPackOrder(2)] public readonly long CurrentRaw;

        public MobaActorHpRollbackEntry(int actorId, float baseHp, long currentRaw)
        {
            ActorId = actorId;
            BaseHp = baseHp;
            CurrentRaw = currentRaw;
        }
    }
}
