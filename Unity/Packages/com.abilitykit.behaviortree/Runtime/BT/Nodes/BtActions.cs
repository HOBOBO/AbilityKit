using System;
using AbilityKit.Core.Logging;
using AbilityKit.Deterministic;

namespace AbilityKit.BehaviorTree
{
    /// <summary>
    /// 等待节点：mode=0 按定点秒数（宿主注入的 tick 时间），mode=1 按帧数。
    /// 剩余进度纳入树快照。
    /// </summary>
    public sealed class BtWaitNode : BtActionNodeBase, IBtNodeStateful
    {
        public const string ModeProperty = "mode";               // 0=时间, 1=帧数
        public const string DurationSecondsProperty = "durationSeconds";
        public const string DurationFramesProperty = "durationFrames";

        private bool _byFrames;
        private Fixed64 _duration = Fixed64.One;
        private long _frames = 30;
        private Fixed64 _deadline;
        private long _endFrame;

        public override void OnInit(in BtNodeInitContext context)
        {
            _byFrames = context.Properties.GetInt64(ModeProperty, 0) == 1;
            _duration = context.Properties.GetFixed64(DurationSecondsProperty, Fixed64.One);
            _frames = context.Properties.GetInt64(DurationFramesProperty, 30);
            if (!_byFrames && _duration <= Fixed64.Zero)
                throw new InvalidOperationException($"BT node '{context.Definition.Id}': wait duration must be > 0.");
            if (_byFrames && _frames <= 0)
                throw new InvalidOperationException($"BT node '{context.Definition.Id}': wait frames must be > 0.");
        }

        public override void OnStart(BtExecutionContext context)
        {
            if (_byFrames)
            {
                _endFrame = context.Frame + _frames;
            }
            else
            {
                _deadline = context.Time + _duration;
            }
        }

        public override BtNodeState OnTick(BtExecutionContext context)
        {
            if (_byFrames) return context.Frame >= _endFrame ? BtNodeState.Success : BtNodeState.Running;
            return context.Time >= _deadline ? BtNodeState.Success : BtNodeState.Running;
        }

        public string CaptureState() => _byFrames
            ? "f" + _endFrame.ToString()
            : "t" + _deadline.RawValue.ToString();

        public void RestoreState(string payload)
        {
            if (string.IsNullOrEmpty(payload)) return;
            if (payload[0] == 'f') _endFrame = long.Parse(payload.AsSpan(1));
            else if (payload[0] == 't') _deadline = Fixed64.FromRaw(long.Parse(payload.AsSpan(1)));
        }
    }

    /// <summary>
    /// 写黑板动作：把常量或另一 key 的值写入目标 key（类型按 schema 校验），立即 Success。
    /// </summary>
    public sealed class BtSetBlackboardNode : BtActionNodeBase
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
        private BtValueType _type;
        private BtPropertyReader _properties;

        public override void OnInit(in BtNodeInitContext context)
        {
            _key = context.Properties.GetString(KeyProperty, "");
            _copyFrom = context.Properties.GetInt64(ValueKindProperty, 0) == 1;
            _fromKey = context.Properties.GetString(FromKeyProperty, "");
            _properties = context.Properties;

            if (string.IsNullOrEmpty(_key))
                throw new InvalidOperationException($"BT node '{context.Definition.Id}': setBlackboard requires key.");
            if (!context.Context.Blackboard.Schema.TryGetType(_key, out _type))
                throw new InvalidOperationException($"BT node '{context.Definition.Id}': target key '{_key}' not declared.");
            if (_copyFrom)
            {
                if (string.IsNullOrEmpty(_fromKey)
                    || !context.Context.Blackboard.Schema.TryGetType(_fromKey, out var fromType)
                    || fromType != _type)
                    throw new InvalidOperationException(
                        $"BT node '{context.Definition.Id}': copy source key '{_fromKey}' missing or type mismatch.");
            }
        }

        public override BtNodeState OnTick(BtExecutionContext context)
        {
            var blackboard = context.Blackboard;
            if (_copyFrom)
            {
                switch (_type)
                {
                    case BtValueType.Bool: blackboard.SetBool(_key, blackboard.GetBool(_fromKey)); break;
                    case BtValueType.Int64: blackboard.SetInt64(_key, blackboard.GetInt64(_fromKey)); break;
                    case BtValueType.Fixed64: blackboard.SetFixed64(_key, blackboard.GetFixed64(_fromKey)); break;
                    case BtValueType.String: blackboard.SetString(_key, blackboard.GetString(_fromKey)); break;
                }
                return BtNodeState.Success;
            }

            switch (_type)
            {
                case BtValueType.Bool: blackboard.SetBool(_key, _properties.GetBool(ConstBoolProperty, false)); break;
                case BtValueType.Int64: blackboard.SetInt64(_key, _properties.GetInt64(ConstInt64Property, 0)); break;
                case BtValueType.Fixed64: blackboard.SetFixed64(_key, _properties.GetFixed64(ConstFixed64Property, Fixed64.Zero)); break;
                case BtValueType.String: blackboard.SetString(_key, _properties.GetString(ConstStringProperty, "")); break;
            }
            return BtNodeState.Success;
        }
    }

    /// <summary>日志动作：经 Core Log 输出（非 UnityEngine.Debug），立即 Success。</summary>
    public sealed class BtLogNode : BtActionNodeBase
    {
        public const string MessageProperty = "message";
        public const string LevelProperty = "level";             // 0=Trace 1=Info 2=Warning 3=Error

        private string _message = "";
        private int _level = 1;

        public override void OnInit(in BtNodeInitContext context)
        {
            _message = context.Properties.GetString(MessageProperty, "");
            _level = context.Properties.GetInt32(LevelProperty, 1);
        }

        public override BtNodeState OnTick(BtExecutionContext context)
        {
            var line = $"[BT:{NodeId}] {_message}";
            switch (_level)
            {
                case 0: Log.Trace(line); break;
                case 2: Log.Warning(line); break;
                case 3: Log.Error(line); break;
                default: Log.Info(line); break;
            }
            return BtNodeState.Success;
        }
    }

    /// <summary>恒成功动作。</summary>
    public sealed class BtSucceedNode : BtActionNodeBase
    {
        public override BtNodeState OnTick(BtExecutionContext context) => BtNodeState.Success;
    }

    /// <summary>恒失败动作。</summary>
    public sealed class BtFailNode : BtActionNodeBase
    {
        public override BtNodeState OnTick(BtExecutionContext context) => BtNodeState.Failure;
    }
}
