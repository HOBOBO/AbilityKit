using AbilityKit.BehaviorTree.Definition;
namespace AbilityKit.BehaviorTree
{
    /// <summary>Legacy bridge for prefixed action nodes.</summary>
    [System.Obsolete("Use AbilityKit.BehaviorTree.Nodes.ActionNodeBase.", false)]
    public abstract class BtActionNodeBase : AbilityKit.BehaviorTree.Nodes.ActionNodeBase
    {
        public virtual void OnInit(in BtNodeInitContext context) { }
        public virtual void OnStart(BtExecutionContext context) { }
        public virtual BtNodeState OnTick(BtExecutionContext context) => BtNodeState.Success;
        public virtual void OnStop(BtExecutionContext context) { }

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
    }
}
