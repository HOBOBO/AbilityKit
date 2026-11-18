using System;

namespace AbilityKit.Core.Continuous
{
    /// <summary>
    /// 旧 Core 持续体生命周期契约，保留至下一主版本移除窗口。
    /// 新代码使用 AbilityKit.Continuous；状态转换、计时与结束事件由持续体实现负责，管理器不推进时间。
    /// </summary>
    [Obsolete("Continuous lifecycle APIs no longer belong in Core. Use AbilityKit.Continuous; this compatibility API will be removed in the next major version.")]
    public interface IContinuous
    {
        /// <summary>
        /// 获取配置；在注册期间应保持配置及所属实体标识稳定。
        /// </summary>
        IContinuousConfig Config { get; }

        /// <summary>
        /// 获取实现维护的当前生命周期状态。
        /// </summary>
        ContinuousState State { get; }

        /// <summary>
        /// 获取实现是否处于激活态。
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// 获取实现是否已终止，通常对应 Expired 或 Aborted。
        /// </summary>
        bool IsTerminated { get; }

        /// <summary>
        /// 获取实现是否处于暂停态。
        /// </summary>
        bool IsPaused { get; }

        /// <summary>
        /// 获取实现累计的经过时间，单位为秒；管理器不自动累加。
        /// </summary>
        float ElapsedSeconds { get; }

        /// <summary>
        /// 请求激活，由实现维护状态；管理器可能对已激活对象再次调用此方法。
        /// </summary>
        void Activate();

        /// <summary>
        /// 请求暂停，由实现维护状态及计时规则。
        /// </summary>
        void Pause();

        /// <summary>
        /// 请求恢复，由实现维护状态及计时规则。
        /// </summary>
        void Resume();

        /// <summary>
        /// 按指定原因结束；实现应更新终止状态并发出 OnEnded，以便已注册管理器清理索引。
        /// </summary>
        void End(ContinuousEndReason reason);

        /// <summary>
        /// 按文本原因中止；实现负责终止状态及结束事件的枚举原因。
        /// </summary>
        void Abort(string reason);

        /// <summary>
        /// 通知生命周期结束，参数为结束对象和原因；管理器原样传递原因，不根据状态重新推断。
        /// </summary>
        event Action<IContinuous, ContinuousEndReason> OnEnded;
    }
}
