using System.Collections.Generic;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Config.Values;

namespace AbilityKit.Triggering.Runtime.Config.Actions
{
    /// <summary>
    /// Action 调用配置（静态配置数据）
    /// </summary>
    public interface IActionCallConfig
    {
        ActionId ActionId { get; }
        int Arity { get; }
        IReadOnlyList<IValueRefConfig> Args { get; }
    }
}