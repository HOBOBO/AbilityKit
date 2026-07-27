namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private void EnsureSnapshotRoutingBuilt()
        {
            _snapshotRouting.Build(
                _plan,
                _handles,
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
            _snapshotRouting.Dispose(_handles, _ctx, _session, OnSessionFrameReceived);
        }

        private void OnSessionFrameReceived(AbilityKit.Ability.Host.FramePacket packet)
        {
            _snapshotRouting.Feed(_handles, packet);
        }
    }
}
