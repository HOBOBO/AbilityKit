#nullable enable

using System;

namespace AbilityKit.Samples.Abstractions
{
    /// <summary>
    /// 执行模式
    /// </summary>
    public enum ExecutionMode
    {
        /// <summary>
        /// 即时模式 - 同步执行所有逻辑
        /// </summary>
        Instant,

        /// <summary>
        /// 模拟模式 - 按帧模拟执行
        /// </summary>
        Simulated,

        /// <summary>
        /// 实时模式 - 按实际时间执行
        /// </summary>
        Realtime
    }

    /// <summary>
    /// 示例环境工厂接口
    /// </summary>
    public interface ISampleEnvironmentFactory
    {
        /// <summary>
        /// 创建运行环境
        /// </summary>
        ISampleEnvironment Create(ExecutionMode mode);
    }
}
