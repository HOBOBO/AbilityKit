namespace AbilityKit.BehaviorTree.Nodes
{
    using AbilityKit.BehaviorTree.Definition;
    using AbilityKit.BehaviorTree.Execution;

    public abstract class ParentNodeBase : NodeBase
    {
        protected int ChildCount { get; private set; }
        protected int RunningIndex { get; set; } = -1;

        public sealed override void OnInit(in NodeInitContext context)
        {
            ChildCount = context.ChildCount;
            OnInitParent(context);
        }

        protected virtual void OnInitParent(in NodeInitContext context) { }
        protected internal abstract bool CanExecute();
        protected internal abstract void OnChildExecuted(int childIndex, NodeState childState);
        protected internal virtual void OnChildStart() { }
        protected internal virtual void OnConditionalAbort(int childIndex) { }
        protected internal virtual bool CanRunParallel() => false;
        protected internal virtual NodeState OverrideState(NodeState state) => state;
        protected internal virtual int CurrentChildIndex => RunningIndex;
        protected internal virtual int CaptureRunningIndex() => RunningIndex;
        protected internal virtual void RestoreRunningIndex(int index) => RunningIndex = index;

    }
}
