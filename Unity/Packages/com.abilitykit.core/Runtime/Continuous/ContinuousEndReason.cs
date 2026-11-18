using System;

namespace AbilityKit.Core.Continuous
{
    /// <summary>
    /// 旧 Core 持续体结束原因，枚举顺序保留兼容；新代码使用 AbilityKit.Continuous。
    /// </summary>
    [Obsolete("Continuous lifecycle APIs no longer belong in Core. Use AbilityKit.Continuous; this compatibility API will be removed in the next major version.")]
    public enum ContinuousEndReason
    {
        /// <summary>
        /// 正常完成，例如由实现判定达到时长或条件。
        /// </summary>
        Completed,
        /// <summary>
        /// 被中断。
        /// </summary>
        Interrupted,
        /// <summary>
        /// 来源已结束，例如所属技能被打断。
        /// </summary>
        SourceEnded,
        /// <summary>
        /// 因清理而移除。
        /// </summary>
        CleanedUp,
    }
}
