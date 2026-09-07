using System;
using AbilityKit.Deterministic;

using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Nodes
{
    /// <summary>
    /// 比较黑板值与常量或另一黑板值，比较类型由左key schema 决定    /// </summary>
    public class BlackboardCompareNode : ConditionNodeBase
    {
        public const string LeftKeyProperty = "leftKey";
        public const string OpProperty = "op";
        public const string RightKindProperty = "rightKind";   // 0=常量, 1=key
        public const string RightKeyProperty = "rightKey";
        public const string RightBoolProperty = "rightBool";
        public const string RightInt64Property = "rightInt64";
        public const string RightFixed64RawProperty = "rightFixed64Raw";
        public const string RightStringProperty = "rightString";

        private string _leftKey = "";
        private int _op;
        private bool _rightIsKey;
        private string _rightKey = "";
        private AbilityKit.BehaviorTree.Definition.ValueType _type;
        private bool _rightBool;
        private long _rightInt64;
        private long _rightFixed64Raw;
        private string _rightString = "";

        public override void OnInit(in NodeInitContext context)
        {
            _leftKey = context.Properties.GetString(LeftKeyProperty, "");
            _op = context.Properties.GetInt32(OpProperty, 0);
            _rightIsKey = context.Properties.GetInt64(RightKindProperty, 0) == 1;
            _rightKey = context.Properties.GetString(RightKeyProperty, "");

            if (string.IsNullOrEmpty(_leftKey))
                throw new InvalidOperationException($"BT node '{context.Definition.Id}': blackboard compare requires leftKey.");
            if (_op is < 0 or > 5)
                throw new InvalidOperationException($"BT node '{context.Definition.Id}': invalid compare op {_op}.");
            if (_rightIsKey && string.IsNullOrEmpty(_rightKey))
                throw new InvalidOperationException($"BT node '{context.Definition.Id}': rightKind=key requires rightKey.");
            if (!_contextBlackboard(context).Schema.TryGetType(_leftKey, out _type))
                throw new InvalidOperationException($"BT node '{context.Definition.Id}': left key '{_leftKey}' not declared.");
            if (_rightIsKey)
            {
                if (!_contextBlackboard(context).Schema.TryGetType(_rightKey, out var rightType) || rightType != _type)
                    throw new InvalidOperationException(
                        $"BT node '{context.Definition.Id}': right key '{_rightKey}' missing or type mismatch.");
            }
            else
            {
                // 常量按左 key schema 类型读取对应字段
                switch (_type)
                {
                    case AbilityKit.BehaviorTree.Definition.ValueType.Bool:
                        _rightBool = context.Properties.GetBool(RightBoolProperty, false);
                        break;
                    case AbilityKit.BehaviorTree.Definition.ValueType.Int64:
                        _rightInt64 = context.Properties.GetInt64(RightInt64Property, 0);
                        break;
                    case AbilityKit.BehaviorTree.Definition.ValueType.Fixed64:
                        _rightFixed64Raw = context.Properties.GetFixed64(RightFixed64RawProperty, Fixed64.Zero).RawValue;
                        break;
                    case AbilityKit.BehaviorTree.Definition.ValueType.String:
                        _rightString = context.Properties.GetString(RightStringProperty, "");
                        break;
                }
            }
            if (_type is AbilityKit.BehaviorTree.Definition.ValueType.Bool or AbilityKit.BehaviorTree.Definition.ValueType.String && _op is 2 or 3 or 4 or 5)
                throw new InvalidOperationException(
                    $"BT node '{context.Definition.Id}': bool/string compare only supports eq/ne.");
        }

        private static AbilityKit.BehaviorTree.Blackboard.Blackboard _contextBlackboard(in NodeInitContext context) => context.Context.Blackboard;

        protected override bool Validate(AbilityKit.BehaviorTree.Execution.ExecutionContext context)
        {
            var blackboard = context.Blackboard;
            return _type switch
            {
                AbilityKit.BehaviorTree.Definition.ValueType.Bool => Compare(blackboard.GetBool(_leftKey), ReadRightBool(blackboard)),
                AbilityKit.BehaviorTree.Definition.ValueType.Int64 => Compare(blackboard.GetInt64(_leftKey), ReadRightInt64(blackboard)),
                AbilityKit.BehaviorTree.Definition.ValueType.Fixed64 => Compare(blackboard.GetFixed64(_leftKey).RawValue, ReadRightFixedRaw(blackboard)),
                AbilityKit.BehaviorTree.Definition.ValueType.String => CompareOrdinal(blackboard.GetString(_leftKey), ReadRightString(blackboard)),
                _ => false,
            };
        }

        private bool ReadRightBool(AbilityKit.BehaviorTree.Blackboard.Blackboard blackboard) => _rightIsKey ? blackboard.GetBool(_rightKey) : _rightBool;
        private long ReadRightInt64(AbilityKit.BehaviorTree.Blackboard.Blackboard blackboard) => _rightIsKey ? blackboard.GetInt64(_rightKey) : _rightInt64;
        private long ReadRightFixedRaw(AbilityKit.BehaviorTree.Blackboard.Blackboard blackboard)
            => _rightIsKey ? blackboard.GetFixed64(_rightKey).RawValue : _rightFixed64Raw;
        private string ReadRightString(AbilityKit.BehaviorTree.Blackboard.Blackboard blackboard) => _rightIsKey ? blackboard.GetString(_rightKey) : _rightString;

        private bool Compare(long left, long right) => _op switch
        {
            0 => left == right,
            1 => left != right,
            2 => left < right,
            3 => left <= right,
            4 => left > right,
            _ => left >= right,
        };

        private bool Compare(bool left, bool right) => _op == 0 ? left == right : left != right;

        private bool CompareOrdinal(string left, string right) => _op == 0
            ? string.Equals(left, right, StringComparison.Ordinal)
            : !string.Equals(left, right, StringComparison.Ordinal);
    }
}
