using System;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 时间提供者接口，用于抽象时间访问，使管线逻辑可独立于 Unity 运行
    /// </summary>
    public interface ITimeProvider
    {
        /// <summary>
        /// 获取自系统启动以来的实时时间（秒）
        /// </summary>
        float RealtimeSinceStartup { get; }
    }
}
