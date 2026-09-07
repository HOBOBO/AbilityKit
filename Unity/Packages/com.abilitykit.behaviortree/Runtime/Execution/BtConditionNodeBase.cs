using AbilityKit.BehaviorTree.Definition;
namespace AbilityKit.BehaviorTree
{
    /// <summary>Legacy bridge for prefixed condition nodes.</summary>
    [System.Obsolete("Use AbilityKit.BehaviorTree.Nodes.ConditionNodeBase.", false)]
    public abstract class BtConditionNodeBase : AbilityKit.BehaviorTree.Nodes.ConditionNodeBase
    {
        public virtual void OnInit(in BtNodeInitContext context) { }
        public virtual void OnStart(BtExecutionContext context) { }
        public virtual void OnStop(BtExecutionContext context) { }

        protected abstract bool Validate(BtExecutionContext context);

        public sealed override void OnInit(in AbilityKit.BehaviorTree.Execution.NodeInitContext context)
        {
            var legacy = new BtNodeInitContext(in context);
            OnInit(in legacy);
        }

        public sealed override void OnStart(AbilityKit.BehaviorTree.Execution.ExecutionContext context)
        {
            var legacy = new BtExecutionContext(context);
            OnStart(legacy);
        }

        protected sealed override bool Validate(AbilityKit.BehaviorTree.Execution.ExecutionContext context)
        {
            var legacy = new BtExecutionContext(context);
            var result = Validate(legacy);
            return result;
        }

        public sealed override void OnStop(AbilityKit.BehaviorTree.Execution.ExecutionContext context)
        {
            var legacy = new BtExecutionContext(context);
            OnStop(legacy);
        }
    }
}
