using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Executable;
using AbilityKit.Triggering.Variables.Numeric;
using AbilityKit.Triggering.Variables.Numeric.Expression;

namespace AbilityKit.Triggering.Runtime.Plan
{
    public partial class ActionSchemaRegistry
    {
        private static readonly Dictionary<ActionId, IActionSchema> Schemas = new Dictionary<ActionId, IActionSchema>();
        private static readonly object SchemasLock = new object();

        private sealed class GenericSchemaAdapter<TActionArgs, TCtx> : IActionSchema
        {
            private readonly IActionSchema<TActionArgs, TCtx> _inner;

            public GenericSchemaAdapter(IActionSchema<TActionArgs, TCtx> inner)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            }

            public ActionId ActionId => _inner.ActionId;
            public Type ArgsType => typeof(TActionArgs);
            public bool IsDeterministic => true;

            public object ParseArgs(Dictionary<string, ActionArgValue> namedArgs, object ctx)
            {
                return namedArgs;
            }

            public bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error)
            {
                return _inner.TryValidateArgs(args, out error);
            }
        }

        public static bool TryGet(ActionId actionId, out IActionSchema schema)
        {
            lock (SchemasLock)
            {
                return Schemas.TryGetValue(actionId, out schema);
            }
        }

        public static void Register<TActionArgs, TCtx>(IActionSchema<TActionArgs, TCtx> schema)
        {
            if (schema == null)
                throw new ArgumentNullException(nameof(schema));

            lock (SchemasLock)
            {
                Schemas[schema.ActionId] = new GenericSchemaAdapter<TActionArgs, TCtx>(schema);
            }
        }

        public static void Register(ActionId actionId, IActionSchema schema)
        {
            if (schema == null)
                throw new ArgumentNullException(nameof(schema));

            lock (SchemasLock)
            {
                Schemas[actionId] = schema;
            }
        }

        public static object ParseArgs(Dictionary<string, ActionArgValue> namedArgs, object ctx)
        {
            return namedArgs;
        }

        public static object ParseArgs(ActionId actionId, Dictionary<string, ActionArgValue> namedArgs, object ctx)
        {
            return ParseArgs(namedArgs, ctx);
        }

        public static object ParseArgs<TActionArgs, TCtx>(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<TCtx> ctx)
        {
            return namedArgs;
        }

        public static object ParseArgs<TActionArgs, TCtx>(ActionId actionId, Dictionary<string, ActionArgValue> namedArgs, ExecCtx<TCtx> ctx)
        {
            return ParseArgs<TActionArgs, TCtx>(namedArgs, ctx);
        }

        public static object GetParsedArgs(ActionId actionId, Dictionary<string, ActionArgValue> namedArgs, object ctx)
        {
            return namedArgs;
        }

        public static object GetParsedArgs<TActionArgs, TCtx>(ActionId actionId, Dictionary<string, ActionArgValue> namedArgs, ExecCtx<TCtx> ctx)
        {
            return namedArgs;
        }

        public static bool TryResolveNumericRef<TArgs, TCtx>(in NumericValueRef valueRef, in TArgs args, in ExecCtx<TCtx> ctx, out double value)
        {
            if (!TryResolveNumericRefCore(in valueRef, in args, in ctx, out value))
            {
                if (!valueRef.HasFallback || valueRef.Required)
                {
                    value = 0d;
                    return false;
                }

                value = valueRef.FallbackValue;
            }

            value = ApplyNumericValuePolicy(in valueRef, value);
            return true;
        }

        public static bool TryResolveNumericRef(in NumericValueRef valueRef, object args, object ctx, out double value)
        {
            if (ctx is ExecCtx<object> execCtx)
            {
                return TryResolveNumericRef(in valueRef, in args, in execCtx, out value);
            }

            value = default;
            return false;
        }

        private static bool TryResolveNumericRefCore<TArgs, TCtx>(in NumericValueRef valueRef, in TArgs args, in ExecCtx<TCtx> ctx, out double value)
        {
            switch (valueRef.Kind)
            {
                case ENumericValueRefKind.Const:
                    value = valueRef.ConstValue;
                    return true;
                case ENumericValueRefKind.Blackboard:
                    return TryResolveBlackboard(in valueRef, in ctx, out value);
                case ENumericValueRefKind.PayloadField:
                    return TryResolvePayloadField(in valueRef, in args, in ctx, out value);
                case ENumericValueRefKind.Var:
                    return TryResolveVar(in valueRef, in ctx, out value);
                case ENumericValueRefKind.Expr:
                    return TryResolveExpr(in valueRef, in args, in ctx, out value);
                default:
                    value = default;
                    return false;
            }
        }

        private static bool TryResolveBlackboard<TCtx>(in NumericValueRef valueRef, in ExecCtx<TCtx> ctx, out double value)
        {
            value = default;
            if (ctx.Blackboards == null)
            {
                return false;
            }

            if (!ctx.Blackboards.TryResolve(valueRef.BoardId, out var board) || board == null)
            {
                return false;
            }

            if (board is IBlackboardSchema schema &&
                schema.TryGetKeySchema(valueRef.KeyId, out var keySchema) &&
                !keySchema.CanRead)
            {
                return false;
            }

            return board.TryGetDouble(valueRef.KeyId, out value);
        }

        private static bool TryResolvePayloadField<TArgs, TCtx>(in NumericValueRef valueRef, in TArgs args, in ExecCtx<TCtx> ctx, out double value)
        {
            value = default;
            return ctx.Payloads != null && ctx.Payloads.TryGetDouble(in args, valueRef.FieldId, out value);
        }

        private static bool TryResolveVar<TCtx>(in NumericValueRef valueRef, in ExecCtx<TCtx> ctx, out double value)
        {
            value = default;
            if (ctx.NumericDomains == null || string.IsNullOrEmpty(valueRef.DomainId) || string.IsNullOrEmpty(valueRef.Key))
            {
                return false;
            }

            return ctx.NumericDomains.TryGetDomain(valueRef.DomainId, out var domain)
                && domain != null
                && domain.TryGet(in ctx, valueRef.Key, out value);
        }

        private static bool TryResolveExpr<TArgs, TCtx>(in NumericValueRef valueRef, in TArgs args, in ExecCtx<TCtx> ctx, out double value)
        {
            value = default;
            if (string.IsNullOrEmpty(valueRef.ExprText))
            {
                return false;
            }

            if (!NumericExpressionCompiler.TryCompileCached(valueRef.ExprText, out var program) || program == null)
            {
                return false;
            }

            var evalCtx = ctx;
            return NumericExpressionEvaluator.TryEvaluate(in evalCtx, program, out value);
        }

        private static double ApplyNumericValuePolicy(in NumericValueRef valueRef, double value)
        {
            if (valueRef.HasScale) value *= valueRef.Scale;
            if (valueRef.Offset != 0d) value += valueRef.Offset;
            if (valueRef.HasMin && value < valueRef.MinValue) value = valueRef.MinValue;
            if (valueRef.HasMax && value > valueRef.MaxValue) value = valueRef.MaxValue;
            return value;
        }
    }
}
