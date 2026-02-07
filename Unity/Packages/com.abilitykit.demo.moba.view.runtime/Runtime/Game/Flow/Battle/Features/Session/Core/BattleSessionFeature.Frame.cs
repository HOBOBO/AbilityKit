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
                Events?.Publish(new FirstFrameReceivedEvent());
            }

            if (_ctx != null)
            {
                _ctx.LastFrame = _lastFrame;
            }

            if (_subFeatureHost != null)
            {
                var fctx = new FeatureModuleContext<BattleSessionFeature>(_phaseCtx, this);
                _subFeatureHost.ForEach<ISessionFrameReceivedSubFeature<BattleSessionFeature>>(m => m.OnFrameReceived(fctx, packet));
            }
        }
    }
}
