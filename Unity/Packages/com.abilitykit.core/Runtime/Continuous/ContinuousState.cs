using System;

namespace AbilityKit.Core.Continuous
{
    /// <summary>
    /// 持续体状态
    /// </summary>
    [Obsolete("Continuous lifecycle APIs no longer belong in Core. Use AbilityKit.Continuous; this compatibility API will be removed in the next major version.")]
    public enum ContinuousState
    {
        /// <summary>未激活/已销毁</summary>
        Inactive,
        /// <summary>正在激活中</summary>
        Activating,
        /// <summary>运行中</summary>
        Active,
        /// <summary>已暂停</summary>
        Paused,
        /// <summary>已过期（正常结束）</summary>
        Expired,
        /// <summary>已中止（非正常结束）</summary>
        Aborted,
    }
}
