namespace AbilityKit.Ability.Triggering.Blackboard
{
    public interface IBlackboardResolver
    {
        bool TryResolve(string boardId, out IBlackboard blackboard);
    }
}
