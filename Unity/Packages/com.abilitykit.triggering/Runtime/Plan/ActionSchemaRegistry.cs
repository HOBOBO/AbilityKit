using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Core.Common.Log;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Variables.Numeric;
using AbilityKit.Triggering.Variables.Numeric.Expression;

namespace AbilityKit.Triggering.Runtime.Plan
{
    /// <summary>
    /// Action Schema 注册表
    /// 业务包启动时通过 PlanActionModule.Register() 注册各自的 Schema
    /// 运行时通过 ActionSchemaRegistry.ParseArgs(actionId, args, ctx) 将字典解析为强类型 struct
    /// </summary>
    public static class ActionSchemaRegistry
    {
        // 存储泛型 schema（内部）
        private static readonly Dictionary<ActionId, IActionSchema> _schemas = new Dictionary<ActionId, IActionSchema>();
        private static bool _frozen;

        /// <summary>
        /// 注册一个 Action Schema（通常由 PlanActionModule 在 Register() 中调用）
        /// </summary>
        /// <remarks>
        /// 注册发生在启动阶段（冻结前），支持重复注册（后面的覆盖前面的）。
        /// 运行时严禁修改。
        /// </remarks>
        public static void Register(IActionSchema schema)
        {
            if (schema == null) return;

            if (_frozen)
            {
                Log.Warning($"[ActionSchemaRegistry] Register called after freeze. Schema={schema.ActionId.Value}. This should not happen at runtime.");
                return;
            }

            _schemas[schema.ActionId] = schema;
        }

        /// <summary>
        /// 注册一个泛型 Action Schema
        /// </summary>
        public static void Register<TActionArgs, TCtx>(IActionSchema<TActionArgs, TCtx> schema)
        {
            Register(new GenericSchemaAdapter<TActionArgs, TCtx>(schema));
        }

        /// <summary>
        /// 冻结注册表，阻止后续注册（启动完成后调用）
        /// </summary>
        public static void Freeze()
        {
            _frozen = true;
        }

        /// <summary>
        /// 重置注册表（仅用于单元测试）
        /// </summary>
        internal static void Reset()
        {
            _schemas.Clear();
            _frozen = false;
        }

        /// <summary>
        /// 尝试获取指定 ActionId 的 Schema
        /// </summary>
        public static bool TryGet(ActionId id, out IActionSchema schema)
        {
            return _schemas.TryGetValue(id, out schema);
        }

        /// <summary>
        /// 尝试获取指定 ActionId 的泛型 Schema
        /// </summary>
        public static bool TryGet<TActionArgs, TCtx>(ActionId id, out IActionSchema<TActionArgs, TCtx> schema)
        {
            if (_schemas.TryGetValue(id, out var s) && s is GenericSchemaAdapter<TActionArgs, TCtx> adapter)
            {
                schema = adapter.Inner;
                return true;
            }
            schema = default;
            return false;
        }

        /// <summary>
        /// 通过 ActionId 获取已解析的强类型参数（内部用）
        /// </summary>
        internal static object GetParsedArgs<TActionArgs, TCtx>(ActionId id, Dictionary<string, ActionArgValue> namedArgs, ExecCtx<TCtx> ctx)
        {
            if (namedArgs == null || namedArgs.Count == 0)
                return null;

            if (_schemas.TryGetValue(id, out var s) && s is GenericSchemaAdapter<TActionArgs, TCtx> adapter)
            {
                // 验证
                var span = new ReadOnlySpan<KeyValuePair<string, ActionArgValue>>(namedArgs.ToArray());
                if (!adapter.Inner.TryValidateArgs(span, out var error))
                {
                    Log.Warning($"[ActionSchemaRegistry] Args validation failed for action={id.Value}: {error}");
                }
                return adapter.Inner.ParseArgs(namedArgs, ctx);
            }
            return null;
        }

        /// <summary>
        /// 将具名参数字典解析为强类型参数结构体
        /// 如果 Action 没有注册 Schema（向后兼容），则返回 null
        /// </summary>
        public static object ParseArgs(ActionId id, Dictionary<string, ActionArgValue> namedArgs, object ctx)
        {
            if (namedArgs == null || namedArgs.Count == 0)
                return null;

            if (!_schemas.TryGetValue(id, out var schema))
                return null;

            // 验证（可选）
            var span = new ReadOnlySpan<KeyValuePair<string, ActionArgValue>>(namedArgs.ToArray());
            if (!schema.TryValidateArgs(span, out var error))
            {
                Log.Warning($"[ActionSchemaRegistry] Args validation failed for action={id.Value}: {error}");
            }

            return schema.ParseArgs(namedArgs, ctx);
        }

