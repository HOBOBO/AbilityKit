namespace AbilityKit.Ability.Editor
{
    internal sealed class AbilityListRefreshCommand : IAbilityListToolbarCommand
    {
        public int Order => 0;
        public string Label => "刷新";

        public bool IsVisible(AbilityListWindow window, AbilityModuleSO selected) => true;
        public bool IsEnabled(AbilityListWindow window, AbilityModuleSO selected) => true;

        public void Execute(AbilityListWindow window, AbilityModuleSO selected)
        {
            window?.RequestRebuild();
        }
    }

    internal sealed class AbilityListCreateCommand : IAbilityListToolbarCommand
    {
        public int Order => 10;
        public string Label => "创建";

        public bool IsVisible(AbilityListWindow window, AbilityModuleSO selected) => true;
        public bool IsEnabled(AbilityListWindow window, AbilityModuleSO selected) => true;

        public void Execute(AbilityListWindow window, AbilityModuleSO selected)
        {
            window?.ExecuteCreate();
        }
    }

    internal sealed class AbilityListExportSelectedCommand : IAbilityListToolbarCommand
    {
        public int Order => 20;
        public string Label => "导出选中";

        public bool IsVisible(AbilityListWindow window, AbilityModuleSO selected) => true;
        public bool IsEnabled(AbilityListWindow window, AbilityModuleSO selected) => selected != null;

        public void Execute(AbilityListWindow window, AbilityModuleSO selected)
        {
            window?.ExecuteExportSelected(selected);
        }
    }

    internal sealed class AbilityListExportAllCommand : IAbilityListToolbarCommand
    {
        public int Order => 30;
        public string Label => "导出全部";

        public bool IsVisible(AbilityListWindow window, AbilityModuleSO selected) => true;
        public bool IsEnabled(AbilityListWindow window, AbilityModuleSO selected) => true;

        public void Execute(AbilityListWindow window, AbilityModuleSO selected)
        {
            window?.ExecuteExportAll();
        }
    }
}
