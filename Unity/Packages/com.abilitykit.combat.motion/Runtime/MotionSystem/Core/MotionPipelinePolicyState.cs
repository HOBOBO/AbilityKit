namespace AbilityKit.Combat.MotionSystem.Core
{
    /// <summary>
    /// 保存 MotionPipelinePolicy 的完整纯值规则，不包含策略对象身份或运行时依赖。
    /// </summary>
    public struct MotionPipelinePolicyState
    {
        /// <summary>
        /// 按 SuppressorGroupId 升序排列的完整规则；空数组表示没有抑制规则。
        /// </summary>
        public MotionGroupSuppressionState[] Rules;
    }

    /// <summary>
    /// 保存一个抑制组及其有序目标组列表。
    /// </summary>
    public struct MotionGroupSuppressionState
    {
        /// <summary>
        /// 触发抑制的来源组编号。
        /// </summary>
        public int SuppressorGroupId;

        /// <summary>
        /// 被抑制的来源组编号；保留输入顺序及重复值。
        /// </summary>
        public int[] SuppressedGroupIds;
    }
}
