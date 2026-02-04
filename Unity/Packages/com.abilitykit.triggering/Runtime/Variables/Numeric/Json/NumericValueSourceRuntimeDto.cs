using System;

namespace AbilityKit.Triggering.Variables.Numeric.Json
{
    [Serializable]
    public sealed class NumericValueSourceRuntimeDto
    {
        public string Kind;
        public double ConstValue;
        public string DomainId;
        public string Key;
        public string Expr;
    }

    public static class NumericValueSourceRuntimeDtoBuilder
    {
        public static bool TryBuild(NumericValueSourceRuntimeDto dto, out NumericValueSourceRuntime value)
        {
            value = default;
            if (dto == null) return false;

            if (!Enum.TryParse<ENumericValueSourceKind>(dto.Kind, ignoreCase: true, out var kind))
            {
                return false;
            }

            switch (kind)
            {
                case ENumericValueSourceKind.Const:
                    value = NumericValueSourceRuntime.Const(dto.ConstValue);
                    return true;

                case ENumericValueSourceKind.Var:
                    if (string.IsNullOrEmpty(dto.DomainId) || string.IsNullOrEmpty(dto.Key)) return false;
                    value = NumericValueSourceRuntime.Var(dto.DomainId, dto.Key);
                    return true;

                case ENumericValueSourceKind.Expr:
                    if (string.IsNullOrEmpty(dto.Expr)) return false;
                    value = NumericValueSourceRuntime.ExprText(dto.Expr);
                    return true;

                default:
                    return false;
            }
        }

        public static NumericValueSourceRuntimeDto ToDto(in NumericValueSourceRuntime value)
        {
            var dto = new NumericValueSourceRuntimeDto();
            dto.Kind = value.Kind.ToString();
            switch (value.Kind)
            {
                case ENumericValueSourceKind.Const:
                    dto.ConstValue = value.ConstValue;
                    break;

                case ENumericValueSourceKind.Var:
                    dto.DomainId = value.VarRef.DomainId;
                    dto.Key = value.VarRef.Key;
                    break;

                case ENumericValueSourceKind.Expr:
                    dto.Expr = value.Expr;
                    break;
            }

            return dto;
        }
    }
}
