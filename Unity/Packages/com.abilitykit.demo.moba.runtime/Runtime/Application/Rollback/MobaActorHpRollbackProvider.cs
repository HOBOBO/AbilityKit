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
    /// 仅恢复属性组 HP BaseValue；资源 Current 与组件存在性由生成组件快照唯一恢复。
    /// v3 拒绝旧载荷，不重放伤害或治疗事件，Modifier 仍由原生命周期所有者恢复。
    /// </summary>
    [MobaRollbackProvider(DefaultKey)]
    public sealed class MobaActorHpRollbackProvider : IRollbackStateProvider, IRollbackStatePreflightProvider
    {
        /// <summary>
        /// 属性 HP 基线在协调器中的稳定键。
        /// </summary>
        public const int DefaultKey = 10002;
        /// <summary>
        /// 仅包含 HP BaseValue 的载荷版本，拒绝旧资源混合布局。
        /// </summary>
        public const int CurrentPayloadVersion = 3;

        private static readonly ObjectPool<List<MobaActorHpRollbackEntry>> s_entryListPool = Pools.GetPool(
            createFunc: () => new List<MobaActorHpRollbackEntry>(16),
            onRelease: list => list.Clear(),
            defaultCapacity: 8,
            maxSize: 64,
            collectionCheck: false);

        private readonly MobaActorRegistry _registry;

        /// <summary>
        /// 使用世界内 Actor 注册表解析属性恢复目标。
        /// </summary>
        public MobaActorHpRollbackProvider(MobaActorRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>
        /// 获取稳定 Provider 键。
        /// </summary>
        public int Key => DefaultKey;

        /// <summary>
        /// 获取恢复诊断名称。
        /// </summary>
        public string Name => "ActorHp";

        /// <summary>
        /// 捕获当前属性 HP 基线。
        /// </summary>
        public byte[] Export(FrameIndex frame)
        {
            return ExportState(frame);
        }

        /// <summary>
        /// 验证并恢复属性 HP 基线，不写资源 Current。
        /// </summary>
        public void Import(FrameIndex frame, byte[] payload)
        {
            ImportState(frame, payload);
        }

        /// <summary>
        /// 按 ActorId 排序输出独立值载荷。
        /// </summary>
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
                return MemoryPackSerializer.Serialize(new MobaActorHpRollbackPayload(CurrentPayloadVersion, payloadEntries));
            }
            finally
            {
                s_entryListPool.Release(entries);
            }
        }

        /// <summary>
        /// 在结构回退前验证格式与字段；尚未由 Command 重建的 Actor 在导入时再解析。
        /// </summary>
        public void ValidateImport(FrameIndex frame, byte[] payload)
        {
            Read(payload);
        }

        private static MobaActorHpRollbackPayload Read(byte[] payload)
        {
            if (payload == null || payload.Length == 0) throw new InvalidOperationException("Missing ActorHp payload.");
            var p = MemoryPackSerializer.Deserialize<MobaActorHpRollbackPayload>(payload);
            if (p.Version != CurrentPayloadVersion)
            {
                throw new NotSupportedException(
                    $"Unsupported ActorHp rollback payload version: {p.Version}. Expected {CurrentPayloadVersion}.");
            }

            if (p.Entries == null) throw new InvalidOperationException("Missing ActorHp entries.");
            var previous = 0;
            foreach (var entry in p.Entries)
            {
                if (entry.ActorId <= previous || float.IsNaN(entry.BaseHp) || float.IsInfinity(entry.BaseHp))
                    throw new InvalidOperationException("Invalid ActorHp entry or ordering.");
                previous = entry.ActorId;
            }
            return p;
        }

        /// <summary>
        /// 先解析全部属性目标，再恢复 HP BaseValue，不触发伤害或治疗入口。
        /// </summary>
        public void ImportState(FrameIndex frame, byte[] payload)
        {
            var p = Read(payload);
            var targets = new AttributeGroup[p.Entries.Length];
            for (var i = 0; i < targets.Length; i++)
            {
                if (!_registry.TryGet(p.Entries[i].ActorId, out var entity) || entity == null ||
                    !entity.hasAttributeGroup || entity.attributeGroup.Group == null)
                    throw new InvalidOperationException("ActorHp target attribute group is missing.");
                targets[i] = entity.attributeGroup.Group;
            }

            for (int i = 0; i < p.Entries.Length; i++)
            {
                var it = p.Entries[i];
                targets[i].SetBase(MobaAttributeIds.HP, it.BaseHp);
            }
        }
    }

    /// <summary>
    /// HP 属性基线的版本化值载荷。
    /// </summary>
    [MemoryPackable]
    public readonly partial struct MobaActorHpRollbackPayload
    {
        /// <summary>
        /// 载荷布局版本。
        /// </summary>
        [MemoryPackOrder(0)] public readonly int Version;
        /// <summary>
        /// 按 ActorId 升序排列的基线条目。
        /// </summary>
        [MemoryPackOrder(1)] public readonly MobaActorHpRollbackEntry[] Entries;

        /// <summary>
        /// 构造显式版本的 HP 属性载荷。
        /// </summary>
        [MemoryPackConstructor]
        public MobaActorHpRollbackPayload(int version, MobaActorHpRollbackEntry[] entries)
        {
            Version = version;
            Entries = entries;
        }
    }

    /// <summary>
    /// 一个 Actor 的 HP 属性基线，不包含资源当前值。
    /// </summary>
    [MemoryPackable]
    public readonly partial struct MobaActorHpRollbackEntry
    {
        /// <summary>
        /// 稳定 Actor 身份。
        /// </summary>
        [MemoryPackOrder(0)] public readonly int ActorId;
        /// <summary>
        /// 属性组中的 HP BaseValue。
        /// </summary>
        [MemoryPackOrder(1)] public readonly float BaseHp;

        /// <summary>
        /// 构造 Actor 的 HP 属性基线条目。
        /// </summary>
        public MobaActorHpRollbackEntry(int actorId, float baseHp)
        {
            ActorId = actorId;
            BaseHp = baseHp;
        }
    }
}
