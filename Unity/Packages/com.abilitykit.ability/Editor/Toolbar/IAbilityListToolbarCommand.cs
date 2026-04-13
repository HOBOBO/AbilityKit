namespace AbilityKit.Ability.Editor
{
    internal interface IAbilityListToolbarCommand
    {
        int Order { get; }
        string Label { get; }

        bool IsVisible(AbilityListWindow window, AbilityModuleSO selected);
        bool IsEnabled(AbilityListWindow window, AbilityModuleSO selected);

        void Execute(AbilityListWindow window, AbilityModuleSO selected);
    }
}
