namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private void EnsureSnapshotRoutingBuilt()
        {
            _runtime.SnapshotRouting.Build(
                _plan,
                _ctx,
                _session,
                _netAdapterContextHost,
                OnSessionFrameReceived);
        }

        private void DisposeSnapshotRoutingIfAny()
        {
            DisposeSnapshotRouting();
        }

        private void DisposeSnapshotRouting()
        {
            _runtime.SnapshotRouting.Dispose();
        }

        private void OnSessionFrameReceived(AbilityKit.Ability.Host.FramePacket packet)
        {
            _runtime.SnapshotRouting.Feed(packet);
        }
    }
}
