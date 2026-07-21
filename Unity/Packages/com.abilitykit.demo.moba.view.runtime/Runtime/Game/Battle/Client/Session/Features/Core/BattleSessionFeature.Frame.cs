using AbilityKit.Ability.Host;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private void OnFrame(FramePacket packet)
        {
            if (_subFeatureHost != null)
            {
                var fctx = new FeatureModuleContext<BattleSessionFeature>(_phaseCtx, this);
                _subFeatureHost.ForEach<ISessionFramePacketTransformSubFeature<BattleSessionFeature>>(m => packet = m.TransformFramePacket(fctx, packet));
            }

            _lastFrame = packet.Frame.Value;

            if (!_firstFrameReceived)
            {
                _firstFrameReceived = true;
                _eventsCtrl.NotifyFirstFrameReceived(this);

                // Local WorldInit and GatewayRemote room loading both complete their asset barriers
                // before the first frame is published. Bridge that completed barrier into the client HFSM.
                if (CompletesAssetBarrierOnFirstFrame(_plan.HostMode))
                {
                    NotifyAssetsLoadCompleted();
                }
            }

            SessionContextBinder.BindLastFrame(_ctx, _state);

            if (_subFeatureHost != null)
            {
                var fctx = new FeatureModuleContext<BattleSessionFeature>(_phaseCtx, this);
                _subFeatureHost.ForEach<ISessionFrameReceivedSubFeature<BattleSessionFeature>>(m => m.OnFrameReceived(fctx, packet));
            }
        }

        internal static bool CompletesAssetBarrierOnFirstFrame(BattleStartConfig.BattleHostMode hostMode)
        {
            return hostMode == BattleStartConfig.BattleHostMode.Local ||
                   hostMode == BattleStartConfig.BattleHostMode.GatewayRemote;
        }
    }
}
