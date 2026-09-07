namespace AbilityKit.BehaviorTree.Nodes
{
    using AbilityKit.BehaviorTree.Definition;
    using AbilityKit.BehaviorTree.Execution;

    public abstract class DecoratorNode : ParentNodeBase
    {
        protected internal sealed override int CurrentChildIndex => 0;

        public virtual NodeState Decorate(NodeState state) => state;

        protected internal virtual bool TryTickOverride(ExecutionContext context, out NodeState state)
        {
            state = default;
            return false;
        }

    }
}
