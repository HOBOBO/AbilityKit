

namespace AbilityKit.HFSM.Extension
{

    public sealed class CompositeActionStateSnapshot
    {
        public CompositeActionStateSnapshot(
            bool exitRequested,
            bool completed,
            ActionBehaviourStatus lastStatus,
            ActionBehaviourSnapshot root)
        {
            ExitRequested = exitRequested;
            Completed = completed;
            LastStatus = lastStatus;
            Root = root ?? throw new System.ArgumentNullException(nameof(root));
        }

        public bool ExitRequested { get; }
        public bool Completed { get; }
        public ActionBehaviourStatus LastStatus { get; }
        public ActionBehaviourSnapshot Root { get; }
    }
}
