namespace AbilityKit.Ability.Explain
{
    public interface IExplainResolver
    {
        bool TryResolve(ExplainResolveRequest request, out ExplainResolveResult result);
        bool TryExpandDiscoveredRoot(ExplainExpandRequest request, out ExplainTreeRoot root);
    }
}
