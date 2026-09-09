using System;
using AbilityKit.Deterministic;

using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Nodes
{
    /// <summary>
    /// 写黑板动作：把常量或另一 key 的值写入目key（类型按 schema 校验），立即 Success
    /// </summary>
    public class SetBlackboardNode : ActionNodeBase
    {
        public const string KeyProperty = "key";
        public const string ValueKindProperty = "valueKind";     // 0=常量, 1=copyFrom
        public const string FromKeyProperty = "fromKey";
        public const string ConstBoolProperty = "constBool";
        public const string ConstInt64Property = "constInt64";
        public const string ConstFixed64Property = "constFixed64";
        public const string ConstStringProperty = "constString";

        private string _key = "";
        private bool _copyFrom;
        private string _fromKey = "";
        private AbilityKit.BehaviorTree.Definition.ValueType _type;
        private PropertyReader _properties;

        public override void OnInit(in NodeInitContext context)
        {
            _key = context.Properties.GetString(KeyProperty, "");
            _copyFrom = context.Properties.GetInt64(ValueKindProperty, 0) == 1;
            _fromKey = context.Properties.GetString(FromKeyProperty, "");
            _properties = context.Properties;

            if (string.IsNullOrEmpty(_key))
                throw new InvalidOperationException($"行为树节点 '{context.Definition.Id}'：设置黑板值时必须指定目标键。");
            if (!context.Context.Blackboard.Schema.TryGetType(_key, out _type))
                throw new InvalidOperationException($"行为树节点 '{context.Definition.Id}'：目标黑板键 '{_key}' 未声明。");
            if (_copyFrom)
            {
                if (string.IsNullOrEmpty(_fromKey)
                    || !context.Context.Blackboard.Schema.TryGetType(_fromKey, out var fromType)
                    || fromType != _type)
                    throw new InvalidOperationException(
                        $"行为树节点 '{context.Definition.Id}'：来源黑板键 '{_fromKey}' 不存在或类型不匹配。");
            }
        }

        public override NodeState OnTick(AbilityKit.BehaviorTree.Execution.ExecutionContext context)
        {
            var blackboard = context.Blackboard;
            if (_copyFrom)
            {
                switch (_type)
                {
                    case AbilityKit.BehaviorTree.Definition.ValueType.Bool: blackboard.SetBool(_key, blackboard.GetBool(_fromKey)); break;
                    case AbilityKit.BehaviorTree.Definition.ValueType.Int64: blackboard.SetInt64(_key, blackboard.GetInt64(_fromKey)); break;
                    case AbilityKit.BehaviorTree.Definition.ValueType.Fixed64: blackboard.SetFixed64(_key, blackboard.GetFixed64(_fromKey)); break;
                    case AbilityKit.BehaviorTree.Definition.ValueType.String: blackboard.SetString(_key, blackboard.GetString(_fromKey)); break;
                }
                return NodeState.Success;
            }

            switch (_type)
            {
                case AbilityKit.BehaviorTree.Definition.ValueType.Bool: blackboard.SetBool(_key, _properties.GetBool(ConstBoolProperty, false)); break;
                case AbilityKit.BehaviorTree.Definition.ValueType.Int64: blackboard.SetInt64(_key, _properties.GetInt64(ConstInt64Property, 0)); break;
                case AbilityKit.BehaviorTree.Definition.ValueType.Fixed64: blackboard.SetFixed64(_key, _properties.GetFixed64(ConstFixed64Property, Fixed64.Zero)); break;
                case AbilityKit.BehaviorTree.Definition.ValueType.String: blackboard.SetString(_key, _properties.GetString(ConstStringProperty, "")); break;
            }
            return NodeState.Success;
        }
    }
}
