namespace AbilityKit.Triggering.Blackboard
{
    public interface IBlackboardResolver
    {
        bool TryResolve(int boardId, out IBlackboard blackboard);
    }
}
