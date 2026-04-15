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

    // ========================================================================
    // 内建条件配置
    // ========================================================================

    /// <summary>
    /// 距离检查条件配置（静态配置数据）
    /// 
    /// 使用方式：
    /// 1. SourceKey 和 TargetKey 指定位置来源（"position", "target", "caster"）
    /// 2. 通过 MaxDistance/MinDistance 指定距离范围
    /// 3. 通过 Op 指定比较操作
    /// </summary>
    [Serializable]
    public class DistanceCheckPredicateConfig : IPredicateConfig
    {
        public EPredicateKind Kind => EPredicateKind.DistanceCheck;
        public bool IsEmpty => false;

        /// <summary>源位置键（默认为 "position" 或 "caster"）</summary>
        public string SourceKey { get; set; } = "position";

        /// <summary>目标位置键（默认为 "target" 或从 Target 获取）</summary>
        public string TargetKey { get; set; } = "target";

        /// <summary>最小距离（包含）</summary>
        public float MinDistance { get; set; } = 0;

        /// <summary>最大距离（包含）</summary>
        public float MaxDistance { get; set; } = float.MaxValue;

        /// <summary>比较操作符</summary>
        public EDistanceCompareOp Op { get; set; } = EDistanceCompareOp.InRange;
    }

    /// <summary>
    /// 距离比较操作符
    /// </summary>
    public enum EDistanceCompareOp : byte
    {
        /// <summary>在范围内（MinDistance &lt;= d &lt;= MaxDistance）</summary>
        InRange = 0,
        /// <summary>在范围外（d &lt; MinDistance 或 d &gt; MaxDistance）</summary>
        OutOfRange = 1,
        /// <summary>小于（d &lt; MaxDistance）</summary>
        LessThan = 2,
        /// <summary>大于（d &gt; MinDistance）</summary>
        GreaterThan = 3,
        /// <summary>等于（近似）</summary>
        Equal = 4,
    }

    /// <summary>
    /// 生命值检查条件配置（静态配置数据）
    /// 
    /// 使用方式：
    /// 1. TargetKey 指定目标（"target", "self", "caster"）
    /// 2. 通过 MaxHealth/MinHealth 指定生命值范围
    /// 3. 通过 Op 指定比较操作
    /// </summary>
    [Serializable]
    public class HealthCheckPredicateConfig : IPredicateConfig
    {
        public EPredicateKind Kind => EPredicateKind.HealthCheck;
        public bool IsEmpty => false;

        /// <summary>目标键（默认为 "target"）</summary>
        public string TargetKey { get; set; } = "target";

        /// <summary>最小生命值（包含）</summary>
        public float MinHealth { get; set; } = 0;

        /// <summary>最大生命值（包含）</summary>
        public float MaxHealth { get; set; } = float.MaxValue;

        /// <summary>比较操作符</summary>
        public EHealthCompareOp Op { get; set; } = EHealthCompareOp.InRange;
    }

    /// <summary>
    /// 生命值比较操作符
    /// </summary>
    public enum EHealthCompareOp : byte
    {
        /// <summary>在范围内（MinHealth &lt;= hp &lt;= MaxHealth）</summary>
        InRange = 0,
        /// <summary>在范围外（hp &lt; MinHealth 或 hp &gt; MaxHealth）</summary>
        OutOfRange = 1,
        /// <summary>小于（hp &lt; MaxHealth）</summary>
        LessThan = 2,
        /// <summary>大于（hp &gt; MinHealth）</summary>
        GreaterThan = 3,
        /// <summary>等于（近似）</summary>
        Equal = 4,
        /// <summary>存活（hp &gt; 0）</summary>
        Alive = 10,
        /// <summary>死亡（hp &lt;= 0）</summary>
        Dead = 11,
        /// <summary>满血（hp == MaxHealth）</summary>
        Full = 12,
    }
}