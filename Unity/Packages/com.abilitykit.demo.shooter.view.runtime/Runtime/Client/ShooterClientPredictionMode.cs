#nullable enable

using System;

namespace AbilityKit.Demo.Shooter.View
{
    /// <summary>
    /// 本地预测开关（诊断用）：环境变量 ABILITYKIT_SHOOTER_DISABLE_LOCAL_PREDICTION=1 时，
    /// 己方角色改为纯权威渲染——不再抑制服务端发来的自身变换（服务端本就恒发，
    /// 带 PredictedLocal 标记），合成渲染批不再使用本地预测批，也不做纠偏调和。
    /// 用于 A/B 判别"两端位置不一致"来自预测/调和链路还是同步管线本身：
    /// 无预测模式下两端渲染的都是服务器权威（各自经插值延迟），收敛位置必须一致。
    /// </summary>
    public static class ShooterClientPredictionMode
    {
        public static readonly bool LocalPredictionEnabled =
            !string.Equals(
                Environment.GetEnvironmentVariable("ABILITYKIT_SHOOTER_DISABLE_LOCAL_PREDICTION"),
                "1",
                StringComparison.OrdinalIgnoreCase);
    }
}
