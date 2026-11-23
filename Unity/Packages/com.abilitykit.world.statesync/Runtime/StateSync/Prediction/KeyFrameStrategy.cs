using System;

namespace AbilityKit.Ability.StateSync
{
    /// <summary>
    /// 按帧间隔计算的计划级别，不表示快照、差分或权威状态实际可用。
    /// </summary>
    public enum KeyFrameLevel
    {
        /// <summary>
        /// 命中全量间隔时使用，优先于其他级别。
        /// </summary>
        FullSnapshot = 0,
        /// <summary>
        /// 未命中全量但命中主要差分间隔时使用。
        /// </summary>
        MajorDelta = 1,
        /// <summary>
        /// 未命中全量和主要差分但命中次要差分间隔时使用。
        /// </summary>
        MinorDelta = 2,
        /// <summary>
        /// 未命中以上任一间隔时使用。
        /// </summary>
        Incremental = 3
    }

    /// <summary>
    /// 按帧号取模生成静态计划，不存储快照、验证基线或推进世界。
    /// 接入方应使用非负且为相邻帧查询预留整数运算空间的帧号；不会验证帧号合法性。
    /// </summary>
    public sealed class KeyFrameStrategy
    {
        private readonly int _fullSnapshotInterval;
        private readonly int _majorDeltaInterval;
        private readonly int _minorDeltaInterval;

        /// <summary>
        /// 全量计划间隔，以帧为单位。
        /// </summary>
        public int FullSnapshotInterval => _fullSnapshotInterval;
        /// <summary>
        /// 主要差分计划间隔，以帧为单位。
        /// </summary>
        public int MajorDeltaInterval => _majorDeltaInterval;
        /// <summary>
        /// 次要差分计划间隔，以帧为单位。
        /// </summary>
        public int MinorDeltaInterval => _minorDeltaInterval;

        /// <summary>
        /// 创建不可变计划，默认全量、主要和次要间隔分别为 60、10、3 帧。
        /// 各间隔必须为正，否则抛出参数越界异常；不要求互相整除或按大小排列。
        /// </summary>
        public KeyFrameStrategy(
            int fullSnapshotInterval = 60,
            int majorDeltaInterval = 10,
            int minorDeltaInterval = 3)
        {
            if (fullSnapshotInterval <= 0) throw new ArgumentOutOfRangeException(nameof(fullSnapshotInterval));
            if (majorDeltaInterval <= 0) throw new ArgumentOutOfRangeException(nameof(majorDeltaInterval));
            if (minorDeltaInterval <= 0) throw new ArgumentOutOfRangeException(nameof(minorDeltaInterval));

            _fullSnapshotInterval = fullSnapshotInterval;
            _majorDeltaInterval = majorDeltaInterval;
            _minorDeltaInterval = minorDeltaInterval;
        }

        /// <summary>
        /// 保留命名的本地预设，三个间隔依次为 60、10、1 帧；不代表实现其他产品的同步协议。
        /// </summary>
        public static KeyFrameStrategy OverwatchStyle()
        {
            return new KeyFrameStrategy(
                fullSnapshotInterval: 60,
                majorDeltaInterval: 10,
                minorDeltaInterval: 1);
        }

        /// <summary>
        /// 返回三个间隔依次为 300、30、5 帧的预设，不保证实际带宽。
        /// </summary>
        public static KeyFrameStrategy LowBandwidth()
        {
            return new KeyFrameStrategy(
                fullSnapshotInterval: 300,
                majorDeltaInterval: 30,
                minorDeltaInterval: 5);
        }

        /// <summary>
        /// 返回三个间隔依次为 30、5、1 帧的预设，不保证表现质量。
        /// </summary>
        public static KeyFrameStrategy HighQuality()
        {
            return new KeyFrameStrategy(
                fullSnapshotInterval: 30,
                majorDeltaInterval: 5,
                minorDeltaInterval: 1);
        }

        /// <summary>
        /// 帧号能被全量间隔整除时返回真，正常起始帧 0 属于全量计划。
        /// </summary>
        public bool IsKeyFrame(int frame)
        {
            return frame % _fullSnapshotInterval == 0;
        }

        /// <summary>
        /// 仅在非全量帧且帧号能被主要间隔整除时返回真。
        /// </summary>
        public bool IsMajorDeltaFrame(int frame)
        {
            return !IsKeyFrame(frame) && frame % _majorDeltaInterval == 0;
        }

        /// <summary>
        /// 仅在非全量、非主要差分帧且帧号能被次要间隔整除时返回真。
        /// </summary>
        public bool IsMinorDeltaFrame(int frame)
        {
            return !IsKeyFrame(frame) && !IsMajorDeltaFrame(frame) && frame % _minorDeltaInterval == 0;
        }

        /// <summary>
        /// 依全量、主要、次要、增量的优先级返回唯一计划级别。
        /// </summary>
        public KeyFrameLevel GetKeyFrameLevel(int frame)
        {
            if (IsKeyFrame(frame)) return KeyFrameLevel.FullSnapshot;
            if (IsMajorDeltaFrame(frame)) return KeyFrameLevel.MajorDelta;
            if (IsMinorDeltaFrame(frame)) return KeyFrameLevel.MinorDelta;
            return KeyFrameLevel.Incremental;
        }

        /// <summary>
        /// 从下一帧向前扫描到下一个全量计划帧，不包含传入帧；耗时随全量间隔增大。
        /// 仅报告计划位置，不查询实际可用快照。
        /// </summary>
        public int GetNextKeyFrame(int currentFrame)
        {
            int next = currentFrame + 1;
            while (!IsKeyFrame(next))
            {
                next++;
                if (next - currentFrame > _fullSnapshotInterval)
                    return currentFrame + 1;
            }
            return next;
        }

        /// <summary>
        /// 从前一帧向低帧号扫描到上一个全量计划帧；非负区间无更早结果时返回 0。
        /// 返回 0 不证明存在可用于恢复的快照，耗时随全量间隔增大。
        /// </summary>
        public int GetPreviousKeyFrame(int currentFrame)
        {
            int prev = currentFrame - 1;
            while (prev >= 0 && !IsKeyFrame(prev))
            {
                prev--;
            }
            return prev >= 0 ? prev : 0;
        }

        /// <summary>
        /// 对实体数与平均实体大小的乘积按全量、主要、次要、增量分别乘二、除二、除四、除八。
        /// 若平均大小以字节计，返回估算的单帧字节数，不是吞吐；整数除法截断，不校验负值或防止乘积溢出。
        /// </summary>
        public int EstimateBandwidth(int frame, int entityCount, int averageEntitySize)
        {
            var level = GetKeyFrameLevel(frame);
            switch (level)
            {
                case KeyFrameLevel.FullSnapshot:
                    return entityCount * averageEntitySize * 2;
                case KeyFrameLevel.MajorDelta:
                    return entityCount * averageEntitySize / 2;
                case KeyFrameLevel.MinorDelta:
                    return entityCount * averageEntitySize / 4;
                case KeyFrameLevel.Incremental:
                    return entityCount * averageEntitySize / 8;
                default:
                    return entityCount * averageEntitySize / 4;
            }
        }
    }
}
