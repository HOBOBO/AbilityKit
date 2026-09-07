using AbilityKit.Core.Logging;

using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Nodes
{
    /// <summary>日志动作：经 Core Log 输出（非 UnityEngine.Debug），立即 Success</summary>
    public class LogNode : ActionNodeBase
    {
        public const string MessageProperty = "message";
        public const string LevelProperty = "level";             // 0=Trace 1=Info 2=Warning 3=Error

        private string _message = "";
        private int _level = 1;

        public override void OnInit(in NodeInitContext context)
        {
            _message = context.Properties.GetString(MessageProperty, "");
            _level = context.Properties.GetInt32(LevelProperty, 1);
        }

        public override NodeState OnTick(AbilityKit.BehaviorTree.Execution.ExecutionContext context)
        {
            var line = $"[BT:{NodeId}] {_message}";
            switch (_level)
            {
                case 0: Log.Trace(line); break;
                case 2: Log.Warning(line); break;
                case 3: Log.Error(line); break;
                default: Log.Info(line); break;
            }
            return NodeState.Success;
        }
    }
}
