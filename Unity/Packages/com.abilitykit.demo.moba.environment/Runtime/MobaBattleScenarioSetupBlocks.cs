using AbilityKit.BattleScenario;
using AbilityKit.Scenario;

namespace AbilityKit.Demo.Moba.EnvironmentModel
{
    /// <summary>
    /// 在无头 MOBA 场景开跑前设置角色属性，不替代时间线中的真实伤害。
    /// </summary>
    public sealed class SetMobaActorAttributeBlock : BattleAtomicBlock
    {
        /// <summary>
        /// 要设置属性的角色别名。
        /// </summary>
        public string ActorAlias { get; set; } = string.Empty;

        /// <summary>
        /// 无头执行器支持的属性键。
        /// </summary>
        public string Property { get; set; } = string.Empty;

        /// <summary>
        /// 布置阶段要写入的数值。
        /// </summary>
        public float Value { get; set; }

        /// <inheritdoc/>
        public override BattleBlockSection Section => BattleBlockSection.Setup;

        /// <inheritdoc/>
        public override void Compile(BattleScenarioBuilder builder) => builder.AddSetupAction(new TestSetupAction
        {
            Action = "set_attr",
            ActorAlias = ActorAlias,
            Property = Property,
            Value = Value,
        });
    }
}
