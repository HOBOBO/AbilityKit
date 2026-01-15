using AbilityKit.Game.Battle;

namespace AbilityKit.Game.Editor
{
    internal interface IBattleDebugToolbarCommand
    {
        string Label { get; }
        int Order { get; }

        bool IsVisible(in BattleDebugContext ctx);
        bool IsEnabled(in BattleDebugContext ctx);

        void Execute(in BattleDebugContext ctx);
    }
}
