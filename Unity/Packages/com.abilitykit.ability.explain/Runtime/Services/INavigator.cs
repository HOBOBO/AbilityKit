namespace AbilityKit.Ability.Explain
{
    public interface INavigator
    {
        bool CanNavigate(NavigationTarget target);
        void Navigate(NavigationTarget target);
    }
}
