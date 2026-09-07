using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Nodes
{
    /// <summary>恒成功动�?/summary>
    public class SucceedNode : ActionNodeBase
    {
        public override NodeState OnTick(AbilityKit.BehaviorTree.Execution.ExecutionContext context) => NodeState.Success;
    }
}
