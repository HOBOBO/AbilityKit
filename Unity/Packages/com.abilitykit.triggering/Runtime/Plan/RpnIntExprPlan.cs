using System;

namespace AbilityKit.Triggering.Runtime.Plan
{
    public enum ERpnIntNodeKind : byte
    {
        Push = 0,
        Add = 1,
        Sub = 2,
        Mul = 3,
        Div = 4,
    }

    public readonly struct RpnIntNode
    {
        public readonly ERpnIntNodeKind Kind;
        public readonly IntValueRef Value;

        private RpnIntNode(ERpnIntNodeKind kind, IntValueRef value)
        {
            Kind = kind;
            Value = value;
        }

        public static RpnIntNode Push(IntValueRef value) => new RpnIntNode(ERpnIntNodeKind.Push, value);
        public static RpnIntNode Add() => new RpnIntNode(ERpnIntNodeKind.Add, default);
        public static RpnIntNode Sub() => new RpnIntNode(ERpnIntNodeKind.Sub, default);
        public static RpnIntNode Mul() => new RpnIntNode(ERpnIntNodeKind.Mul, default);
        public static RpnIntNode Div() => new RpnIntNode(ERpnIntNodeKind.Div, default);
    }

    public readonly struct RpnIntExprPlan
    {
        public readonly string ExprLang;
        public readonly string ExprText;
        public readonly RpnIntNode[] Nodes;

        public bool IsCompiled => Nodes != null;

        public RpnIntExprPlan(string exprLang, string exprText)
        {
            ExprLang = exprLang;
            ExprText = exprText;
            Nodes = null;
        }

        public RpnIntExprPlan(string exprLang, RpnIntNode[] nodes)
        {
            ExprLang = exprLang;
            ExprText = null;
            Nodes = nodes;
        }
    }
}
