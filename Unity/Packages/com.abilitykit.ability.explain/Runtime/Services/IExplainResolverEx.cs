namespace AbilityKit.Ability.Explain
{
    public interface IExplainResolverEx : IExplainResolver, IRegistryPriority
    {
        bool CanResolve(ExplainResolveRequest request);
        bool CanExpand(ExplainExpandRequest request);
    }
}
