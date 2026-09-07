using System;
using System.Collections.Generic;
using UnityEngine;

namespace AbilityKit.HFSM.Graph.Conditions
{
    /// <summary>
    /// 参数比较条件 - 用于比较参数值与指定值
    /// </summary>
    [Serializable]
    public class ParameterCondition : TransitionCondition
    {
        /// <summary>
        /// 参数名称
        /// </summary>
        public string ParameterName;

        /// <summary>
        /// 比较操作符
        /// </summary>
        public CompareOperator Operator = CompareOperator.Equal;

        /// <summary>
        /// 比较值（根据参数类型使用不同的字段）
        /// </summary>
        public bool BoolValue;
        public float FloatValue;
        public int IntValue;

        /// <summary>
        /// 参数类型（用于确定使用哪个值字段）
        /// </summary>
        public ParameterValueType ParameterType = ParameterValueType.Bool;

        public override string TypeName => "ParameterCompare";

        public override string DisplayName
        {
            get
            {
                return ParameterType switch
                {
                    ParameterValueType.Bool => "Bool Parameter",
                    ParameterValueType.Float => "Float Parameter",
                    ParameterValueType.Int => "Int Parameter",
                    ParameterValueType.Trigger => "Trigger Parameter",
                    _ => "Parameter"
                };
            }
        }

        public override string GetDescription()
        {
            string op = Operator switch
            {
                CompareOperator.Equal => "==",
                CompareOperator.NotEqual => "!=",
                CompareOperator.GreaterThan => ">",
                CompareOperator.LessThan => "<",
                CompareOperator.GreaterOrEqual => ">=",
                CompareOperator.LessOrEqual => "<=",
                _ => "?"
            };

            return ParameterType switch
            {
                ParameterValueType.Bool => $"{ParameterName} = {BoolValue}",
                ParameterValueType.Float => $"{ParameterName} {op} {FloatValue}",
                ParameterValueType.Int => $"{ParameterName} {op} {IntValue}",
                ParameterValueType.Trigger => $"{ParameterName} Triggered",
                _ => $"{ParameterName} {op} ?"
            };
        }

        public override bool Evaluate(IEvaluationContext context)
        {
            if (string.IsNullOrEmpty(ParameterName))
                return false;

            switch (ParameterType)
            {
                case ParameterValueType.Bool:
                    return EvaluateBool(context);
                case ParameterValueType.Float:
                    return EvaluateFloat(context);
                case ParameterValueType.Int:
                    return EvaluateInt(context);
                case ParameterValueType.Trigger:
                    return context.GetTrigger(ParameterName);
                default:
                    return false;
            }
        }

        private bool EvaluateBool(IEvaluationContext context)
        {
            bool paramValue = context.GetBool(ParameterName);
            return paramValue == BoolValue;
        }

        private bool EvaluateFloat(IEvaluationContext context)
        {
            float paramValue = context.GetFloat(ParameterName);
            return Compare(paramValue, FloatValue);
        }

        private bool EvaluateInt(IEvaluationContext context)
        {
            int paramValue = context.GetInt(ParameterName);
            return Compare(paramValue, IntValue);
        }

        private bool Compare(float left, float right)
        {
            return Operator switch
            {
                CompareOperator.Equal => Mathf.Approximately(left, right),
                CompareOperator.NotEqual => !Mathf.Approximately(left, right),
                CompareOperator.GreaterThan => left > right,
                CompareOperator.LessThan => left < right,
                CompareOperator.GreaterOrEqual => left >= right,
                CompareOperator.LessOrEqual => left <= right,
                _ => false
            };
        }

        private bool Compare(int left, int right)
        {
            return Operator switch
            {
                CompareOperator.Equal => left == right,
                CompareOperator.NotEqual => left != right,
                CompareOperator.GreaterThan => left > right,
                CompareOperator.LessThan => left < right,
                CompareOperator.GreaterOrEqual => left >= right,
                CompareOperator.LessOrEqual => left <= right,
                _ => false
            };
        }

        public override TransitionCondition Clone()
        {
            return new ParameterCondition
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
                Operator = (CompareOperator)(int)op;

            if (config.TryGetValue("ParameterType", out var type))
                ParameterType = (ParameterValueType)(int)type;

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
