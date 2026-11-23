using AbilityKit.Ability.StateSync.Buffer;
using AbilityKit.Ability.StateSync.Snapshot;
using AbilityKit.Core.Buffers;

namespace AbilityKit.Ability.StateSync.Samples
{
    /// <summary>
    /// 演示在宿主完成一次本地状态提交后，保留独立的元数据快照历史。
    /// </summary>
    public static class LocalSnapshotHistoryExample
    {
        /// <summary>
        /// 用相同调用验证默认后端或环形后端的帧零、覆盖、剪裁和所有权行为。
        /// </summary>
        public static bool Run(bool useRingStorage = false)
        {
            var history = useRingStorage
                ? new SnapshotBuffer(new RingFrameIndexedBuffer<WorldStateSnapshot>(2))
                : new SnapshotBuffer(maxBufferSize: 2);

            var published = new WorldStateSnapshot { Frame = 0, WorldFlags = 1 };
            history.Store(published.Frame, published);
            published.WorldFlags = 7;
            if (!history.TryGet(0, out var first) || first.WorldFlags != 1)
                return false;
            first.WorldFlags = 9;
            if (!history.TryGet(0, out var retained) || retained.WorldFlags != 1)
                return false;

            history.Store(4, new WorldStateSnapshot { Frame = 4, WorldFlags = 2 });
            history.Store(4, new WorldStateSnapshot { Frame = 4, WorldFlags = 3 });
            history.Store(8, new WorldStateSnapshot { Frame = 8 });

            return !history.TryGet(0, out _) && !history.TryGet(1, out _) &&
                history.Count == 2 && history.TryGet(4, out var updated) && updated.WorldFlags == 3 &&
                history.GetLatestFrame() == 8;
        }
    }
}
