#nullable enable

using AbilityKit.Network.Runtime.Sync;

namespace AbilityKit.Demo.Shooter.View
{
    public sealed class ShooterTimeAnchorCoordinator
    {
        private readonly ClockSynchronizationCoordinator _coordinator;

        public ShooterTimeAnchorCoordinator(int tickRate)
        {
            _coordinator = new ClockSynchronizationCoordinator(tickRate);
        }

        public SyncTimeAnchor LastLocalAnchor => _coordinator.LastLocalAnchor;

        public SyncTimeAnchor AdvanceLocal()
        {
            return _coordinator.AdvanceLocal();
        }

        public void Reset(int tickRate)
        {
            _coordinator.Reset(tickRate);
        }

        public static ShooterTimeAnchorCoordinator CreateLocal(int tickRate)
        {
            return new ShooterTimeAnchorCoordinator(tickRate);
        }

        public static ShooterRemoteTimeAnchorProjection ProjectRemote(
            in ShooterGatewayWorldStartAnchor worldStartAnchor,
            long serverNowTicks)
        {
            var projection = ClockSynchronizationCoordinator.ProjectAuthoritative(
                worldStartAnchor.StartServerTicks,
                worldStartAnchor.ServerTickFrequency,
                worldStartAnchor.StartFrame,
                worldStartAnchor.FixedDeltaSeconds,
                serverNowTicks);
            if (!projection.AnchorValid)
            {
                return default;
            }

            return new ShooterRemoteTimeAnchorProjection(
                projection.AnchorValid,
                projection.ServerNowTicks,
                projection.TargetFrame,
                projection.CatchUpFrames,
                projection.ElapsedSeconds,
                projection.TimeAnchor);
        }
    }

    public readonly struct ShooterRemoteTimeAnchorProjection
    {
        public ShooterRemoteTimeAnchorProjection(
            bool anchorValid,
            long serverNowTicks,
            int targetFrame,
            int catchUpFrames,
            double elapsedSeconds,
            SyncTimeAnchor timeAnchor)
        {
            AnchorValid = anchorValid;
            ServerNowTicks = serverNowTicks;
            TargetFrame = targetFrame;
            CatchUpFrames = catchUpFrames;
            ElapsedSeconds = elapsedSeconds;
            TimeAnchor = timeAnchor;
        }

        public bool AnchorValid { get; }
        public long ServerNowTicks { get; }
        public int TargetFrame { get; }
        public int CatchUpFrames { get; }
        public double ElapsedSeconds { get; }
        public SyncTimeAnchor TimeAnchor { get; }
    }
}
