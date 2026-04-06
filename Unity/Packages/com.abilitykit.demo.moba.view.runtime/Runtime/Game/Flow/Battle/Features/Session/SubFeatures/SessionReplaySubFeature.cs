using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Common.Record.Lockstep;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Game.Flow.Battle.Replay;
using AbilityKit.Game.Flow.Modules;
using AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private sealed class SessionReplaySubFeature :
            ISessionSubFeature<BattleSessionFeature>,
            ISessionPreTickSubFeature<BattleSessionFeature>,
            ISessionReplaySetupSubFeature<BattleSessionFeature>,
            ISessionFrameReceivedSubFeature<BattleSessionFeature>,
            IGameModuleId,
            IGameModuleDependencies
        {
            public string Id => "session_replay";

            public System.Collections.Generic.IEnumerable<string> Dependencies => new[] { "session_events" };

            public void OnAttach(in FeatureModuleContext<BattleSessionFeature> ctx) { }

            public void OnDetach(in FeatureModuleContext<BattleSessionFeature> ctx) { }

            public void PreTick(in FeatureModuleContext<BattleSessionFeature> ctx, float deltaTime)
            {
                var f = ctx.Feature;
                if (f == null) return;

                f._replayCtrl.PreTick(f._plan, f._state, f._handles, f._ctx, (ISessionReplayHost)f);
            }

            public void SetupReplayOrRecord(in FeatureModuleContext<BattleSessionFeature> ctx)
            {
                var f = ctx.Feature;
                if (f == null) return;

                IBattleReplayDriverProvider provider = null;
                if (ctx.Phase.Root.IsValid)
                {
                    ctx.Phase.Root.TryGetRef(out provider);
                }
                if (provider == null && ctx.Phase.Entry != null)
                {
                    ctx.Phase.Entry.TryGet(out provider);
                }
                f._replayCtrl.SetupReplayOrRecord(provider, f._plan, f._handles, f._ctx);
            }

            public void OnFrameReceived(in FeatureModuleContext<BattleSessionFeature> ctx, FramePacket packet)
            {
                var f = ctx.Feature;

                if (f == null) return;

                f._replayCtrl.OnFrameReceived(f._plan, f._state, f._handles, f._ctx, packet);
            }

            public void Tick(in FeatureModuleContext<BattleSessionFeature> ctx, float deltaTime) { }

            public void RebindAll(in FeatureModuleContext<BattleSessionFeature> ctx) { }

        }
    }
}
