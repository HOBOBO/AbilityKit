using AbilityKit.BehaviorTree.Definition;
namespace AbilityKit.BehaviorTree
{
    /// <summary>Legacy bridge for prefixed decorator nodes.</summary>
    [System.Obsolete("Use AbilityKit.BehaviorTree.Nodes.DecoratorNode.", false)]
    public abstract class BtDecoratorNode : AbilityKit.BehaviorTree.Nodes.DecoratorNode
    {
        public virtual void OnStart(BtExecutionContext context) { }
        public virtual BtNodeState OnTick(BtExecutionContext context) => BtNodeState.Success;
        public virtual void OnStop(BtExecutionContext context) { }
        public virtual BtNodeState Decorate(BtNodeState state) => state;

        protected virtual void OnInitParent(in BtNodeInitContext context) { }
        protected internal abstract override bool CanExecute();
        protected internal abstract void OnChildExecuted(int childIndex, BtNodeState childState);
        protected internal virtual BtNodeState OverrideState(BtNodeState state) => state;
        protected internal virtual bool TryTickOverride(BtExecutionContext context, out BtNodeState state)
        {
            state = default;
            return false;
        }

        public sealed override void OnStart(AbilityKit.BehaviorTree.Execution.ExecutionContext context)
        {
            var legacy = new BtExecutionContext(context);
            OnStart(legacy);
        }

        public sealed override AbilityKit.BehaviorTree.Definition.NodeState OnTick(AbilityKit.BehaviorTree.Execution.ExecutionContext context)
        {
            var legacy = new BtExecutionContext(context);
            var state = OnTick(legacy).ToApi();
            return state;
        }

        public sealed override void OnStop(AbilityKit.BehaviorTree.Execution.ExecutionContext context)
        {
            var legacy = new BtExecutionContext(context);
            OnStop(legacy);
        }

        public sealed override AbilityKit.BehaviorTree.Definition.NodeState Decorate(AbilityKit.BehaviorTree.Definition.NodeState state)
            => Decorate(state.ToLegacy()).ToApi();

        protected sealed override void OnInitParent(in AbilityKit.BehaviorTree.Execution.NodeInitContext context)
        {
            var legacy = new BtNodeInitContext(in context);
            OnInitParent(in legacy);
        }

        protected internal sealed override void OnChildExecuted(int childIndex, AbilityKit.BehaviorTree.Definition.NodeState childState)
            => OnChildExecuted(childIndex, childState.ToLegacy());

        protected internal sealed override AbilityKit.BehaviorTree.Definition.NodeState OverrideState(AbilityKit.BehaviorTree.Definition.NodeState state)
            => OverrideState(state.ToLegacy()).ToApi();

        protected internal sealed override bool TryTickOverride(
            AbilityKit.BehaviorTree.Execution.ExecutionContext context,
            out AbilityKit.BehaviorTree.Definition.NodeState state)
        {
            var legacy = new BtExecutionContext(context);
            if (TryTickOverride(legacy, out var legacyState))
            {
                state = legacyState.ToApi();
                return true;
            }

            state = default;
            return false;
        }
    }
}
