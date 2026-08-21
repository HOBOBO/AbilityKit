using System;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Blackboard
{
    public static class BlackboardMutation
    {
        public static bool TrySetValue(
            IBlackboardResolver resolver,
            in BlackboardWriteTarget target,
            in ActionArgValue value,
            out string error)
        {
            switch (value.Kind)
            {
                case ActionArgKind.NumericValue:
                    if (value.Ref.Kind != ENumericValueRefKind.Const)
                    {
                        error = "Typed Blackboard writes require a resolved numeric value.";
                        return false;
                    }
                    return TrySetNumeric(resolver, in target, value.Ref.ConstValue, out error);
                case ActionArgKind.BooleanValue:
                    return TrySetBool(resolver, in target, value.BooleanValue, out error);
                case ActionArgKind.StringValue:
                    return TrySetString(resolver, in target, value.StringValue, out error);
                default:
                    error = $"Action argument kind {value.Kind} is not a writable Blackboard value.";
                    return false;
            }
        }

        public static bool TrySetBool(
            IBlackboardResolver resolver,
            in BlackboardWriteTarget target,
            bool value,
            out string error)
        {
            if (!TryResolveWritable(resolver, in target, BlackboardKeyType.Bool, out var board, out error))
                return false;
            board.SetBool(target.KeyId, value);
            error = null;
            return true;
        }

        public static bool TrySetString(
            IBlackboardResolver resolver,
            in BlackboardWriteTarget target,
            string value,
            out string error)
        {
            if (!TryResolveWritable(resolver, in target, BlackboardKeyType.String, out var board, out error))
                return false;
            board.SetString(target.KeyId, value ?? string.Empty);
            error = null;
            return true;
        }

        public static bool TrySetNumeric(
            IBlackboardResolver resolver,
            in BlackboardWriteTarget target,
            double value,
            out string error)
        {
            if (!TryResolveWritable(resolver, in target, target.KeyType, out var board, out error))
                return false;

            if (target.KeyType != BlackboardKeyType.Int && target.KeyType != BlackboardKeyType.Float && target.KeyType != BlackboardKeyType.Double)
            {
                error = $"Blackboard key type {target.KeyType} is not numeric.";
                return false;
            }

            if (!((IBlackboardSchema)board).TryGetKeySchema(target.KeyId, out var schema))
            {
                error = $"Blackboard key schema was not found. boardId={target.BoardId} keyId={target.KeyId}.";
                return false;
            }

            switch (schema.Type)
            {
                case BlackboardKeyType.Int:
                    if (double.IsNaN(value) || double.IsInfinity(value) || value < int.MinValue || value > int.MaxValue)
                    {
                        error = $"Numeric value {value} cannot be represented as Int32.";
                        return false;
                    }
                    board.SetInt(target.KeyId, (int)Math.Round(value));
                    break;
                case BlackboardKeyType.Float:
                    if (double.IsNaN(value) || double.IsInfinity(value) || value < -float.MaxValue || value > float.MaxValue)
                    {
                        error = $"Numeric value {value} cannot be represented as Single.";
                        return false;
                    }
                    board.SetFloat(target.KeyId, (float)value);
                    break;
                case BlackboardKeyType.Double:
                    board.SetDouble(target.KeyId, value);
                    break;
                default:
                    error = $"Blackboard key type {schema.Type} is not numeric.";
                    return false;
            }

            error = null;
            return true;
        }

        public static bool TryAddNumeric(
            IBlackboardResolver resolver,
            in BlackboardWriteTarget target,
            double delta,
            out string error)
        {
            if (!TryResolveWritable(resolver, in target, target.KeyType, out var board, out error))
                return false;
            if (target.KeyType != BlackboardKeyType.Int && target.KeyType != BlackboardKeyType.Float && target.KeyType != BlackboardKeyType.Double)
            {
                error = $"Blackboard key type {target.KeyType} is not numeric.";
                return false;
            }
            if (!board.TryGetDouble(target.KeyId, out var current))
            {
                error = $"Blackboard key has no numeric value. boardId={target.BoardId} keyId={target.KeyId}.";
                return false;
            }

            return TrySetNumeric(resolver, in target, current + delta, out error);
        }

        private static bool TryResolveWritable(
            IBlackboardResolver resolver,
            in BlackboardWriteTarget target,
            BlackboardKeyType expectedType,
            out IBlackboard board,
            out string error)
        {
            board = null;
            if (resolver == null || !resolver.TryResolve(target.BoardId, out board) || board == null)
            {
                error = $"Blackboard was not found. boardId={target.BoardId}.";
                return false;
            }
            if (!(board is IBlackboardSchema schemaProvider) || !schemaProvider.TryGetKeySchema(target.KeyId, out var schema))
            {
                error = $"Blackboard key schema was not found. boardId={target.BoardId} keyId={target.KeyId}.";
                return false;
            }
            if (!schema.CanWrite)
            {
                error = $"Blackboard key is read-only. boardId={target.BoardId} keyId={target.KeyId}.";
                return false;
            }
            if (schema.Type != expectedType || schema.Type != target.KeyType)
            {
                error = $"Blackboard target type mismatch. schema={schema.Type} target={target.KeyType} value={expectedType}.";
                return false;
            }
            error = null;
            return true;
        }
    }
}
