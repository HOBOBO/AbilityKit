using AbilityKit.Ability.Share.ECS;
using AbilityKit.Game.Battle;

namespace AbilityKit.Game.Editor
{
    internal interface IBattleDebugPanel
    {
        string Name { get; }
        int Order { get; }

        bool IsVisible(in BattleDebugContext ctx);

        void Draw(in BattleDebugContext ctx);
    }
}
