using System.Collections.Generic;
using AbilityKit.Deterministic;

namespace AbilityKit.Demo.Moba.Components
{
    public readonly struct ShieldContainerView
    {
        public ShieldContainerView(int layerCount, int nextInstanceId, Fixed64 totalRemaining, bool dirty)
        {
            LayerCount = layerCount;
            NextInstanceId = nextInstanceId;
            TotalRemaining = totalRemaining;
            Dirty = dirty;
        }

        public int LayerCount { get; }
        public int NextInstanceId { get; }
        public Fixed64 TotalRemaining { get; }
        public bool Dirty { get; }
    }

    /// <summary>
    /// Read-only projection for UI, snapshots and diagnostics. It deliberately contains
    /// values, ids and versions only; no mutable layer, pool or member reference crosses
    /// the shield service boundary.
    /// </summary>
    public readonly struct ShieldPresentationSnapshot
    {
        public ShieldPresentationSnapshot(
            int actorId,
            Fixed64 totalPersonalValue,
            Fixed64 totalSharedValue,
            Fixed64 totalVisibleValue,
            Fixed64 visibleCapacity,
            int primaryShieldId,
            int primaryPoolId,
            int primaryPoolVersion,
            int layerCount,
            bool hasShared)
        {
            ActorId = actorId;
            TotalPersonalValue = totalPersonalValue;
            TotalSharedValue = totalSharedValue;
            TotalVisibleValue = totalVisibleValue;
            VisibleCapacity = visibleCapacity;
            PrimaryShieldId = primaryShieldId;
            PrimaryPoolId = primaryPoolId;
            PrimaryPoolVersion = primaryPoolVersion;
            LayerCount = layerCount;
            HasShared = hasShared;
        }

        public int ActorId { get; }
        public Fixed64 TotalPersonalValue { get; }
        public Fixed64 TotalSharedValue { get; }
        public Fixed64 TotalVisibleValue { get; }
        public Fixed64 VisibleCapacity { get; }
        public int PrimaryShieldId { get; }
        public int PrimaryPoolId { get; }
        public int PrimaryPoolVersion { get; }
        public int LayerCount { get; }
        public bool HasShared { get; }
        public bool HasShield => TotalVisibleValue > Fixed64.Zero;
    }

    // 2026-10-12：ShieldContainerComponent/SharedShieldPoolsComponent 两个 [Actor] 组件定义已删除
    // （Jenny 死后新增、从未生成 API、零消费者——护盾状态实际在 MobaShieldService）。
    // 护盾 ECS 化待重新规划后再以正确形态落地。

    public sealed class ShieldContainer
    {
        public List<ShieldLayer> Layers;
        public int NextInstanceId;
        public Fixed64 TotalRemaining;
        public bool Dirty;
    }

    /// <summary>
    /// 护盾数值字段（CurrentValue/MaxValue/InitialValue/AbsorbRatio/TransferRatio）为 Q32.32 定点，
    /// 参与伤害吸收的确定性算术；构造入口（配置 PlanAction）做单次 float 边界换算。
    /// </summary>
    public sealed class ShieldLayer
    {
        public int InstanceId;
        public int ShieldId;
        public int SourceActorId;
        public int OwnerActorId;
        public int TargetActorId;
        public long SourceContextId;
        public long RootContextId;
        public long OwnerContextId;

        public int SharedPoolId;
        public int SharedPoolMemberId;
        public bool UsesSharedPoolValue;

        public int TransferredFromActorId;
        public int TransferredToActorId;
        public int TransferredAtFrame;
        public Fixed64 TransferRatio;

        public Fixed64 CurrentValue;
        public Fixed64 MaxValue;
        public Fixed64 InitialValue;
        public Fixed64 AbsorbRatio;

        public int Priority;
        public int DamageTypeMask;
        public int StartFrame;
        public int ExpireFrame;
        public bool RemoveWhenDepleted;

        public ShieldStackingPolicy StackingPolicy;
        public ShieldConsumePolicy ConsumePolicy;
        public ShieldSharePolicy SharePolicy;
        public ShieldTransferPolicy TransferPolicy;
    }

    public sealed class SharedShieldPoolContainer
    {
        public Dictionary<int, SharedShieldPool> Pools;
        public int NextPoolId;
        public bool Dirty;
    }

    public sealed class SharedShieldPool
    {
        public int PoolId;
        public int ShieldId;
        public int SourceActorId;
        public int OwnerActorId;
        public long SourceContextId;
        public long RootContextId;
        public long OwnerContextId;

        public List<SharedShieldPoolMember> Members;
        public Fixed64 CurrentValue;
        public Fixed64 MaxValue;
        public Fixed64 InitialValue;
        public Fixed64 AbsorbRatio;
        public int Version;
        public int NextMemberId;

        public int Priority;
        public int DamageTypeMask;
        public int StartFrame;
        public int ExpireFrame;
        public bool RemoveWhenDepleted;

        public ShieldStackingPolicy StackingPolicy;
        public ShieldConsumePolicy ConsumePolicy;
        public ShieldSharePolicy SharePolicy;
        public ShieldTransferPolicy TransferPolicy;
    }

    public sealed class SharedShieldPoolMember
    {
        public int MemberId;
        public int ActorId;
        public Fixed64 Weight;
        public Fixed64 MaxConsumeValue;
        public Fixed64 ConsumedValue;
        public int JoinedFrame;
        public int LeftFrame;
        public bool Active;
    }

    /// <summary>只读的共享池摘要；余额只从 Pool 读取，成员绑定不复制余额。</summary>
    public readonly struct SharedShieldPoolView
    {
        public SharedShieldPoolView(int poolId, int shieldId, Fixed64 currentValue, Fixed64 maxValue,
            int version, int memberCount, int activeMemberCount, int startFrame, int expireFrame)
        {
            PoolId = poolId;
            ShieldId = shieldId;
            CurrentValue = currentValue;
            MaxValue = maxValue;
            Version = version;
            MemberCount = memberCount;
            ActiveMemberCount = activeMemberCount;
            StartFrame = startFrame;
            ExpireFrame = expireFrame;
        }

        public int PoolId { get; }
        public int ShieldId { get; }
        public Fixed64 CurrentValue { get; }
        public Fixed64 MaxValue { get; }
        public int Version { get; }
        public int MemberCount { get; }
        public int ActiveMemberCount { get; }
        public int StartFrame { get; }
        public int ExpireFrame { get; }
    }

    public enum ShieldStackingPolicy
    {
        Independent = 0,
        MergeSameShieldAndSource = 1,
        RefreshSameShieldAndSource = 2,
        ReplaceLowerPriority = 3,
    }

    public enum ShieldConsumePolicy
    {
        PriorityThenOldest = 0,
        PriorityThenNewest = 1,
        OldestFirst = 2,
        NewestFirst = 3,
    }

    public enum ShieldSharePolicy
    {
        None = 0,
        SharedTotalPool = 1,
        WeightedMemberShare = 2,
        TargetPersonalThenShared = 3,
        LowestHpMemberFirst = 4,
    }

    public enum ShieldTransferPolicy
    {
        None = 0,
        MoveRemaining = 1,
        SplitRemaining = 2,
        CopySnapshot = 3,
        ReturnToOwnerOnExpire = 4,
    }
}
