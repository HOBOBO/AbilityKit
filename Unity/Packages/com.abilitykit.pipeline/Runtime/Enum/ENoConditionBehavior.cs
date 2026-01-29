using Sirenix.OdinInspector;

namespace AbilityKit.Ability
{
    /// <summary>
    /// 定义条件都不满足时的行为
    /// </summary>
    [LabelText("条件都不满足时的行为")]
    public enum ENoConditionBehavior
    {
        // 等待直到有条件满足
        [LabelText("等待直到有条件满足")]
        Wait,
        // 完成当前阶段
        [LabelText("完成当前阶段")]
        Complete,
        // 中断并失败
        [LabelText("中断并失败")]
        Fail,
        // 跳过当前阶段
        [LabelText("跳过当前阶段")]
        Skip
    }
}