using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host
{
    /// <summary>
    /// 将宿主快照提供者的输出适配为只含快照的帧片段，不推进世界或提交恢复游标。
    /// </summary>
    public static class SnapshotProviderDrain
    {
        /// <summary>
        /// 优先使用批量收集，否则逐条读取；按提供顺序同步交付，不去重或复制载荷。
        /// </summary>
        /// <param name="provider">快照提供者；为空时不读取。</param>
        /// <param name="worldId">写入帧片段的世界标识。</param>
        /// <param name="frame">向提供者查询并写入帧片段的帧号。</param>
        /// <param name="maxSnapshotsPerStep">批量收集的预算或单条查询的次数上限；非正时不读取。</param>
        /// <param name="feed">接收帧片段的回调；为空时不读取。</param>
        /// <returns>交付数量；未读取时返回零。</returns>
        /// <remarks>
        /// 批量提供者负责遵守收集预算。提供者或回调抛出的异常向上传递，已交付的帧片段不会撤销。
        /// </remarks>
        public static int DrainSnapshots(
            IWorldStateSnapshotProvider provider,
            WorldId worldId,
            FrameIndex frame,
            int maxSnapshotsPerStep,
            Action<FramePacket> feed)
        {
            if (provider == null) return 0;
            if (feed == null) return 0;
            if (maxSnapshotsPerStep <= 0) return 0;

            if (provider is IWorldStateSnapshotBatchProvider batchProvider)
            {
                var snapshots = new List<WorldStateSnapshot>(Math.Min(maxSnapshotsPerStep, 32));
                var count = batchProvider.CollectSnapshots(frame, snapshots, maxSnapshotsPerStep);
                var drained = Math.Min(count, snapshots.Count);
                for (var index = 0; index < drained; index++)
                {
                    feed(new FramePacket(worldId, frame, Array.Empty<PlayerInputCommand>(), snapshots[index]));
                }

                return drained;
            }

            var fallbackDrained = 0;
            for (var index = 0; index < maxSnapshotsPerStep; index++)
            {
                if (!provider.TryGetSnapshot(frame, out var snapshot)) break;
                feed(new FramePacket(worldId, frame, Array.Empty<PlayerInputCommand>(), snapshot));
                fallbackDrained++;
            }

            return fallbackDrained;
        }
    }
}
