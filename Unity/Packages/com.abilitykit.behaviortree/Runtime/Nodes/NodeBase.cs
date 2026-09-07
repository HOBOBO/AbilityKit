namespace AbilityKit.BehaviorTree.Nodes
{
    using AbilityKit.BehaviorTree.Definition;
    using AbilityKit.BehaviorTree.Execution;

    public abstract class NodeBase
    {
        public string NodeId { get; internal set; } = "";
        public NodeState State { get; protected internal set; } = NodeState.Inactive;

        public virtual void OnInit(in NodeInitContext context) { }
        public virtual void OnStart(ExecutionContext context) { }
        public virtual NodeState OnTick(ExecutionContext context) => NodeState.Success;
        public virtual void OnStop(ExecutionContext context) { }

    }
}
