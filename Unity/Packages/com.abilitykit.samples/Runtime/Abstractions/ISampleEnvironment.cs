#nullable enable

using System;

namespace AbilityKit.Samples.Abstractions
{
    /// <summary>
    /// 示例运行环境接口
    /// </summary>
    public interface ISampleEnvironment
    {
        /// <summary>
        /// 当前时间
        /// </summary>
        float Time { get; }

        /// <summary>
        /// 上一帧时间
        /// </summary>
        float DeltaTime { get; }

        /// <summary>
        /// 是否暂停
        /// </summary>
        bool IsPaused { get; }

        /// <summary>
        /// 推进时间
        /// </summary>
        void Advance(float delta);

        /// <summary>
        /// 暂停
        /// </summary>
        void Pause();

        /// <summary>
        /// 继续
        /// </summary>
        void Resume();

        /// <summary>
        /// 重置
        /// </summary>
        void Reset();

        /// <summary>
        /// 推进到指定时间
        /// </summary>
        void AdvanceTo(float targetTime);

        /// <summary>
        /// 每帧更新回调
        /// </summary>
        event Action<float>? OnTick;

        /// <summary>
        /// 执行一个 "帧"
        /// </summary>
        void Tick();

        /// <summary>
        /// 执行到完成（用于即时模式）
        /// </summary>
        void ExecuteUntilComplete();
    }
}
