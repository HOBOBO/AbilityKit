using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Variables.Numeric;

namespace AbilityKit.Triggering.Runtime.Plan
{
    /// <summary>
    /// PlannedTrigger 的参数解析辅助，统一处理具名参数、位置参数和 NumericValueRef 解析。
    /// </summary>
    internal static class PlannedTriggerArgumentResolver<TArgs, TCtx>
        where TArgs : class
    {
        public static NamedArgsDict ResolveNamedArgs(in TArgs args, in ActionCallPlan call, in ExecCtx<TCtx> ctx)
        {
            var arguments = call.Arguments;
            if (arguments.NamedArgs == null || arguments.NamedArgs.Count == 0)
            {
                return NamedArgsDict.Empty;
            }

            var resolvedArgs = new Dictionary<string, ActionArgValue>(arguments.NamedArgs.Count);
            foreach (var pair in arguments.NamedArgs)
            {
                var argument = pair.Value;
                var numericRef = argument.Ref;
                var value = ResolveNumeric(in args, in numericRef, in ctx);
                resolvedArgs[pair.Key] = ActionArgValue.OfConst(value, argument.Name);
            }

            var parsed = ActionSchemaRegistry.GetParsedArgs<TArgs, TCtx>(call.Id, resolvedArgs, ctx);
            return ConvertToNamedArgsDict(parsed, resolvedArgs);
        }

        public static NamedArgsDict CreatePositionalArgs(double v0)
        {
            return new NamedArgsDict(new Dictionary<string, ActionArgValue>
            {
                ["0"] = ActionArgValue.OfConst(v0, "0")
            });
        }

        public static NamedArgsDict CreatePositionalArgs(double v0, double v1)
        {
            return new NamedArgsDict(new Dictionary<string, ActionArgValue>
            {
                ["0"] = ActionArgValue.OfConst(v0, "0"),
                ["1"] = ActionArgValue.OfConst(v1, "1")
            });
        }

        public static double ResolveNumeric(in TArgs args, in NumericValueRef valueRef, in ExecCtx<TCtx> ctx)
        {
            if (ActionSchemaRegistry.TryResolveNumericRef(in valueRef, in args, in ctx, out var value))
            {
                return value;
            }

            throw new InvalidOperationException(FormatNumericResolveError(in valueRef, in ctx));
        }

        public static string FormatFunctionId(in ExecCtx<TCtx> ctx, FunctionId id)
        {
            var names = ctx.IdNames;
            if (names != null && names.TryGetFunctionName(id, out var name) && !string.IsNullOrEmpty(name))
            {
                return $"{id.Value}('{name}')";
            }

            return id.Value.ToString();
        }

        public static string FormatActionId(in ExecCtx<TCtx> ctx, ActionId id)
        {
            var names = ctx.IdNames;
            if (names != null && names.TryGetActionName(id, out var name) && !string.IsNullOrEmpty(name))
            {
                return $"{id.Value}('{name}')";
            }

            return id.Value.ToString();
        }

        private static NamedArgsDict ConvertToNamedArgsDict(object parsed, Dictionary<string, ActionArgValue> rawArgs)
        {
            if (parsed is NamedArgsDict namedDict)
            {
                return namedDict;
            }

            if (rawArgs == null || rawArgs.Count == 0)
            {
                return NamedArgsDict.Empty;
            }

            return new NamedArgsDict(rawArgs);
        }

        private static string FormatNumericResolveError(in NumericValueRef valueRef, in ExecCtx<TCtx> ctx)
        {
            string message;
            switch (valueRef.Kind)
            {
                case ENumericValueRefKind.Blackboard:
                    message = $"Numeric blackboard value not found. boardId={FormatBoardId(in ctx, valueRef.BoardId)} keyId={FormatKeyId(in ctx, valueRef.KeyId)}";
                    break;
                case ENumericValueRefKind.PayloadField:
                    message = $"Numeric payload field not found. argsType={typeof(TArgs).Name} fieldId={FormatFieldId(in ctx, valueRef.FieldId)}";
                    break;
                case ENumericValueRefKind.Var:
                    message = $"Numeric var not found. domainId='{valueRef.DomainId}' key='{valueRef.Key}'";
                    break;
                case ENumericValueRefKind.Expr:
                    message = "Numeric expr evaluate failed: " + valueRef.ExprText;
                    break;
                default:
                    message = $"Unsupported NumericValueRef kind: {valueRef.Kind}";
                    break;
            }

            if (!string.IsNullOrEmpty(valueRef.Label)) message += $" label='{valueRef.Label}'";
            if (!string.IsNullOrEmpty(valueRef.Scope)) message += $" scope='{valueRef.Scope}'";
            if (valueRef.Required) message += " required=true";
            if (valueRef.HasFallback) message += $" fallback={valueRef.FallbackValue}";
            if (valueRef.HasScale) message += $" scale={valueRef.Scale}";
            if (valueRef.Offset != 0d) message += $" offset={valueRef.Offset}";
            if (valueRef.HasMin) message += $" min={valueRef.MinValue}";
            if (valueRef.HasMax) message += $" max={valueRef.MaxValue}";
            return message;
        }

        private static string FormatBoardId(in ExecCtx<TCtx> ctx, int id)
        {
            var names = ctx.IdNames;
            if (names != null && names.TryGetBoardName(id, out var name) && !string.IsNullOrEmpty(name))
            {
                return $"{id}('{name}')";
            }

            return id.ToString();
        }

        private static string FormatKeyId(in ExecCtx<TCtx> ctx, int id)
        {
            var names = ctx.IdNames;
            if (names != null && names.TryGetKeyName(id, out var name) && !string.IsNullOrEmpty(name))
            {
                return $"{id}('{name}')";
            }

            return id.ToString();
        }

        private static string FormatFieldId(in ExecCtx<TCtx> ctx, int id)
        {
            var names = ctx.IdNames;
            if (names != null && names.TryGetFieldName(id, out var name) && !string.IsNullOrEmpty(name))
            {
                return $"{id}('{name}')";
            }

            return id.ToString();
        }
    }
}
