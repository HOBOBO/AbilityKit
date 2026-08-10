using AbilityKit.Ability.Share.ECS;
using AbilityKit.Game.Battle;

namespace AbilityKit.Game.Editor
{
    internal enum BattleDebugWorkspace
    {
        Actor = 0,
        Diagnostics = 1
    }

    internal interface IBattleDebugPanel
    {
        string Name { get; }
        int Order { get; }

        bool IsVisible(in BattleDebugContext ctx);

        void Draw(in BattleDebugContext ctx);
    }

    internal interface IBattleDebugPanelLayout
    {
        BattleDebugWorkspace Workspace { get; }
        bool OwnsScrollView { get; }
    }

    internal interface IBattleDebugTraceTarget
    {
        void OpenTrace(long rootContextId, long contextId);
    }

    internal interface IBattleDebugEventsTarget
    {
        void OpenForActor(long actorId);
        void OpenRecentFailures();
    }
}
