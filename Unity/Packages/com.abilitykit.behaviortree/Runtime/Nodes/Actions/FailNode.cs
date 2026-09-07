using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Nodes
{
    /// <summary>恒失败动�?/summary>
    public class FailNode : ActionNodeBase
    {
        public override NodeState OnTick(AbilityKit.BehaviorTree.Execution.ExecutionContext context) => NodeState.Failure;
    }
}
