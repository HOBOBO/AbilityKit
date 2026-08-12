using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Recording.FrameRecord;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    internal sealed class SessionReplaySubFeature :
        ISessionSubFeature<BattleSessionFeature>,
        ISessionReplaySetupSubFeature<BattleSessionFeature>,
        ISessionFrameReceivedSubFeature<BattleSessionFeature>,
        IGameModuleId,
        IGameModuleDependencies
    {
        public string Id => "session_replay";

        public System.Collections.Generic.IEnumerable<string> Dependencies => new[] { "session_events" };

        public void OnAttach(in FeatureModuleContext<BattleSessionFeature> ctx) { }

        public void OnDetach(in FeatureModuleContext<BattleSessionFeature> ctx) { }

        public void SetupReplayOrRecord(in FeatureModuleContext<BattleSessionFeature> ctx)
        {
            if (!BattleSessionFeatureRuntimeAccess.TryGet<ISessionReplayRuntime>(ctx, out var runtime)) return;

            runtime.Replay.SetupReplayOrRecord(
                runtime.Plan,
                runtime.Context,
                runtime.ReplayResources);
        }

        public void OnFrameReceived(in FeatureModuleContext<BattleSessionFeature> ctx, FramePacket packet)
        {
            if (!BattleSessionFeatureRuntimeAccess.TryGet<ISessionReplayRuntime>(ctx, out var runtime)) return;

            runtime.Replay.OnFrameReceived(runtime.Plan, runtime.State, runtime.Context, packet);
        }

        public void Tick(in FeatureModuleContext<BattleSessionFeature> ctx, float deltaTime) { }

        public void RebindAll(in FeatureModuleContext<BattleSessionFeature> ctx) { }
    }
}
