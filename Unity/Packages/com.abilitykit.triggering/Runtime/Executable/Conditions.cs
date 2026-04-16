using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Variables.Numeric;

namespace AbilityKit.Triggering.Runtime.Executable
{
    // ========================================================================
    // 条件评估结果
    // ========================================================================

    /// <summary>
    /// 条件评估结果
    /// </summary>
    public readonly struct ConditionResult
    {
        public readonly bool Passed;
        public readonly string FailureReason;

        public static ConditionResult Pass => new(true, null);
        public static ConditionResult Fail() => new(false, null);
        public static ConditionResult Fail(string reason) => new(false, reason);

        private ConditionResult(bool passed, string reason)
        {
            Passed = passed;
            FailureReason = reason;
        }

        public ConditionResult And(ConditionResult other)
        {
            if (!Passed) return this;
            return other;
        }

        public ConditionResult Or(ConditionResult other)
        {
            if (Passed) return this;
            return other;
        }

        public ConditionResult Not() => Passed ? Fail("Not") : Pass;
    }

    /// <summary>
    /// 条件元数据
    /// </summary>
    public readonly struct ConditionMetadata
    {
        public readonly int TypeId;
        public readonly string TypeName;

        public ConditionMetadata(int typeId, string typeName)
        {
            TypeId = typeId;
            TypeName = typeName;
        }
    }

    /// <summary>
    /// 条件接口
    /// </summary>
    public interface ICondition
    {
        string Name { get; }
        ConditionMetadata Metadata { get; }
        ConditionResult Evaluate(object ctx);
    }

    // ========================================================================
    // 条件实现
    // ========================================================================

    /// <summary>
    /// 常量条件
    /// </summary>
    [ConditionTypeId(TypeIdRegistry.Condition.Const, "Const")]
    public sealed class ConstCondition : ICondition
    {
        public string Name => "Const";
        public ConditionMetadata Metadata => new(TypeIdRegistry.Condition.Const, "Const");

        public bool Value { get; set; }

        public ConditionResult Evaluate(object ctx)
            => Value ? ConditionResult.Pass : ConditionResult.Fail("Const false");
    }

    /// <summary>
    /// And 组合条件
    /// </summary>
    [ConditionTypeId(TypeIdRegistry.Condition.And, "And")]
    public sealed class AndCondition : ICondition
    {
        public string Name => "And";
        public ConditionMetadata Metadata => new(TypeIdRegistry.Condition.And, "And");

        public ICondition Left { get; set; }
        public ICondition Right { get; set; }

        public ConditionResult Evaluate(object ctx)
        {
            var left = Left?.Evaluate(ctx) ?? ConditionResult.Fail();
            if (!left.Passed) return left;
            var right = Right?.Evaluate(ctx) ?? ConditionResult.Fail();
            return right;
        }
    }

    /// <summary>
    /// Or 组合条件
    /// </summary>
    [ConditionTypeId(TypeIdRegistry.Condition.Or, "Or")]
    public sealed class OrCondition : ICondition
    {
        public string Name => "Or";
        public ConditionMetadata Metadata => new(TypeIdRegistry.Condition.Or, "Or");

        public ICondition Left { get; set; }
        public ICondition Right { get; set; }

        public ConditionResult Evaluate(object ctx)
        {
            var left = Left?.Evaluate(ctx) ?? ConditionResult.Fail();
            if (left.Passed) return left;
            var right = Right?.Evaluate(ctx) ?? ConditionResult.Fail();
            return right;
        }
    }

    /// <summary>
    /// Not 条件
    /// </summary>
    [ConditionTypeId(TypeIdRegistry.Condition.Not, "Not")]
    public sealed class NotCondition : ICondition
    {
        public string Name => "Not";
        public ConditionMetadata Metadata => new(TypeIdRegistry.Condition.Not, "Not");

        public ICondition Inner { get; set; }

        public ConditionResult Evaluate(object ctx)
        {
            var inner = Inner?.Evaluate(ctx) ?? ConditionResult.Pass;
            return inner.Not();
        }
    }

    /// <summary>
    /// 数值比较条件
    /// </summary>
    [ConditionTypeId(TypeIdRegistry.Condition.NumericCompare, "NumericCompare")]
    public sealed class NumericCompareCondition : ICondition
    {
        public string Name => "NumericCompare";
        public ConditionMetadata Metadata => new(TypeIdRegistry.Condition.NumericCompare, "NumericCompare");

        public ECompareOp Op { get; set; }
        public NumericValueRef Left { get; set; }
        public NumericValueRef Right { get; set; }

