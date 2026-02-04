using System;

namespace AbilityKit.Triggering.Runtime.Plan
{
    public enum ERpnNumericNodeKind : byte
    {
        Push = 0,
        Add = 1,
        Sub = 2,
        Mul = 3,
        Div = 4,
    }

    public readonly struct RpnNumericNode
    {
        public readonly ERpnNumericNodeKind Kind;
        public readonly NumericValueRef Value;

        private RpnNumericNode(ERpnNumericNodeKind kind, NumericValueRef value)
        {
            Kind = kind;
            Value = value;
        }

        public static RpnNumericNode Push(NumericValueRef value) => new RpnNumericNode(ERpnNumericNodeKind.Push, value);
        public static RpnNumericNode Add() => new RpnNumericNode(ERpnNumericNodeKind.Add, default);
        public static RpnNumericNode Sub() => new RpnNumericNode(ERpnNumericNodeKind.Sub, default);
        public static RpnNumericNode Mul() => new RpnNumericNode(ERpnNumericNodeKind.Mul, default);
        public static RpnNumericNode Div() => new RpnNumericNode(ERpnNumericNodeKind.Div, default);
    }

    public readonly struct RpnNumericExprPlan
    {
        public readonly string ExprLang;
        public readonly string ExprText;
        public readonly RpnNumericNode[] Nodes;

        public bool IsCompiled => Nodes != null;

        public RpnNumericExprPlan(string exprLang, string exprText)
        {
            ExprLang = exprLang;
            ExprText = exprText;
            Nodes = null;
        }

        public RpnNumericExprPlan(string exprLang, RpnNumericNode[] nodes)
        {
            ExprLang = exprLang;
            ExprText = null;
            Nodes = nodes;
        }
    }
}
