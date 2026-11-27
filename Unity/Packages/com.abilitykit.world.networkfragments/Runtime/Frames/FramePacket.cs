using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host
{
    /// <summary>
    /// 携带一个世界帧的输入片段和可选快照，不代表该帧的数据已经齐备。
    /// </summary>
    public sealed class FramePacket : ISnapshotEnvelope
    {
        /// <summary>
        /// 创建帧片段；空输入引用转换为空集合，不复制输入或快照载荷。
        /// </summary>
        public FramePacket(WorldId worldId, FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs, WorldStateSnapshot? snapshot)
        {
            WorldId = worldId;
            Frame = frame;
            Inputs = inputs ?? Array.Empty<PlayerInputCommand>();
            Snapshot = snapshot;
        }

        /// <summary>
        /// 片段所属的世界标识。
        /// </summary>
        public WorldId WorldId { get; }

        /// <summary>
        /// 片段对应的帧号，不表示确认帧或恢复游标。
        /// </summary>
        public FrameIndex Frame { get; }

        /// <summary>
        /// 借用的输入集合，可为空；交付后调用方应保持内容不变。
        /// </summary>
        public IReadOnlyList<PlayerInputCommand> Inputs { get; }

        /// <summary>
        /// 可选的世界快照；载荷由调用方负责保持有效且不变。
        /// </summary>
        public WorldStateSnapshot? Snapshot { get; }
    }
}
