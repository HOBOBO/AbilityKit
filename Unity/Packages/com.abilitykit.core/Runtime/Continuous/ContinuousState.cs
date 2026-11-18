using System;

namespace AbilityKit.Core.Continuous
{
    /// <summary>
    /// 旧 Core 持续体状态，枚举顺序保留兼容；由持续体实现维护，新代码使用 AbilityKit.Continuous。
    /// </summary>
    [Obsolete("Continuous lifecycle APIs no longer belong in Core. Use AbilityKit.Continuous; this compatibility API will be removed in the next major version.")]
    public enum ContinuousState
    {
        /// <summary>
        /// 未激活状态。
        /// </summary>
        Inactive,
        /// <summary>
        /// 正在激活的过渡状态。
        /// </summary>
        Activating,
        /// <summary>
        /// 运行中的激活状态。
        /// </summary>
        Active,
        /// <summary>
        /// 已暂停状态。
        /// </summary>
        Paused,
        /// <summary>
        /// 正常结束后的终止状态；管理器不会自动按时长进入此状态。
        /// </summary>
        Expired,
        /// <summary>
        /// 中止后的终止状态。
        /// </summary>
        Aborted,
    }
}
