using System;

namespace AbilityKit.Core.Continuous
{
    /// <summary>
    /// 持续体接口
    /// 
    /// 统一所有具有"持续时间、可被中断/暂停"的对象的外壳
    /// 不关心内部如何执行，只负责生命周期管理
    /// 
    /// 配置通过 IContinuousConfig 传入，支持可选扩展接口
    /// </summary>
    [Obsolete("Continuous lifecycle APIs no longer belong in Core. Use AbilityKit.Continuous; this compatibility API will be removed in the next major version.")]
    public interface IContinuous
    {
        /// <summary>
        /// 配置
        /// </summary>
        IContinuousConfig Config { get; }

        /// <summary>
        /// 当前状态
        /// </summary>
        ContinuousState State { get; }

        /// <summary>
        /// 已激活
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// 已终止（Expired 或 Aborted）
        /// </summary>
        bool IsTerminated { get; }

        /// <summary>
        /// 已暂停
        /// </summary>
        bool IsPaused { get; }

        /// <summary>
        /// 已消耗的时间（秒）
        /// </summary>
        float ElapsedSeconds { get; }

        /// <summary>
        /// 激活持续体
        /// </summary>
        void Activate();

        /// <summary>
        /// 暂停持续体
        /// </summary>
        void Pause();

        /// <summary>
        /// 恢复持续体
        /// </summary>
        void Resume();

        /// <summary>
        /// 结束持续体。
        /// </summary>
        void End(ContinuousEndReason reason);

        /// <summary>
        /// 中止持续体
        /// </summary>
        /// <param name="reason">中止原因</param>
        void Abort(string reason);

        /// <summary>
        /// 持续体结束事件
        /// </summary>
        event Action<IContinuous, ContinuousEndReason> OnEnded;
    }
}
