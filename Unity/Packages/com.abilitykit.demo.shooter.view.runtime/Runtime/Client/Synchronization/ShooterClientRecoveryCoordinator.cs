#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Network.Runtime.Sync;

namespace AbilityKit.Demo.Shooter.View
{
    internal sealed class ShooterClientRecoveryCoordinator
    {
        private readonly ClientSyncRecoveryCoordinator<ShooterClientResyncReason> _recovery;

        public ShooterClientRecoveryCoordinator(ShooterClientDriftRecoveryPolicy policy, Func<int> getCurrentFrame)
        {
            _recovery = new ClientSyncRecoveryCoordinator<ShooterClientResyncReason>(
                policy.ReplayThreshold,
                getCurrentFrame,
                ShooterClientResyncReason.None);
        }

        public ShooterClientRecoveryState State => (ShooterClientRecoveryState)_recovery.State;

        public bool NeedsFullSnapshotResync => _recovery.NeedsFullSnapshotResync;

        public FastReconnectPhase FastReconnectPhase => _recovery.FastReconnectPhase;

        public IReadOnlyList<SyncHealthEvent> LastFastReconnectHealthEvents =>
            _recovery.LastFastReconnectHealthEvents;

        public ShooterClientResyncReason LastResyncReason => _recovery.LastRecoveryReason;

        public int LastResyncClientFrame => _recovery.LastRecoveryClientFrame;

        public int LastResyncAuthoritativeFrame => _recovery.LastRecoveryAuthoritativeFrame;

        public uint LastResyncClientStateHash => _recovery.LastRecoveryClientStateHash;

        public uint LastResyncAuthoritativeStateHash => _recovery.LastRecoveryAuthoritativeStateHash;

        public int CatchUpTargetFrame => _recovery.CatchUpTargetFrame;

        public void SetState(ShooterClientRecoveryState next)
        {
            _recovery.SetState((SyncRecoveryState)next);
        }

        public void EnterCatchUp(int authoritativeFrame)
        {
            _recovery.EnterCatchUp(authoritativeFrame);
        }

        public void MarkFullSnapshotResyncNeeded(
            ShooterClientResyncReason reason,
            int clientFrame,
            int authoritativeFrame,
            uint clientStateHash,
            uint authoritativeStateHash)
        {
            _recovery.MarkFullSnapshotResyncNeeded(
                reason,
                clientFrame,
                authoritativeFrame,
                clientStateHash,
                authoritativeStateHash);
        }

        public void ClearFullSnapshotResync()
        {
            _recovery.ClearFullSnapshotResync();
        }

        public void HeartbeatFastReconnect(int authoritativeFrame)
        {
            _recovery.HeartbeatFastReconnect(authoritativeFrame);
        }
    }
}
