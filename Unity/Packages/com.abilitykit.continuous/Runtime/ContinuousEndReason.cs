namespace AbilityKit.Continuous
{
    /// <summary>
    /// 持续体结束原因
    /// </summary>
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
