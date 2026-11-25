using System;

namespace AbilityKit.Demo.Moba.Share.Config
{
    /// <summary>
    /// 非生命资源的固定量自动回复规则；资源类型与速率属性均由正式内容显式绑定。
    /// </summary>
    [Serializable]
    public sealed class ResourceRegenerationRuleDTO
    {
        /// <summary>
        /// 稳定规则 ID，作为确定性处理顺序。
        /// </summary>
        public int Id;
        /// <summary>
        /// 资源枚举值；None 和 HP 不允许走此回复入口。
        /// </summary>
        public int ResourceType;
        /// <summary>
        /// 每秒固定回复量的 BattleAttributeType ID。
        /// </summary>
        public int FlatPerSecondAttribute;
    }
}
