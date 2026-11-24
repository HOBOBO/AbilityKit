#nullable enable annotations
using System;
using AbilityKit.Network.Runtime;

namespace AbilityKit.Ability.Host.Extensions.FrameSync
{
    /// <summary>
    /// 按调用方显式选择的缺帧策略创建输入缓冲端口。
    /// </summary>
    public static class FrameSyncInputHubFactory
    {
        /// <summary>
        /// 创建新的缓冲端口；调用方负责在会话结束时释放。
        /// </summary>
        public static FrameJitterBufferHub<TFrame> CreateJitterBufferHub<TFrame>(
            int delayFrames,
            MissingFrameMode missingMode,
            Func<TFrame> missingFrameFactory,
            int initialCapacity = 256)
        {
            var buf = new AbilityKit.Network.Runtime.FrameJitterBuffer<TFrame>(delayFrames, missingMode, missingFrameFactory, initialCapacity);
            return new FrameJitterBufferHub<TFrame>(buf);
        }
    }
}
