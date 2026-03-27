using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityHFSM.Graph.Conditions
{
    /// <summary>
    /// 参数比较条件 - 用于比较参数值与指定值
    /// </summary>
    [Serializable]
    public class HfsmParameterCondition : HfsmTransitionCondition
    {
        /// <summary>
        /// 参数名称
        /// </summary>
        public string ParameterName;

        /// <summary>
        /// 比较操作符
        /// </summary>
        public HfsmCompareOperator Operator = HfsmCompareOperator.Equal;

        /// <summary>
        /// 比较值（根据参数类型使用不同的字段）
        /// </summary>
        public bool BoolValue;
        public float FloatValue;
        public int IntValue;

        /// <summary>
        /// 参数类型（用于确定使用哪个值字段）
        /// </summary>
        public HfsmParameterType ParameterType = HfsmParameterType.Bool;

        public override string TypeName => "ParameterCompare";

        public override string DisplayName
        {
            get
            {
                return ParameterType switch
                {
                    HfsmParameterType.Bool => "Bool Parameter",
                    HfsmParameterType.Float => "Float Parameter",
                    HfsmParameterType.Int => "Int Parameter",
                    HfsmParameterType.Trigger => "Trigger Parameter",
                    _ => "Parameter"
                };
            }
        }

        public override string GetDescription()
        {
            string op = Operator switch
            {
                HfsmCompareOperator.Equal => "==",
                HfsmCompareOperator.NotEqual => "!=",
                HfsmCompareOperator.GreaterThan => ">",
                HfsmCompareOperator.LessThan => "<",
                HfsmCompareOperator.GreaterOrEqual => ">=",
                HfsmCompareOperator.LessOrEqual => "<=",
                _ => "?"
            };

            return ParameterType switch
            {
                HfsmParameterType.Bool => $"{ParameterName} = {BoolValue}",
                HfsmParameterType.Float => $"{ParameterName} {op} {FloatValue}",
                HfsmParameterType.Int => $"{ParameterName} {op} {IntValue}",
                HfsmParameterType.Trigger => $"{ParameterName} Triggered",
                _ => $"{ParameterName} {op} ?"
            };
        }

        public override bool Evaluate(IHfsmEvaluationContext context)
        {
            if (string.IsNullOrEmpty(ParameterName))
                return false;

            switch (ParameterType)
            {
                case HfsmParameterType.Bool:
                    return EvaluateBool(context);
                case HfsmParameterType.Float:
                    return EvaluateFloat(context);
                case HfsmParameterType.Int:
                    return EvaluateInt(context);
                case HfsmParameterType.Trigger:
                    return context.GetTrigger(ParameterName);
                default:
                    return false;
            }
        }

        private bool EvaluateBool(IHfsmEvaluationContext context)
        {
            bool paramValue = context.GetBool(ParameterName);
            return paramValue == BoolValue;
        }

        private bool EvaluateFloat(IHfsmEvaluationContext context)
        {
            float paramValue = context.GetFloat(ParameterName);
            return Compare(paramValue, FloatValue);
        }

        private bool EvaluateInt(IHfsmEvaluationContext context)
        {
            int paramValue = context.GetInt(ParameterName);
            return Compare(paramValue, IntValue);
        }

        private bool Compare(float left, float right)
        {
            return Operator switch
            {
                HfsmCompareOperator.Equal => Mathf.Approximately(left, right),
                HfsmCompareOperator.NotEqual => !Mathf.Approximately(left, right),
                HfsmCompareOperator.GreaterThan => left > right,
                HfsmCompareOperator.LessThan => left < right,
                HfsmCompareOperator.GreaterOrEqual => left >= right,
                HfsmCompareOperator.LessOrEqual => left <= right,
                _ => false
            };
        }

        private bool Compare(int left, int right)
        {
            return Operator switch
            {
                HfsmCompareOperator.Equal => left == right,
                HfsmCompareOperator.NotEqual => left != right,
                HfsmCompareOperator.GreaterThan => left > right,
                HfsmCompareOperator.LessThan => left < right,
                HfsmCompareOperator.GreaterOrEqual => left >= right,
                HfsmCompareOperator.LessOrEqual => left <= right,
                _ => false
            };
        }

        public override HfsmTransitionCondition Clone()
        {
            return new HfsmParameterCondition
            {
                ParameterName = ParameterName,
                Operator = Operator,
                BoolValue = BoolValue,
                FloatValue = FloatValue,
                IntValue = IntValue,
                ParameterType = ParameterType
            };
        }

        public override string[] GetRequiredParameters()
        {
            return string.IsNullOrEmpty(ParameterName) ? Array.Empty<string>() : new[] { ParameterName };
        }

        public override void SetFromConfig(Dictionary<string, object> config)
        {
            if (config.TryGetValue("ParameterName", out var name))
                ParameterName = name as string ?? "";

            if (config.TryGetValue("Operator", out var op))
                Operator = (HfsmCompareOperator)(int)op;

            if (config.TryGetValue("ParameterType", out var type))
                ParameterType = (HfsmParameterType)(int)type;

            if (config.TryGetValue("BoolValue", out var bval))
                BoolValue = bval is bool b ? b : Convert.ToBoolean(bval);

            if (config.TryGetValue("FloatValue", out var fval))
                FloatValue = fval is float f ? f : Convert.ToSingle(fval);

            if (config.TryGetValue("IntValue", out var ival))
                IntValue = ival is int i ? i : Convert.ToInt32(ival);
        }

        public override Dictionary<string, object> ToConfig()
        {
            var config = new Dictionary<string, object>
            {
                ["ParameterName"] = ParameterName,
                ["Operator"] = (int)Operator,
                ["ParameterType"] = (int)ParameterType,
                ["BoolValue"] = BoolValue,
                ["FloatValue"] = FloatValue,
                ["IntValue"] = IntValue
            };
            return config;
        }
    }
}
