#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using AbilityKit.Network.Runtime;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.View
{
    /// <summary>
    /// 从网关快照采集的远端权威样本，用于 <see cref="NetworkSyncModel.AuthoritativeInterpolation"/> 播放。
    /// 保存 actor 变换、packed 生命周期与组件负载，以及缓冲和插值所需的世界、帧与服务器 tick 元数据。
    /// <see cref="TimelineTicks"/> 对应快照的权威 <c>ServerTicks</c>。
    /// </summary>
    public sealed class ShooterRemoteSnapshotSample : IRemoteSnapshotSample, IReadOnlyList<ShooterGatewayActorSnapshot>
    {
        private readonly IReadOnlyList<ShooterGatewayActorSnapshot> _actors;
        private readonly int _excludedActorIndex;

        public ShooterRemoteSnapshotSample(
            ulong worldId,
            int frame,
            long serverTicks,
            IReadOnlyList<ShooterGatewayActorSnapshot> actors,
            ShooterPackedSnapshotPayload? packedSnapshot = null,
            int excludedActorId = -1)
        {
            WorldId = worldId;
            Frame = frame;
            ServerTicks = serverTicks;
            PackedSnapshot = packedSnapshot;
            _actors = actors ?? Array.Empty<ShooterGatewayActorSnapshot>();
            _excludedActorIndex = FindActorIndex(_actors, excludedActorId);
        }

        public ulong WorldId { get; }

        public int Frame { get; }

        public long ServerTicks { get; }

        public ShooterPackedSnapshotPayload? PackedSnapshot { get; }

        public IReadOnlyList<ShooterGatewayActorSnapshot> Actors => this;

        public int Count => _actors.Count - (_excludedActorIndex >= 0 ? 1 : 0);

        public ShooterGatewayActorSnapshot this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
                var sourceIndex = _excludedActorIndex >= 0 && index >= _excludedActorIndex ? index + 1 : index;
                return _actors[sourceIndex];
            }
        }

        public long TimelineTicks => ServerTicks;

        public IEnumerator<ShooterGatewayActorSnapshot> GetEnumerator()
        {
            for (var i = 0; i < Count; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private static int FindActorIndex(IReadOnlyList<ShooterGatewayActorSnapshot> actors, int actorId)
        {
            if (actorId <= 0)
            {
                return -1;
            }

            for (var i = 0; i < actors.Count; i++)
            {
                if (actors[i].ActorId == actorId)
                {
                    return i;
                }
            }

            return -1;
        }
    }

    /// <summary>
    /// 基于一对包围播放时间点的 <see cref="ShooterRemoteSnapshotSample"/> 样本构建插值后的
    /// <see cref="ShooterGatewaySnapshot"/>，让现有表现管线无需回滚即可渲染延迟且平滑插值的远端 actor 状态。
    ///
    /// 这是一个有状态、单消费者的投影器：它会跨调用复用内部 actor 列表与索引，避免稳定播放期逐帧分配。
    /// 产出的快照只在下一次 <see cref="Project"/> 调用前有效；表现管线会同步消费并拷贝所需字段，因此这是安全的。
    /// </summary>
    public sealed class ShooterRemoteSnapshotProjector
    {
        private readonly List<ShooterGatewayActorSnapshot> _actors = new List<ShooterGatewayActorSnapshot>();
        private readonly Dictionary<int, int> _fromIndexById = new Dictionary<int, int>();
        private readonly HashSet<int> _toActorIds = new HashSet<int>();
        private readonly List<int> _fromOnlyIndices = new List<int>();
        private int[] _fromIndexByToIndex = Array.Empty<int>();
        private ShooterRemoteSnapshotSample? _mappedFrom;
        private ShooterRemoteSnapshotSample? _mappedTo;

        /// <summary>
        /// 生成 actor 按 <paramref name="interpolation"/>.Alpha 在两个包围样本之间线性插值的快照。
        /// 两个样本都存在的 actor 会混合位置、旋转、速度和生命值；只存在于一侧的 actor 会特殊处理：
        /// 新生成的远端对象直接以权威姿态出现，消失中的远端对象则在中间帧保持最后姿态，避免插值中途闪烁消失。
        /// </summary>
        public ShooterGatewaySnapshot Project(in RemoteSnapshotInterpolation<ShooterRemoteSnapshotSample> interpolation)
        {
            var from = interpolation.From;
            var to = interpolation.To;
            float alpha = interpolation.Alpha;

            if (ReferenceEquals(from, to) || alpha <= 0f)
            {
                return BuildSnapshot(from, from.Actors);
            }

            if (alpha >= 1f)
            {
                return BuildSnapshot(to, to.Actors);
            }

            _actors.Clear();
            EnsureActorIndexMapping(from, to);

            // 输出每个目标 actor；若存在上一姿态则混合，否则新生成 actor 直接使用权威姿态。
            for (int i = 0; i < to.Actors.Count; i++)
            {
                var target = to.Actors[i];
                var fromIndex = _fromIndexByToIndex[i];
                if (fromIndex >= 0)
                {
                    var source = from.Actors[fromIndex];
                    _actors.Add(Lerp(in source, in target, alpha));
                }
                else
                {
                    _actors.Add(target);
                }
            }

            // 保留存在于 from 但不存在于 to 的 actor（两个样本之间消失）。
            // 中间帧保持最后姿态可避免单帧闪烁；播放越过 to 后，下一个样本的目标集合会移除它们。
            for (int i = 0; i < _fromOnlyIndices.Count; i++)
            {
                _actors.Add(from.Actors[_fromOnlyIndices[i]]);
            }

            return BuildSnapshot(to, _actors);
        }

        private static ShooterGatewaySnapshot BuildSnapshot(ShooterRemoteSnapshotSample meta, IReadOnlyList<ShooterGatewayActorSnapshot> actors)
        {
            var packed = meta.PackedSnapshot;
            var isFullSnapshot = packed.HasValue
                ? (packed.Value.SnapshotFlags & ShooterPackedSnapshotFlags.Full) != 0
                : true;
            var payloadOpCode = packed.HasValue
                ? ((packed.Value.SnapshotFlags & ShooterPackedSnapshotFlags.Delta) != 0
                    ? ShooterOpCodes.Snapshot.PackedStateDelta
                    : ShooterOpCodes.Snapshot.PackedState)
                : 0;

            return new ShooterGatewaySnapshot(
                meta.WorldId,
                meta.Frame,
                0d,
                meta.ServerTicks,
                isFullSnapshot,
                actors,
                payloadOpCode,
                packedSnapshot: packed);
        }

        private void EnsureActorIndexMapping(ShooterRemoteSnapshotSample from, ShooterRemoteSnapshotSample to)
        {
            if (ReferenceEquals(_mappedFrom, from) && ReferenceEquals(_mappedTo, to))
            {
                return;
            }

            if (_fromIndexByToIndex.Length < to.Actors.Count)
            {
                var capacity = Math.Max(to.Actors.Count, Math.Max(16, _fromIndexByToIndex.Length * 2));
                _fromIndexByToIndex = new int[capacity];
            }

            _fromIndexById.Clear();
            for (int i = 0; i < from.Actors.Count; i++)
            {
                _fromIndexById[from.Actors[i].ActorId] = i;
            }

            _toActorIds.Clear();
            for (int i = 0; i < to.Actors.Count; i++)
            {
                var actorId = to.Actors[i].ActorId;
                _toActorIds.Add(actorId);
                _fromIndexByToIndex[i] = _fromIndexById.TryGetValue(actorId, out var fromIndex)
                    ? fromIndex
                    : -1;
            }

            _fromOnlyIndices.Clear();
            for (int i = 0; i < from.Actors.Count; i++)
            {
                if (!_toActorIds.Contains(from.Actors[i].ActorId))
                {
                    _fromOnlyIndices.Add(i);
                }
            }

            _mappedFrom = from;
            _mappedTo = to;
        }

        private static ShooterGatewayActorSnapshot Lerp(in ShooterGatewayActorSnapshot from, in ShooterGatewayActorSnapshot to, float alpha)
        {
            return new ShooterGatewayActorSnapshot(
                to.ActorId,
                InterpolationMath.Lerp(from.X, to.X, alpha),
                InterpolationMath.Lerp(from.Y, to.Y, alpha),
                // Rotation 是由瞄准向量导出的弧度角。沿最短弧混合，避免跨 ±π 接缝时绕远路旋转。
                InterpolationMath.LerpAngleRadians(from.Rotation, to.Rotation, alpha),
                InterpolationMath.Lerp(from.VelocityX, to.VelocityX, alpha),
                InterpolationMath.Lerp(from.VelocityY, to.VelocityY, alpha),
                InterpolationMath.Lerp(from.Hp, to.Hp, alpha),
                to.HpMax,
                to.TeamId);
        }
    }
}
