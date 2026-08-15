using System;

namespace AbilityKit.Core.Continuous
{
    /// <summary>
    /// 持续体结束原因
    /// </summary>
    [Obsolete("Continuous lifecycle APIs no longer belong in Core. Use AbilityKit.Continuous; this compatibility API will be removed in the next major version.")]
    public enum ContinuousEndReason
    {
        /// <summary>正常完成（达到时长或条件）</summary>
        Completed,
        /// <summary>被中断</summary>
        Interrupted,
        /// <summary>来源已结束（如技能被打断）</summary>
        SourceEnded,
        /// <summary>被清理</summary>
        CleanedUp,
    }
}
