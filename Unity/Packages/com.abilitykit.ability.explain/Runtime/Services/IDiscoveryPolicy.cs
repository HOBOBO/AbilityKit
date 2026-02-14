namespace AbilityKit.Ability.Explain
{
    public interface IDiscoveryPolicy : IRegistryPriority
    {
        bool IsDiscoverable(in PipelineItemKey key);
    }
}
