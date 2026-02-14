namespace AbilityKit.Ability.Explain
{
    public interface IEntityProviderEx : IEntityProvider, IRegistryPriority
    {
        bool CanProvide(string searchText);
    }
}
