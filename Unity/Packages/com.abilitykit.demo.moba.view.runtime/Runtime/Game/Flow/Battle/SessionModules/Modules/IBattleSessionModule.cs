using AbilityKit.Game.Battle;
using System.Collections.Generic;

namespace AbilityKit.Game.Flow.Battle.Modules
{
    public interface IBattleSessionModule
    {
        void OnAttach(in BattleSessionModuleContext ctx);
        void OnDetach(in BattleSessionModuleContext ctx);
        void Tick(in BattleSessionModuleContext ctx, float deltaTime);
    }

    public interface IBattleSessionModuleId
    {
        string Id { get; }
    }

    public interface IBattleSessionModuleDependencies
    {
        IEnumerable<string> Dependencies { get; }
    }
}
