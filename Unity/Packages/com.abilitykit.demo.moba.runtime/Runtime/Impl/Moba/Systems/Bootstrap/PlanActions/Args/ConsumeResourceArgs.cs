using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;
using AbilityKit.Ability.Impl.Moba.Components;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    /// <summary>
    /// consume_resource Action 的强类型参数
    /// 用于技能释放时消耗资源（蓝量/生命值/能量等）
    /// </summary>
    public readonly struct ConsumeResourceArgs
    {
        /// <summary>
        /// 资源类型
        /// </summary>
        public readonly ResourceType ResourceType;

        /// <summary>
        /// 消耗量（通过 NumericValueRef 支持常量/黑板/变量等多种来源）
        /// </summary>
        public readonly float Amount;

        /// <summary>
        /// 消耗失败时的提示信息 Key
        /// </summary>
        public readonly string FailMessageKey;

        public ConsumeResourceArgs(ResourceType resourceType, float amount, string failMessageKey = null)
        {
            ResourceType = resourceType;
            Amount = amount;
            FailMessageKey = failMessageKey ?? "not_enough_resource";
        }

        public static ConsumeResourceArgs Default => new ConsumeResourceArgs(ResourceType.Mana, 0f, "not_enough_mana");
    }
}
