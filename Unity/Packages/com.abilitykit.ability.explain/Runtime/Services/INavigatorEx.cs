namespace AbilityKit.Ability.Explain
{
    public interface INavigatorEx : INavigator, IRegistryPriority
    {
        bool CanNavigateExt(NavigationTarget target);
    }
}
