using AbilityKit.BehaviorTree.Definition;
namespace AbilityKit.BehaviorTree
{
    /// <summary>Legacy bridge for prefixed composite nodes.</summary>
    [System.Obsolete("Use AbilityKit.BehaviorTree.Nodes.CompositeNode.", false)]
    public abstract class BtCompositeNode : AbilityKit.BehaviorTree.Nodes.CompositeNode
    {
        public new BtAbortType AbortType
        {
            get => base.AbortType.ToLegacy();
            protected set => base.AbortType = value.ToApi();
        }

        public virtual void OnStart(BtExecutionContext context) { }
        public virtual BtNodeState OnTick(BtExecutionContext context) => BtNodeState.Success;
        public virtual void OnStop(BtExecutionContext context) { }

        protected virtual void OnCompositeInit(in BtNodeInitContext context) { }
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

        protected sealed override void OnCompositeInit(in AbilityKit.BehaviorTree.Execution.NodeInitContext context)
        {
            var legacy = new BtNodeInitContext(in context);
            OnCompositeInit(in legacy);
        }

        protected internal sealed override void OnChildExecuted(int childIndex, AbilityKit.BehaviorTree.Definition.NodeState childState)
            => OnChildExecuted(childIndex, childState.ToLegacy());

        protected internal sealed override AbilityKit.BehaviorTree.Definition.NodeState OverrideState(AbilityKit.BehaviorTree.Definition.NodeState state)
            => OverrideState(state.ToLegacy()).ToApi();
    }
}
