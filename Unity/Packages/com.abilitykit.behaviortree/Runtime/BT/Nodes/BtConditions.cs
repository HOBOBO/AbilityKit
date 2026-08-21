using System;
using AbilityKit.Deterministic;

namespace AbilityKit.BehaviorTree
{
    /// <summary>
    /// 黑板比较条件：leftKey 与常量或另一 key 比较。运算符：0=等于 1=不等于 2=小于
    /// 3=小于等于 4=大于 5=大于等于。Bool/String 仅支持等于/不等于。
    /// </summary>
    public sealed class BtBlackboardCompareNode : BtConditionNodeBase
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
        private BtValueType _type;
        private bool _rightBool;
        private long _rightInt64;
        private long _rightFixed64Raw;
        private string _rightString = "";

        public override void OnInit(in BtNodeInitContext context)
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
                // 常量按左 key 的 schema 类型读取对应字段
                switch (_type)
                {
                    case BtValueType.Bool:
                        _rightBool = context.Properties.GetBool(RightBoolProperty, false);
                        break;
                    case BtValueType.Int64:
                        _rightInt64 = context.Properties.GetInt64(RightInt64Property, 0);
                        break;
                    case BtValueType.Fixed64:
                        _rightFixed64Raw = context.Properties.GetFixed64(RightFixed64RawProperty, Fixed64.Zero).RawValue;
                        break;
                    case BtValueType.String:
                        _rightString = context.Properties.GetString(RightStringProperty, "");
                        break;
                }
            }
            if (_type is BtValueType.Bool or BtValueType.String && _op is 2 or 3 or 4 or 5)
                throw new InvalidOperationException(
                    $"BT node '{context.Definition.Id}': bool/string compare only supports eq/ne.");
        }

        private static BtBlackboard _contextBlackboard(in BtNodeInitContext context) => context.Context.Blackboard;

        protected override bool Validate(BtExecutionContext context)
        {
            var blackboard = context.Blackboard;
            return _type switch
            {
                BtValueType.Bool => Compare(blackboard.GetBool(_leftKey), ReadRightBool(blackboard)),
                BtValueType.Int64 => Compare(blackboard.GetInt64(_leftKey), ReadRightInt64(blackboard)),
                BtValueType.Fixed64 => Compare(blackboard.GetFixed64(_leftKey).RawValue, ReadRightFixedRaw(blackboard)),
                BtValueType.String => CompareOrdinal(blackboard.GetString(_leftKey), ReadRightString(blackboard)),
                _ => false,
            };
        }

        private bool ReadRightBool(BtBlackboard blackboard) => _rightIsKey ? blackboard.GetBool(_rightKey) : _rightBool;
        private long ReadRightInt64(BtBlackboard blackboard) => _rightIsKey ? blackboard.GetInt64(_rightKey) : _rightInt64;
        private long ReadRightFixedRaw(BtBlackboard blackboard)
            => _rightIsKey ? blackboard.GetFixed64(_rightKey).RawValue : _rightFixed64Raw;
        private string ReadRightString(BtBlackboard blackboard) => _rightIsKey ? blackboard.GetString(_rightKey) : _rightString;

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

    /// <summary>
    /// 概率条件：节点专属确定性随机流取 [0,100) 整数，小于 percent 视为通过。
    /// 随机流状态纳入树快照。
    /// </summary>
    public sealed class BtProbabilityNode : BtConditionNodeBase
    {
        public const string PercentProperty = "percent";

        private DeterministicRandom _random = null!;
        private long _percent = 50;

        public override void OnInit(in BtNodeInitContext context)
        {
            _random = context.Random;
            _percent = context.Properties.GetInt64(PercentProperty, 50);
            if (_percent is < 0 or > 100)
                throw new InvalidOperationException($"BT node '{context.Definition.Id}': percent must be within [0,100].");
        }

        protected override bool Validate(BtExecutionContext context)
            => _random.NextInt32(0, 100) < (int)_percent;
    }

    /// <summary>黑板 key 存在性条件（用于检测可选 key 是否已写入）。</summary>
    public sealed class BtBlackboardHasKeyNode : BtConditionNodeBase
    {
        public const string KeyProperty = "key";

        private string _key = "";

        public override void OnInit(in BtNodeInitContext context)
        {
            _key = context.Properties.GetString(KeyProperty, "");
            if (string.IsNullOrEmpty(_key))
                throw new InvalidOperationException($"BT node '{context.Definition.Id}': hasKey requires key.");
        }

        protected override bool Validate(BtExecutionContext context)
            => context.Blackboard.Schema.TryGetType(_key, out _);
    }
}
