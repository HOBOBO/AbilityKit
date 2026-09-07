using System;
using AbilityKit.Deterministic;

using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Nodes
{
    /// <summary>
    /// 等待节点：mode=0 按定点秒数（宿主注入tick 时间），mode=1 按帧�?   /// 剩余进度纳入树快�?   /// </summary>
    public class WaitNode : ActionNodeBase, NodeStateful
    {
        public const string ModeProperty = "mode";               // 0=时间, 1=帧数
        public const string DurationSecondsProperty = "durationSeconds";
        public const string DurationFramesProperty = "durationFrames";

        private bool _byFrames;
        private Fixed64 _duration = Fixed64.One;
        private long _frames = 30;
        private Fixed64 _deadline;
        private long _endFrame;

        public override void OnInit(in NodeInitContext context)
        {
            _byFrames = context.Properties.GetInt64(ModeProperty, 0) == 1;
            _duration = context.Properties.GetFixed64(DurationSecondsProperty, Fixed64.One);
            _frames = context.Properties.GetInt64(DurationFramesProperty, 30);
            if (!_byFrames && _duration <= Fixed64.Zero)
                throw new InvalidOperationException($"BT node '{context.Definition.Id}': wait duration must be > 0.");
            if (_byFrames && _frames <= 0)
                throw new InvalidOperationException($"BT node '{context.Definition.Id}': wait frames must be > 0.");
        }

        public override void OnStart(AbilityKit.BehaviorTree.Execution.ExecutionContext context)
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

        public override NodeState OnTick(AbilityKit.BehaviorTree.Execution.ExecutionContext context)
        {
            if (_byFrames) return context.Frame >= _endFrame ? NodeState.Success : NodeState.Running;
            return context.Time >= _deadline ? NodeState.Success : NodeState.Running;
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
}
