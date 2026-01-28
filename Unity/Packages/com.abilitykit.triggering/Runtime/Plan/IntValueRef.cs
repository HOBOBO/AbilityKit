using System;

namespace AbilityKit.Triggering.Runtime.Plan
{
    public enum EIntValueRefKind : byte
    {
        Const = 0,
        Blackboard = 1,
        PayloadField = 2,
    }

    public readonly struct IntValueRef : IEquatable<IntValueRef>
    {
        public readonly EIntValueRefKind Kind;
        public readonly int ConstValue;
        public readonly int BoardId;
        public readonly int KeyId;
        public readonly int FieldId;

        private IntValueRef(EIntValueRefKind kind, int constValue, int boardId, int keyId, int fieldId)
        {
            Kind = kind;
            ConstValue = constValue;
            BoardId = boardId;
            KeyId = keyId;
            FieldId = fieldId;
        }

        public static IntValueRef Const(int value) => new IntValueRef(EIntValueRefKind.Const, value, 0, 0, 0);
        public static IntValueRef Blackboard(int boardId, int keyId) => new IntValueRef(EIntValueRefKind.Blackboard, 0, boardId, keyId, 0);
        public static IntValueRef PayloadField(int fieldId) => new IntValueRef(EIntValueRefKind.PayloadField, 0, 0, 0, fieldId);

        public bool Equals(IntValueRef other)
        {
            return Kind == other.Kind && ConstValue == other.ConstValue && BoardId == other.BoardId && KeyId == other.KeyId && FieldId == other.FieldId;
        }

        public override bool Equals(object obj) => obj is IntValueRef other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                var h = (int)Kind;
                h = (h * 397) ^ ConstValue;
                h = (h * 397) ^ BoardId;
                h = (h * 397) ^ KeyId;
                h = (h * 397) ^ FieldId;
                return h;
            }
        }
    }
}