        /// <summary>
        /// 将具名参数字典解析为强类型参数结构体（泛型版本）
        /// </summary>
        public static TActionArgs ParseArgs<TActionArgs, TCtx>(ActionId id, Dictionary<string, ActionArgValue> namedArgs, ExecCtx<TCtx> ctx)
        {
            if (namedArgs == null || namedArgs.Count == 0)
                return default;

            var span = new ReadOnlySpan<KeyValuePair<string, ActionArgValue>>(namedArgs.ToArray());

            if (_schemas.TryGetValue(id, out var s) && s is GenericSchemaAdapter<TActionArgs, TCtx> adapter)
            {
                if (!adapter.Inner.TryValidateArgs(span, out var error))
                {
                    Log.Warning($"[ActionSchemaRegistry] Args validation failed for action={id.Value}: {error}");
                }
                return adapter.Inner.ParseArgs(namedArgs, ctx);
            }

            return default;
        }

        /// <summary>
        /// 解析单个 NumericValueRef 为 double 值
        /// 通用于所有 Schema 实现中的值解析
        /// </summary>
        public static double ResolveNumericRef(in NumericValueRef valueRef, object ctx)
        {
            if (valueRef.Kind == ENumericValueRefKind.Const)
                return valueRef.ConstValue;

            if (ctx == null) return 0.0;

            var ctxType = ctx.GetType();

            if (valueRef.Kind == ENumericValueRefKind.Blackboard)
            {
                var resolver = TryGetBlackboardResolver(ctx);
                if (resolver == null) return 0.0;
                if (!resolver.TryResolve(valueRef.BoardId, out var bb) || bb == null) return 0.0;
                if (!bb.TryGetDouble(valueRef.KeyId, out var v)) return 0.0;
                return v;
            }

            if (valueRef.Kind == ENumericValueRefKind.PayloadField)
            {
                var payloads = TryGetPayloadAccessorRegistry(ctx);
                if (payloads == null) return 0.0;
                return ResolvePayloadFieldReflectively(payloads, valueRef.FieldId, ctx);
            }

            if (valueRef.Kind == ENumericValueRefKind.Var)
            {
                return ResolveNumericVarReflectively(ctx, valueRef.DomainId, valueRef.Key);
            }

            if (valueRef.Kind == ENumericValueRefKind.Expr)
            {
                return ResolveExprReflectively(ctx, valueRef.ExprText);
            }

            return 0.0;
        }

        private static IBlackboardResolver TryGetBlackboardResolver(object ctx)
        {
            var prop = ctx.GetType().GetProperty("Blackboards");
            return prop?.GetValue(ctx) as IBlackboardResolver;
        }

        private static IPayloadAccessorRegistry TryGetPayloadAccessorRegistry(object ctx)
        {
            var prop = ctx.GetType().GetProperty("Payloads");
            return prop?.GetValue(ctx) as IPayloadAccessorRegistry;
        }

        private static double ResolvePayloadFieldReflectively(IPayloadAccessorRegistry registry, int fieldId, object ctx)
        {
            var method = typeof(IPayloadAccessorRegistry)
                .GetMethod("TryGetDouble")
                ?.MakeGenericMethod(ctx.GetType());
            var parameters = new object[] { ctx, fieldId, 0.0 };
            var result = method?.Invoke(registry, parameters);
            if (result is bool success && success)
                return (double)parameters[2];
            return 0.0;
        }

        private static double ResolveNumericVarReflectively(object ctx, string domainId, string key)
        {
            var method = ctx.GetType().GetMethod("TryGetNumericVar");
            if (method == null) return 0.0;
            var parameters = new object[] { domainId, key, 0.0 };
            var result = method.Invoke(ctx, parameters);
            if (result is bool success && success)
                return (double)parameters[2];
            return 0.0;
        }

        private static double ResolveExprReflectively(object ctx, string exprText)
        {
            if (string.IsNullOrEmpty(exprText)) return 0.0;
            if (!NumericExpressionCompiler.TryCompileCached(exprText, out var program) || program == null)
                return 0.0;

            var evalMethod = typeof(NumericExpressionEvaluator).GetMethod("TryEvaluate");
            if (evalMethod == null) return 0.0;

            var parameters = new object[] { ctx, program, 0.0 };
            var result = evalMethod.Invoke(null, parameters);
            if (result is bool success && success)
                return (double)parameters[2];
            return 0.0;
        }

        /// <summary>
        /// 泛型 Schema 的适配器，将 IActionSchema<TActionArgs, TCtx> 适配为 IActionSchema
        /// </summary>
        private sealed class GenericSchemaAdapter<TActionArgs, TCtx> : IActionSchema
        {
            public readonly IActionSchema<TActionArgs, TCtx> Inner;

            public GenericSchemaAdapter(IActionSchema<TActionArgs, TCtx> inner)
            {
                Inner = inner;
            }

            public ActionId ActionId => Inner.ActionId;
            public Type ArgsType => Inner.ArgsType;

        public object ParseArgs(Dictionary<string, ActionArgValue> namedArgs, object ctx)
        {
            var typedCtx = ctx is ExecCtx<TCtx> e ? e : default;
            return Inner.ParseArgs(namedArgs, typedCtx);
        }

        public bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
        {
            return Inner.TryValidateArgs(args, out error);
        }
    }
    }
}
