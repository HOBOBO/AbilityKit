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
        ISessionSubFeature<ISessionRuntimeFeature>,
        ISessionReplaySetupSubFeature<ISessionRuntimeFeature>,
        ISessionFrameReceivedSubFeature<ISessionRuntimeFeature>,
        IGameModuleId,
        IGameModuleDependencies
    {
        public string Id => "session_replay";

        public System.Collections.Generic.IEnumerable<string> Dependencies => new[] { "session_events" };

        public void OnAttach(in FeatureModuleContext<ISessionRuntimeFeature> ctx) =>
            BattleSessionFeatureRuntimeAccess.Require<ISessionReplayRuntime>(ctx, Id);

        public void OnDetach(in FeatureModuleContext<ISessionRuntimeFeature> ctx) { }

        public void SetupReplayOrRecord(in FeatureModuleContext<ISessionRuntimeFeature> ctx)
        {
            var runtime = BattleSessionFeatureRuntimeAccess.Require<ISessionReplayRuntime>(ctx, Id);

            runtime.SetupReplayOrRecord();
        }

        public void OnFrameReceived(in FeatureModuleContext<ISessionRuntimeFeature> ctx, FramePacket packet)
        {
            var runtime = BattleSessionFeatureRuntimeAccess.Require<ISessionReplayRuntime>(ctx, Id);

            runtime.OnReplayFrameReceived(packet);
        }

        public void Tick(in FeatureModuleContext<ISessionRuntimeFeature> ctx, float deltaTime) { }

        public void RebindAll(in FeatureModuleContext<ISessionRuntimeFeature> ctx) { }
    }
}
