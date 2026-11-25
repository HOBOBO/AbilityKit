using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Variables.Numeric;

namespace AbilityKit.Triggering.Runtime.Plan.Json
{
    /// <summary>
    /// ? JSON ?????????????????????????????????????
    /// </summary>
    internal readonly struct TriggerPlanJsonValueConverter
    {
        private const string TemplateParamKind = "TemplateParam";
        private readonly Dictionary<string, TriggerPlanJsonDatabase.NumericValueRefDto> _templateBindings;

        internal TriggerPlanJsonValueConverter(TriggerPlanJsonDatabase.TriggerTemplateBindingDto template)
        {
            _templateBindings = template?.Bindings == null || template.Bindings.Count == 0
                ? null
                : new Dictionary<string, TriggerPlanJsonDatabase.NumericValueRefDto>(template.Bindings, StringComparer.OrdinalIgnoreCase);
        }

        internal NumericValueRef ConvertNumeric(TriggerPlanJsonDatabase.NumericValueRefDto dto)
        {
            if (dto == null) return default;

            if (string.Equals(dto.Kind, TemplateParamKind, StringComparison.OrdinalIgnoreCase))
                dto = ResolveTemplateParam(dto);

            if (!Enum.TryParse<ENumericValueRefKind>(dto.Kind, out var kind))
            {
                throw new InvalidOperationException($"Unknown NumericValueRef kind: {dto.Kind}");
            }

            var valueRef = kind switch
            {
                ENumericValueRefKind.Const => NumericValueRef.Const(dto.ConstValue),
                ENumericValueRefKind.Blackboard => NumericValueRef.Blackboard(dto.BoardId, dto.KeyId),
                ENumericValueRefKind.PayloadField => NumericValueRef.PayloadField(dto.FieldId),
                ENumericValueRefKind.Var => NumericValueRef.Var(dto.DomainId, dto.Key),
                ENumericValueRefKind.Expr => NumericValueRef.Expr(dto.ExprText),
                _ => throw new InvalidOperationException($"Unsupported NumericValueRef kind: {kind}")
            };

            if (dto.Required) valueRef = valueRef.AsRequired();
            if (dto.HasFallback) valueRef = valueRef.WithFallback(dto.FallbackValue);
            if (dto.HasMin) valueRef = valueRef.WithMin(dto.MinValue);
            if (dto.HasMax) valueRef = valueRef.WithMax(dto.MaxValue);
            if (dto.HasScale) valueRef = valueRef.WithScale(dto.Scale);
            if (dto.Offset != 0d) valueRef = valueRef.WithOffset(dto.Offset);
            if (!string.IsNullOrEmpty(dto.Label)) valueRef = valueRef.WithLabel(dto.Label);
            if (!string.IsNullOrEmpty(dto.Scope)) valueRef = valueRef.WithScope(dto.Scope);

            return valueRef;
        }

        internal ActionArgValue ConvertActionArgument(TriggerPlanJsonDatabase.NumericValueRefDto dto, string name)
        {
            if (dto != null && string.Equals(dto.Kind, "BlackboardTarget", StringComparison.OrdinalIgnoreCase))
            {
                if (dto.BoardId == 0 || dto.KeyId == 0)
                    throw new InvalidOperationException($"Blackboard target requires non-zero BoardId and KeyId. argument={name}");
                if (dto.KeyType == BlackboardKeyType.Unknown)
                    throw new InvalidOperationException($"Blackboard target requires KeyType. argument={name}");

                var target = new BlackboardWriteTarget(dto.BoardId, dto.KeyId, dto.KeyType, dto.Scope);
                return ActionArgValue.OfBlackboardTarget(in target, name);
            }

            if (dto != null && string.Equals(dto.Kind, "BlackboardValue", StringComparison.OrdinalIgnoreCase))
            {
                if (dto.BoardId == 0 || dto.KeyId == 0)
                    throw new InvalidOperationException($"Blackboard value requires non-zero BoardId and KeyId. argument={name}");
                if (dto.KeyType == BlackboardKeyType.Unknown)
                    throw new InvalidOperationException($"Blackboard value requires KeyType. argument={name}");

                var value = new BlackboardValueRef(dto.BoardId, dto.KeyId, dto.KeyType);
                return ActionArgValue.OfBlackboardValue(in value, name);
            }

            if (dto != null && string.Equals(dto.Kind, "Bool", StringComparison.OrdinalIgnoreCase))
                return ActionArgValue.OfBool(dto.BoolValue, name);
            if (dto != null && string.Equals(dto.Kind, "String", StringComparison.OrdinalIgnoreCase))
                return ActionArgValue.OfString(dto.StringValue, name);

            return new ActionArgValue(ConvertNumeric(dto), name);
        }

        internal BlackboardWriteTarget ConvertBlackboardTarget(
            TriggerPlanJsonDatabase.NumericValueRefDto dto,
            string name)
        {
            var value = ConvertActionArgument(dto, name);
            if (value.Kind != ActionArgKind.BlackboardTarget)
                throw new InvalidOperationException($"Execution node field '{name}' must be a BlackboardTarget.");
            return value.BlackboardTarget;
        }

        private TriggerPlanJsonDatabase.NumericValueRefDto ResolveTemplateParam(
            TriggerPlanJsonDatabase.NumericValueRefDto dto)
        {
            var resolving = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (dto != null && string.Equals(dto.Kind, TemplateParamKind, StringComparison.OrdinalIgnoreCase))
            {
                var key = dto.Key;
                if (string.IsNullOrEmpty(key))
                {
                    throw new InvalidOperationException("TemplateParam NumericValueRef requires Key.");
                }

                if (_templateBindings == null || !_templateBindings.TryGetValue(key, out var bound) || bound == null)
                {
                    throw new InvalidOperationException($"Template parameter is not bound: {key}");
                }

                if (!resolving.Add(key))
                {
                    throw new InvalidOperationException($"Cyclic template parameter binding detected: {key}");
                }

                dto = bound;
            }

            return dto;
        }
    }
}
