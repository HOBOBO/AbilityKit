namespace AbilityKit.BehaviorTree.Nodes
{
    using AbilityKit.BehaviorTree.Definition;
    using AbilityKit.BehaviorTree.Execution;

    public abstract class ConditionNodeBase : NodeBase
    {
        protected abstract bool Validate(ExecutionContext context);

        public sealed override NodeState OnTick(ExecutionContext context)
            => Validate(context) ? NodeState.Success : NodeState.Failure;
    }
}
