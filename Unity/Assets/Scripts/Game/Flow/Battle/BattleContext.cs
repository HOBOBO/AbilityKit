using AbilityKit.Ability.Server;
using AbilityKit.Game.Battle;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleContext
    {
        public BattleLogicSession Session;
        public BattleStartPlan Plan;
        public int LastFrame;
    }

    public sealed class BattleContextFeature : IGamePhaseFeature
    {
        public void OnAttach(in GamePhaseContext ctx)
        {
            if (ctx.Root.TryGetComponent(out BattleContext existing) && existing != null) return;
            ctx.Root.AddComponent(new BattleContext());
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            if (ctx.Root.IsValid)
            {
                ctx.Root.RemoveComponent(typeof(BattleContext));
            }
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
        }
    }
}
