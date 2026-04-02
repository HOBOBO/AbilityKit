using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.Moba.Components;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    /// <summary>
    /// consume_resource Action 的 Schema 定义
    /// 实现 IActionSchema，提供参数解析和验证逻辑
    /// </summary>
    public sealed class ConsumeResourceSchema : IActionSchema<ConsumeResourceArgs, IWorldResolver>
    {
        public static readonly ConsumeResourceSchema Instance = new ConsumeResourceSchema();

        public ActionId ActionId => TriggeringConstants.ConsumeResourceId;

        public Type ArgsType => typeof(ConsumeResourceArgs);

        public ConsumeResourceArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx)
        {
            float amount = 0f;
            ResourceType resourceType = ResourceType.Mana;
            string failMessageKey = "not_enough_resource";

            if (namedArgs == null || namedArgs.Count == 0)
                return new ConsumeResourceArgs(resourceType, amount, failMessageKey);

            foreach (var kv in namedArgs)
            {
                var rawValue = kv.Value.Ref.Kind == ENumericValueRefKind.Const
                    ? kv.Value.Ref.ConstValue
                    : ActionSchemaRegistry.ResolveNumericRef(kv.Value.Ref, ctx);

                switch (kv.Key.ToLowerInvariant())
                {
                    case "amount":
                    case "cost":
                    case "value":
                        amount = (float)rawValue;
                        break;

                    case "resource_type":
                    case "resourcetype":
                    case "type":
                        resourceType = (ResourceType)(int)System.Math.Round(rawValue);
                        break;

                    case "fail_message_key":
                    case "failmessagekey":
                    case "fail_key":
                        // 字符串类型参数（暂不支持，忽略）
                        break;
                }
            }

            return new ConsumeResourceArgs(resourceType, amount, failMessageKey);
        }

        public bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            error = null;
            foreach (var kv in args)
            {
                switch (kv.Key.ToLowerInvariant())
                {
                    case "amount":
                    case "cost":
                    case "value":
                        return true;
                }
            }
            // amount 是可选的，默认为 0（表示不消耗）
            return true;
        }
    }
}
