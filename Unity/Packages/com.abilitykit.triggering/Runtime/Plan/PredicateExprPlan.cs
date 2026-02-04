using System;

namespace AbilityKit.Triggering.Runtime.Plan
{
    public enum EPredicateKind : byte
    {
        None = 0,
        Function = 1,
        Expr = 2,
    }

    public enum EBoolExprNodeKind : byte
    {
        Const = 0,
        Not = 1,
        And = 2,
        Or = 3,
        CompareNumeric = 4,
    }

    public enum ECompareOp : byte
    {
        Eq = 0,
        Ne = 1,
        Gt = 2,
        Ge = 3,
        Lt = 4,
        Le = 5,
    }

    public readonly struct BoolExprNode
    {
        public readonly EBoolExprNodeKind Kind;
        public readonly bool ConstValue;
        public readonly ECompareOp CompareOp;
        public readonly NumericValueRef Left;
        public readonly NumericValueRef Right;

        private BoolExprNode(EBoolExprNodeKind kind, bool constValue, ECompareOp compareOp, NumericValueRef left, NumericValueRef right)
        {
            Kind = kind;
            ConstValue = constValue;
            CompareOp = compareOp;
            Left = left;
            Right = right;
        }

        public static BoolExprNode Const(bool value) => new BoolExprNode(EBoolExprNodeKind.Const, value, default, default, default);
        public static BoolExprNode Not() => new BoolExprNode(EBoolExprNodeKind.Not, default, default, default, default);
        public static BoolExprNode And() => new BoolExprNode(EBoolExprNodeKind.And, default, default, default, default);
        public static BoolExprNode Or() => new BoolExprNode(EBoolExprNodeKind.Or, default, default, default, default);
        public static BoolExprNode Compare(ECompareOp op, NumericValueRef left, NumericValueRef right) => new BoolExprNode(EBoolExprNodeKind.CompareNumeric, default, op, left, right);
    }

    public readonly struct PredicateExprPlan
    {
        public readonly BoolExprNode[] Nodes;

        public PredicateExprPlan(BoolExprNode[] nodes)
        {
            Nodes = nodes;
        }
    }
}
