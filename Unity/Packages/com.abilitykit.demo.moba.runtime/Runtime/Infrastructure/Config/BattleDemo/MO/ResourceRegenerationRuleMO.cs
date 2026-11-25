using System;
using AbilityKit.Demo.Moba.Share.Config;

namespace AbilityKit.Demo.Moba.Config.BattleDemo.MO
{
    /// <summary>
    /// 已加载的固定量资源回复规则，不持有可变资源值。
    /// </summary>
    public sealed class ResourceRegenerationRuleMO
    {
        /// <summary>
        /// 稳定规则 ID。
        /// </summary>
        public int Id { get; }
        /// <summary>
        /// 显式配置的非生命资源类型。
        /// </summary>
        public int ResourceType { get; }
        /// <summary>
        /// 每秒固定回复量属性 ID。
        /// </summary>
        public int FlatPerSecondAttribute { get; }
        /// <summary>
        /// 拷贝纯数据字段，避免运行时规则依赖可变 DTO。
        /// </summary>
        public ResourceRegenerationRuleMO(ResourceRegenerationRuleDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id; ResourceType = dto.ResourceType; FlatPerSecondAttribute = dto.FlatPerSecondAttribute;
        }
    }
}
