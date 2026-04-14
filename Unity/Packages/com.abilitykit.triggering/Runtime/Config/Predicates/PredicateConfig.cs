using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Config.Values;

namespace AbilityKit.Triggering.Runtime.Config.Predicates
{
    /// <summary>
    /// 函数条件配置（静态配置数据）
    /// </summary>
    [Serializable]
    public class FunctionPredicateConfig : IPredicateConfig
    {
        public EPredicateKind Kind => EPredicateKind.Function;
        public bool IsEmpty => false;

        public FunctionId FunctionId { get; set; }
        public byte Arity { get; set; }
        public ValueRefConfig Arg0 { get; set; }
        public ValueRefConfig Arg1 { get; set; }
    }

    /// <summary>
    /// 表达式条件配置（静态配置数据）
    /// </summary>
    [Serializable]
    public class ExpressionPredicateConfig : IPredicateConfig
    {
        public EPredicateKind Kind => EPredicateKind.Expression;
        public bool IsEmpty => Nodes == null || Nodes.Count == 0;

        public List<BoolExprNodeConfig> Nodes { get; set; }
    }

    /// <summary>
    /// 布尔表达式节点配置（静态配置数据）
    /// </summary>
    [Serializable]
    public struct BoolExprNodeConfig
    {
        public EBoolExprNodeKind Kind { get; set; }
        public bool ConstValue { get; set; }
        public ECompareOp CompareOp { get; set; }
        public ValueRefConfig Left { get; set; }
        public ValueRefConfig Right { get; set; }
    }
}