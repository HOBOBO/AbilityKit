using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    /// <summary>
    /// debug_log Action 的 Schema 定义
    /// </summary>
    public sealed class DebugLogSchema : IActionSchema<DebugLogArgs, IWorldResolver>
    {
        public static readonly DebugLogSchema Instance = new DebugLogSchema();

        public ActionId ActionId => TriggeringConstants.DebugLogId;

        public Type ArgsType => typeof(DebugLogArgs);

        public DebugLogArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<IWorldResolver> ctx)
        {
            int msgId = 0;
            bool dump = false;

            if (namedArgs == null || namedArgs.Count == 0)
                return DebugLogArgs.Default;

            foreach (var kv in namedArgs)
            {
                var rawValue = kv.Value.Ref.Kind == ENumericValueRefKind.Const
                    ? kv.Value.Ref.ConstValue
                    : ActionSchemaRegistry.ResolveNumericRef(kv.Value.Ref, ctx);

                switch (kv.Key.ToLowerInvariant())
                {
                    case "msg_id":
                    case "msgid":
                    case "id":
                        msgId = (int)System.Math.Round(rawValue);
                        break;
                    case "dump":
                    case "is_dump":
                        dump = rawValue >= 0.5;
                        break;
                }
            }

            return new DebugLogArgs(msgId, dump);
        }

        public bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            error = null;
            return true;
        }
    }
}
