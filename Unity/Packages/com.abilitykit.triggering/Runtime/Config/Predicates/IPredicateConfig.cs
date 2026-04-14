using AbilityKit.Triggering.Runtime.Config.Values;

namespace AbilityKit.Triggering.Runtime.Config.Predicates
{
    /// <summary>
    /// 条件配置接口（静态配置数据）
    /// </summary>
    public interface IPredicateConfig
    {
        EPredicateKind Kind { get; }
        bool IsEmpty { get; }
    }
}