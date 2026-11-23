using System;
using MemoryPack;

namespace AbilityKit.Ability.StateSync
{
    /// <summary>
    /// 封装调用方提供的本地检查点数据，不复制数组，也不检查内容。
    /// 调用方须交付独占且不再写入的数组；读取方只能将其视为只读。
    /// </summary>
    public sealed class RollbackState : IRollbackState
    {
        /// <summary>
        /// 返回构造时指定的状态种类标识。
        /// </summary>
        public int SnapshotKey { get; }
        /// <summary>
        /// 返回持有的原始数组；未装入数据时可能为空引用。不得修改返回数组。
        /// </summary>
        public byte[] Data { get; private set; }

        /// <summary>
        /// 创建仅有种类标识、不含数据的状态对象。
        /// </summary>
        public RollbackState(int snapshotKey)
        {
            SnapshotKey = snapshotKey;
        }

        /// <summary>
        /// 使用给定种类标识和原始数组创建状态对象，不复制数据。
        /// </summary>
        public RollbackState(int snapshotKey, byte[] data)
        {
            SnapshotKey = snapshotKey;
            Data = data;
        }

        /// <summary>
        /// 返回当前持有的数组；无数据时返回空数组，不创建副本。
        /// </summary>
        public byte[] Serialize()
        {
            return Data ?? Array.Empty<byte>();
        }

        /// <summary>
        /// 引用给定的本地记录而不复制或修改；空引用转换为空数组。
        /// </summary>
        public void Deserialize(byte[] data)
        {
            Data = data ?? Array.Empty<byte>();
        }
    }

    /// <summary>
    /// 本地实体字段的可变检查点适配器；不负责验证实体代际或创建实体。
    /// </summary>
    public sealed class EntityRollbackState : IRollbackState
    {
        /// <summary>
        /// 返回构造时从初始实体标识计算的键，不随恢复后的实体标识更新。
        /// </summary>
        public int SnapshotKey => _snapshotKey;
        private readonly int _snapshotKey;

        /// <summary>
        /// 本条记录的实体标识；还原字段时不校验与创建者匹配。
        /// </summary>
        public long EntityId;
        /// <summary>
        /// 业务填入的位置值，不在适配器内换算坐标或单位。
        /// </summary>
        public Snapshot.Vec3 position;
        /// <summary>
        /// 业务填入的旋转值，不自动归一化。
        /// </summary>
        public Snapshot.Quat rotation;
        /// <summary>
        /// 业务填入的速度值，单位由业务约定。
        /// </summary>
        public Snapshot.Vec3 velocity;
        /// <summary>
        /// 业务填入的生命比例编码，不校验范围或映射。
        /// </summary>
        public byte healthPercent;
        /// <summary>
        /// 业务定义的状态位集合。
        /// </summary>
        public uint StateFlags;
        /// <summary>
        /// 业务定义的活动能力位掩码。
        /// </summary>
        public long ActiveAbilityMask;
        /// <summary>
        /// 业务定义的队伍标识。
        /// </summary>
        public int TeamId;
        /// <summary>
        /// 业务定义的控制状态位集合。
        /// </summary>
        public byte ControlFlags;

        /// <summary>
        /// 使用初始实体标识设置检查点键；其他字段由业务填充。
        /// </summary>
        public EntityRollbackState(long entityId)
        {
            _snapshotKey = entityId.GetHashCode();
            EntityId = entityId;
        }

        /// <summary>
        /// 根据当前字段值生成独立的本地检查点记录。
        /// </summary>
        public byte[] Serialize()
        {
            return MemoryPackSerializer.Serialize(new EntityRollbackStatePayload(
                EntityId, position, rotation, velocity,
                healthPercent, StateFlags, ActiveAbilityMask, TeamId, ControlFlags));
        }

        /// <summary>
        /// 空输入不修改现有字段；非空输入先还原记录，再按记录覆盖字段，错误向调用方传播。
        /// </summary>
        public void Deserialize(byte[] data)
        {
            if (data == null || data.Length == 0) return;

            var p = MemoryPackSerializer.Deserialize<EntityRollbackStatePayload>(data);
            EntityId = p.EntityId;
            position = p.Position;
            rotation = p.Rotation;
            velocity = p.Velocity;
            healthPercent = p.HealthPercent;
            StateFlags = p.StateFlags;
            ActiveAbilityMask = p.ActiveAbilityMask;
            TeamId = p.TeamId;
            ControlFlags = p.ControlFlags;
        }
    }

    /// <summary>
    /// 本地实体检查点的值记录；字段顺序受现有持久化兼容约束，不是权威世界快照。
    /// </summary>
    [MemoryPackable]
    public readonly partial struct EntityRollbackStatePayload
    {
        /// <summary>
        /// 被记录的实体标识，不附带代际校验。
        /// </summary>
        [MemoryPackOrder(0)] public readonly long EntityId;
        /// <summary>
        /// 捕获时的位置值。
        /// </summary>
        [MemoryPackOrder(1)] public readonly Snapshot.Vec3 Position;
        /// <summary>
        /// 捕获时的旋转值。
        /// </summary>
        [MemoryPackOrder(2)] public readonly Snapshot.Quat Rotation;
        /// <summary>
        /// 捕获时的速度值。
        /// </summary>
        [MemoryPackOrder(3)] public readonly Snapshot.Vec3 Velocity;
        /// <summary>
        /// 业务提供的生命比例编码。
        /// </summary>
        [MemoryPackOrder(4)] public readonly byte HealthPercent;
        /// <summary>
        /// 业务提供的状态位集合。
        /// </summary>
        [MemoryPackOrder(5)] public readonly uint StateFlags;
        /// <summary>
        /// 业务提供的活动能力位掩码。
        /// </summary>
        [MemoryPackOrder(6)] public readonly long ActiveAbilityMask;
        /// <summary>
        /// 业务提供的队伍标识。
        /// </summary>
        [MemoryPackOrder(7)] public readonly int TeamId;
        /// <summary>
        /// 业务提供的控制状态位集合。
        /// </summary>
        [MemoryPackOrder(8)] public readonly byte ControlFlags;

        /// <summary>
        /// 保存给定字段原值，不进行归一化、范围或标识校验。
        /// </summary>
        public EntityRollbackStatePayload(
            long entityId,
            Snapshot.Vec3 position,
            Snapshot.Quat rotation,
            Snapshot.Vec3 velocity,
            byte healthPercent,
            uint stateFlags,
            long activeAbilityMask,
            int teamId,
            byte controlFlags)
        {
            EntityId = entityId;
            Position = position;
            Rotation = rotation;
            Velocity = velocity;
            HealthPercent = healthPercent;
            StateFlags = stateFlags;
            ActiveAbilityMask = activeAbilityMask;
            TeamId = teamId;
            ControlFlags = controlFlags;
        }
    }
}
