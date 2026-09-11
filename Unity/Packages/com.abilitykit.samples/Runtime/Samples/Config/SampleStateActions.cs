#nullable enable

using AbilityKit.Samples.Logic.Infrastructure.Config.Attributes;

namespace AbilityKit.Samples.Logic.Samples.Config
{
    /// <summary>
    /// 播放待机动画
    /// </summary>
    [StateActionTypeId("PlayIdleAnimation")]
    public sealed class PlayIdleAnimationAction { }

    /// <summary>
    /// 检查是否有敌人
    /// </summary>
    [StateActionTypeId("CheckForEnemies")]
    public sealed class CheckForEnemiesAction { }

    /// <summary>
    /// 停止待机动画
    /// </summary>
    [StateActionTypeId("StopIdleAnimation")]
    public sealed class StopIdleAnimationAction { }

    /// <summary>
    /// 播放奔跑动画
    /// </summary>
    [StateActionTypeId("PlayRunAnimation")]
    public sealed class PlayRunAnimationAction { }

    /// <summary>
    /// 绉诲姩鍒扮洰鏍?
    /// </summary>
    [StateActionTypeId("MoveToTarget")]
    public sealed class MoveToTargetAction { }

    /// <summary>
    /// 停止移动
    /// </summary>
    [StateActionTypeId("StopMoving")]
    public sealed class StopMovingAction { }

    /// <summary>
    /// 播放攻击动画
    /// </summary>
    [StateActionTypeId("PlayAttackAnimation")]
    public sealed class PlayAttackAnimationAction { }

    /// <summary>
    /// 妫€鏌ユ敾鍑昏寖鍥?
    /// </summary>
    [StateActionTypeId("CheckAttackRange")]
    public sealed class CheckAttackRangeAction { }

    /// <summary>
    /// 重置攻击冷却
    /// </summary>
    [StateActionTypeId("ResetAttackCooldown")]
    public sealed class ResetAttackCooldownAction { }

    /// <summary>
    /// 播放死亡动画
    /// </summary>
    [StateActionTypeId("PlayDeathAnimation")]
    public sealed class PlayDeathAnimationAction { }

    /// <summary>
    /// 淡出效果
    /// </summary>
    [StateActionTypeId("FadeOut")]
    public sealed class FadeOutAction { }
}