        public ConditionResult Evaluate(object ctx)
        {
            double leftValue = Left.Resolve(ctx);
            double rightValue = Right.Resolve(ctx);

            bool passed = Op switch
            {
                ECompareOp.Equal => System.Math.Abs(leftValue - rightValue) < 0.0001,
                ECompareOp.NotEqual => System.Math.Abs(leftValue - rightValue) >= 0.0001,
                ECompareOp.GreaterThan => leftValue > rightValue,
                ECompareOp.GreaterThanOrEqual => leftValue >= rightValue,
                ECompareOp.LessThan => leftValue < rightValue,
                ECompareOp.LessThanOrEqual => leftValue <= rightValue,
                _ => false
            };

            return passed
                ? ConditionResult.Pass
                : ConditionResult.Fail($"{leftValue} {Op} {rightValue}");
        }
    }

    /// <summary>
    /// Payload 字段数值比较条件
    /// </summary>
    [ConditionTypeId(TypeIdRegistry.Condition.PayloadCompare, "PayloadCompare")]
    public sealed class PayloadCompareCondition : ICondition
    {
        public string Name => "PayloadCompare";
        public ConditionMetadata Metadata => new(TypeIdRegistry.Condition.PayloadCompare, "PayloadCompare");

        public int FieldId { get; set; }
        public ECompareOp Op { get; set; }
        public NumericValueRef CompareValue { get; set; }
        public bool Negate { get; set; }

        public ConditionResult Evaluate(object ctx)
        {
            double value = 0;
            bool found = false;

            if (ctx is IHasPayload payload)
            {
                found = payload.TryGetPayloadDouble(FieldId, out value);
            }

            if (!found)
                return ConditionResult.Fail($"Payload field {FieldId} not found");

            double compareValue = CompareValue.Resolve(ctx);

            bool passed = Op switch
            {
                ECompareOp.Equal => System.Math.Abs(value - compareValue) < 0.0001,
                ECompareOp.NotEqual => System.Math.Abs(value - compareValue) >= 0.0001,
                ECompareOp.GreaterThan => value > compareValue,
                ECompareOp.GreaterThanOrEqual => value >= compareValue,
                ECompareOp.LessThan => value < compareValue,
                ECompareOp.LessThanOrEqual => value <= compareValue,
                _ => false
            };

            if (Negate) passed = !passed;

            return passed
                ? ConditionResult.Pass
                : ConditionResult.Fail($"Payload[{FieldId}]={value} {Op} {compareValue}");
        }
    }

    /// <summary>
    /// 目标存在条件
    /// </summary>
    [ConditionTypeId(TypeIdRegistry.Condition.HasTarget, "HasTarget")]
    public sealed class HasTargetCondition : ICondition
    {
        public string Name => "HasTarget";
        public ConditionMetadata Metadata => new(TypeIdRegistry.Condition.HasTarget, "HasTarget");

        public bool Negate { get; set; }

        public ConditionResult Evaluate(object ctx)
        {
            bool hasTarget = false;

            if (ctx is IHasPayload payload)
            {
                hasTarget = payload.Target != null;
            }

            if (Negate) hasTarget = !hasTarget;

            return hasTarget
                ? ConditionResult.Pass
                : ConditionResult.Fail(Negate ? "Has target" : "No target");
        }
    }

    /// <summary>
    /// 多条件组合器
    /// </summary>
    [ConditionTypeId(TypeIdRegistry.Condition.Multi, "Multi")]
    public sealed class MultiCondition : ICondition
    {
        public string Name => "Multi";
        public ConditionMetadata Metadata => new(TypeIdRegistry.Condition.Multi, "Multi");

        public EConditionCombinator Combinator { get; set; } = EConditionCombinator.And;
        public List<ICondition> Conditions { get; set; } = new();

        public ConditionResult Evaluate(object ctx)
        {
            if (Conditions == null || Conditions.Count == 0)
                return ConditionResult.Pass;

            var result = ConditionResult.Pass;

            foreach (var condition in Conditions)
            {
                var r = condition?.Evaluate(ctx) ?? ConditionResult.Pass;

                if (Combinator == EConditionCombinator.And)
                {
                    result = result.And(r);
                    if (!result.Passed) break;
                }
                else
                {
                    result = result.Or(r);
                    if (result.Passed) break;
                }
            }

            return result;
        }

        public MultiCondition Add(ICondition condition)
        {
            Conditions ??= new List<ICondition>();
            Conditions.Add(condition);
            return this;
        }

        public static MultiCondition And(params ICondition[] conditions)
        {
            var multi = new MultiCondition { Combinator = EConditionCombinator.And };
            foreach (var c in conditions) multi.Add(c);
            return multi;
        }

        public static MultiCondition Or(params ICondition[] conditions)
        {
            var multi = new MultiCondition { Combinator = EConditionCombinator.Or };
            foreach (var c in conditions) multi.Add(c);
            return multi;
        }
    }

    // ========================================================================
    // 辅助接口
    // ========================================================================

    /// <summary>
    /// 带有 Payload 的上下文接口
    /// </summary>
    public interface IHasPayload
    {
        bool TryGetPayloadDouble(int fieldId, out double value);
        object Target { get; }
    }
}
