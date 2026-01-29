namespace AbilityKit.Ability
{
    /// <summary>
    /// 技能管道状态
    /// </summary>
    public enum EAbilityPipelineState
    {
        Ready,          // 准备执行
        Executing,      // 正在执行
        Completed,      // 执行完成
        Failed          // 执行失败
    }
}