namespace AbilityKit.Ability.Explain
{
    public sealed class ExplainAction
    {
        public string Name;
        public NavigationTarget NavigateTo;

        public static ExplainAction Navigate(string name, NavigationTarget target)
        {
            return new ExplainAction { Name = name, NavigateTo = target };
        }
    }
}
