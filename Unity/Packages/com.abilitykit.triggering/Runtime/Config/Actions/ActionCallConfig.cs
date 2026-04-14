using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Config.Values;

namespace AbilityKit.Triggering.Runtime.Config.Actions
{
    /// <summary>
    /// Action 调用配置实现（静态配置数据）
    /// </summary>
    [Serializable]
    public struct ActionCallConfig : IActionCallConfig
    {
        public ActionId ActionId { get; set; }
        public int Arity { get; set; }
        public List<ValueRefConfig> Args { get; set; }

        IReadOnlyList<IValueRefConfig> IActionCallConfig.Args => 
            Args?.ConvertAll<IValueRefConfig>(v => v);

        public static ActionCallConfig Create(ActionId actionId) => new ActionCallConfig
        {
            ActionId = actionId,
            Arity = 0,
            Args = null
        };

        public static ActionCallConfig Create(ActionId actionId, double arg0) => new ActionCallConfig
        {
            ActionId = actionId,
            Arity = 1,
            Args = new List<ValueRefConfig> { ValueRefConfig.Const(arg0) }
        };

        public static ActionCallConfig Create(ActionId actionId, double arg0, double arg1) => new ActionCallConfig
        {
            ActionId = actionId,
            Arity = 2,
            Args = new List<ValueRefConfig> { ValueRefConfig.Const(arg0), ValueRefConfig.Const(arg1) }
        };

        public static ActionCallConfig Create(ActionId actionId, params ValueRefConfig[] args) => new ActionCallConfig
        {
            ActionId = actionId,
            Arity = args.Length,
            Args = new List<ValueRefConfig>(args)
        };
    }
}