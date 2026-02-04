using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Triggering.Blackboard
{
    public interface IBlackboardDomainResolver
    {
        bool TryResolveBoardId<TCtx>(in ExecCtx<TCtx> ctx, string domainId, out int boardId);
    }
}
