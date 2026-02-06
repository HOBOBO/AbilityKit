using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private sealed class SessionTickLoopSubFeature :
            ISessionSubFeature<BattleSessionFeature>,
            ISessionMainTickSubFeature<BattleSessionFeature>
        {
            public void OnAttach(in FeatureModuleContext<BattleSessionFeature> ctx) { }

            public void OnDetach(in FeatureModuleContext<BattleSessionFeature> ctx) { }

            public void MainTick(in FeatureModuleContext<BattleSessionFeature> ctx, float deltaTime)
            {
                var f = ctx.Feature;
                if (f == null || f._session == null) return;

                if (!f._tickEnteredLogged)
                {
                    f._tickEnteredLogged = true;
                }

                f._tickAcc += deltaTime;
                var fixedDelta = f.GetFixedDeltaSeconds();
                while (f._tickAcc >= fixedDelta)
                {
                    var nextFrame = f._lastFrame + 1;
                    f._replay?.Pump(f._session, nextFrame);
                    f._session.Tick(fixedDelta);
                    f._tickAcc -= fixedDelta;
                }

                f.TickRemoteDrivenLocalSim(deltaTime);
                f.TickConfirmedAuthorityWorldSim(deltaTime);
            }

            public void Tick(in FeatureModuleContext<BattleSessionFeature> ctx, float deltaTime) { }

            public void RebindAll(in FeatureModuleContext<BattleSessionFeature> ctx) { }
        }
    }
}
