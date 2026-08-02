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

            if (packet.Frame.Value > _lastFrame)
            {
                _lastFrame = packet.Frame.Value;
            }
            NotifyFirstFrameReceivedOnce();

            SessionContextBinder.BindLastFrame(_ctx, _state);

            if (_subFeatureHost != null)
            {
                var fctx = new FeatureModuleContext<BattleSessionFeature>(_phaseCtx, this);
                _subFeatureHost.ForEach<ISessionFrameReceivedSubFeature<BattleSessionFeature>>(m => m.OnFrameReceived(fctx, packet));
            }
        }

        private void NotifyFirstFrameReceivedOnce()
        {
            if (_firstFrameReceived) return;

            _firstFrameReceived = true;
            _eventsCtrl.NotifyFirstFrameReceived(this);
        }

        internal static bool CompletesAssetBarrierOnFirstFrame(BattleStartConfig.BattleHostMode hostMode)
        {
            return false;
        }
    }
}
