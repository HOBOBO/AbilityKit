using System;

namespace AbilityKit.Game.Flow
{
    public sealed class BattlePhase : IGamePhase
    {
        private readonly IBattleBootstrapper _bootstrapper;

        public BattlePhase(IBattleBootstrapper bootstrapper)
        {
            _bootstrapper = bootstrapper;
        }

        public void Enter(in GamePhaseContext ctx)
        {
            var flow = ctx.Entry.Get<GameFlowDomain>();

            var cfg = (_bootstrapper as IBattleStartConfigProvider)?.Config;
            var set = cfg != null ? cfg.EffectiveFeatureSet : null;
            if (set == null || set.FeatureIds == null || set.FeatureIds.Count == 0)
            {
                flow.Attach(new BattleContextFeature());
                flow.Attach(new BattleSessionFeature(_bootstrapper));
                flow.Attach(new BattleEntityFeature());
                flow.Attach(new BattleSyncFeature());
                flow.Attach(new BattleInputFeature());
                flow.Attach(new BattleViewFeature());
                flow.Attach(new BattleHudFeature());
                flow.Attach(new BattleDebugOnGUIFeature());
                return;
            }

            for (int i = 0; i < set.FeatureIds.Count; i++)
            {
                var id = set.FeatureIds[i];
                if (string.IsNullOrEmpty(id)) continue;

                switch (id)
                {
                    case "context":
                        flow.Attach(new BattleContextFeature());
                        break;
                    case "session":
                        flow.Attach(new BattleSessionFeature(_bootstrapper));
                        break;
                    case "entity":
                        flow.Attach(new BattleEntityFeature());
                        break;
                    case "sync":
                        flow.Attach(new BattleSyncFeature());
                        break;
                    case "input":
                        flow.Attach(new BattleInputFeature());
                        break;
                    case "view":
                        flow.Attach(new BattleViewFeature());
                        break;
                    case "hud":
                        flow.Attach(new BattleHudFeature());
                        break;
                    case "debug_ongui":
                        flow.Attach(new BattleDebugOnGUIFeature());
                        break;
                }
            }
        }

        public void Exit(in GamePhaseContext ctx)
        {
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
        }
    }
}
