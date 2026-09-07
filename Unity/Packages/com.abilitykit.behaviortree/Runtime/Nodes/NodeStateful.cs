namespace AbilityKit.BehaviorTree.Nodes
{
    public interface NodeStateful
    {
        string CaptureState();
        void RestoreState(string payload);
    }
}
