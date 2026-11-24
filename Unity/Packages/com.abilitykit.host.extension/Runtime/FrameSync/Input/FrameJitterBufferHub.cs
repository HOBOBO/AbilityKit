#nullable enable annotations
using System;

namespace AbilityKit.Ability.Host.Extensions.FrameSync
{
    /// <summary>
    /// 为同一帧缓冲提供写入与消费端口；释放端口也会释放该缓冲。
    /// </summary>
    public sealed class FrameJitterBufferHub<TFrame> :
        AbilityKit.Network.Abstractions.IConsumableRemoteFrameSource<TFrame>,
        AbilityKit.Network.Abstractions.IRemoteFrameSink<TFrame>
    {
        private readonly AbilityKit.Network.Runtime.FrameJitterBuffer<TFrame> _buf;

        /// <summary>
        /// 包装指定缓冲；端口负责在释放时清理它。
        /// </summary>
        public FrameJitterBufferHub(AbilityKit.Network.Runtime.FrameJitterBuffer<TFrame> buf)
        {
            _buf = buf ?? throw new ArgumentNullException(nameof(buf));
        }

        /// <summary>
        /// 获取或设置目标消费帧相对最大已收帧的延迟帧数。
        /// </summary>
        public int DelayFrames
        {
            get => _buf.DelayFrames;
            set => _buf.DelayFrames = value;
        }

        /// <summary>
        /// 获取已接收的最大帧号。
        /// </summary>
        public int MaxReceivedFrame => _buf.MaxReceivedFrame;

        /// <summary>
        /// 获取按延迟计算的目标帧号。
        /// </summary>
        public int TargetFrame => _buf.TargetFrame;

        /// <summary>
        /// 获取缓冲记录的最后消费帧。
        /// </summary>
        public int LastConsumedFrame => _buf.LastConsumedFrame;

        /// <summary>
        /// 获取缓冲接受写入的累计次数。
        /// </summary>
        public long AddedCount => _buf.AddedCount;

        /// <summary>
        /// 获取消费前覆盖同帧记录的累计次数。
        /// </summary>
        public long DuplicateCount => _buf.DuplicateCount;

        /// <summary>
        /// 获取因帧已消费而拒绝的累计次数。
        /// </summary>
        public long LateCount => _buf.LateCount;

        /// <summary>
        /// 获取当前保存的帧数。
        /// </summary>
        public int Count => _buf.Count;

        /// <summary>
        /// 查询已缓冲的帧，不消费也不合成缺帧。
        /// </summary>
        public bool TryGet(int frame, out TFrame frameData) => _buf.TryGet(frame, out frameData);

        /// <summary>
        /// 按底层缓冲的缺帧策略尝试消费目标帧。
        /// </summary>
        public bool TryConsume(int frame, out TFrame frameData) => _buf.TryConsume(frame, out frameData);

        /// <summary>
        /// 移除指定帧号之前的缓冲记录。
        /// </summary>
        public void TrimBefore(int minFrameInclusive) => _buf.TrimBefore(minFrameInclusive);

        /// <summary>
        /// 提交一帧数据，重复及晚到处理遵循底层缓冲的策略。
        /// </summary>
        public void Add(int frame, TFrame frameData) => _buf.Add(frame, frameData);

        /// <summary>
        /// 释放底层缓冲，清除其记录与计数。
        /// </summary>
        public void Dispose() => _buf.Dispose();
    }
}
