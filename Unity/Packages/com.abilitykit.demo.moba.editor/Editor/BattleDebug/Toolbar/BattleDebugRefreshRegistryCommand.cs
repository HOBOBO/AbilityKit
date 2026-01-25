namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugRefreshRegistryCommand : IBattleDebugToolbarCommand
    {
        public string Label => "Reload UI";
        public int Order => 100;

        public bool IsVisible(in BattleDebugContext ctx) => true;

        public bool IsEnabled(in BattleDebugContext ctx) => true;

        public void Execute(in BattleDebugContext ctx)
        {
            BattleDebugPanelRegistry.Refresh();
            BattleDebugToolbarCommandRegistry.Refresh();
            ctx.RequestRepaint?.Invoke();
        }
    }
}
