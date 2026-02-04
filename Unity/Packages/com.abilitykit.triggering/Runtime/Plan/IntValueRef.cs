using System;

namespace AbilityKit.Triggering.Runtime.Plan
{
    public enum ENumericValueRefKind : byte
    {
        Const = 0,
        Blackboard = 1,
        PayloadField = 2,
        Var = 3,
        Expr = 4,
    }

    public readonly struct NumericValueRef : IEquatable<NumericValueRef>
    {
        public readonly ENumericValueRefKind Kind;
        public readonly double ConstValue;
        public readonly int BoardId;
        public readonly int KeyId;
        public readonly int FieldId;
        public readonly string DomainId;
        public readonly string Key;
        public readonly string ExprText;

        private NumericValueRef(ENumericValueRefKind kind, double constValue, int boardId, int keyId, int fieldId, string domainId, string key, string exprText)
        {
            Kind = kind;
            ConstValue = constValue;
            BoardId = boardId;
            KeyId = keyId;
            FieldId = fieldId;
            DomainId = domainId;
            Key = key;
            ExprText = exprText;
        }

        public static NumericValueRef Const(double value) => new NumericValueRef(ENumericValueRefKind.Const, value, 0, 0, 0, null, null, null);
        public static NumericValueRef Blackboard(int boardId, int keyId) => new NumericValueRef(ENumericValueRefKind.Blackboard, 0d, boardId, keyId, 0, null, null, null);
        public static NumericValueRef PayloadField(int fieldId) => new NumericValueRef(ENumericValueRefKind.PayloadField, 0d, 0, 0, fieldId, null, null, null);
        public static NumericValueRef Var(string domainId, string key) => new NumericValueRef(ENumericValueRefKind.Var, 0d, 0, 0, 0, domainId, key, null);
        public static NumericValueRef Expr(string exprText) => new NumericValueRef(ENumericValueRefKind.Expr, 0d, 0, 0, 0, null, null, exprText);

        public bool Equals(NumericValueRef other)
        {
            return Kind == other.Kind
                   && ConstValue == other.ConstValue
                   && BoardId == other.BoardId
                   && KeyId == other.KeyId
                   && FieldId == other.FieldId
                   && string.Equals(DomainId, other.DomainId, StringComparison.Ordinal)
                   && string.Equals(Key, other.Key, StringComparison.Ordinal)
                   && string.Equals(ExprText, other.ExprText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is NumericValueRef other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                var h = (int)Kind;
                h = (h * 397) ^ ConstValue.GetHashCode();
                h = (h * 397) ^ BoardId;
                h = (h * 397) ^ KeyId;
                h = (h * 397) ^ FieldId;
                h = (h * 397) ^ (DomainId != null ? DomainId.GetHashCode() : 0);
                h = (h * 397) ^ (Key != null ? Key.GetHashCode() : 0);
                h = (h * 397) ^ (ExprText != null ? ExprText.GetHashCode() : 0);
                return h;
            }
        }
    }
}
