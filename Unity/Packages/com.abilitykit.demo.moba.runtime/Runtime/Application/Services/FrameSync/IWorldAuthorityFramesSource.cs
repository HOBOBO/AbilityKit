using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Demo.Moba.Services
{
    public interface IWorldAuthorityFramesSource
    {
        bool TryGetFrames(WorldId worldId, out FrameIndex confirmed, out FrameIndex predicted);
    }

    /// <summary>
    /// 可选的同世界恢复边界。本地预测帧和普通输入确认不能作为回收授权。
    /// </summary>
    public interface IWorldRestoreFloorSource
    {
        /// <summary>
        /// 返回已由宿主强制执行的最早可恢复帧。
        /// </summary>
        bool TryGetRestoreFloor(WorldId worldId, out FrameIndex frame);
    }
}
