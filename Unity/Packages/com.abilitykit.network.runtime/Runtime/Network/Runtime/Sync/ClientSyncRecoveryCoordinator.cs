#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Network.Runtime.Sync
{
    /// <summary>
    /// Coordinates gameplay-independent client synchronization recovery state, diagnostic evidence,
    /// and projection onto the framework fast-reconnect phase machine.
    /// </summary>
    public sealed class ClientSyncRecoveryCoordinator<TReason>
    {
        private readonly Func<int> _getCurrentFrame;
        private readonly TReason _noneReason;
        private readonly FastReconnectPhaseDriver _fastReconnect;
        private SyncHealthEvent[] _lastHealthEvents = Array.Empty<SyncHealthEvent>();
        private SyncRecoveryState _state = SyncRecoveryState.Normal;

        public ClientSyncRecoveryCoordinator(
            int resumeWindowFrames,
            Func<int> getCurrentFrame,
            TReason noneReason = default!)
        {
            _getCurrentFrame = getCurrentFrame ?? throw new ArgumentNullException(nameof(getCurrentFrame));
            _noneReason = noneReason;
            _fastReconnect = new FastReconnectPhaseDriver(resumeWindowFrames);
            LastRecoveryReason = noneReason;
        }

        public SyncRecoveryState State => _state;

        public bool NeedsFullSnapshotResync { get; private set; }

        public FastReconnectPhase FastReconnectPhase => _fastReconnect.Phase;

        public IReadOnlyList<SyncHealthEvent> LastFastReconnectHealthEvents => _lastHealthEvents;

        public TReason LastRecoveryReason { get; private set; }

        public int LastRecoveryClientFrame { get; private set; }

        public int LastRecoveryAuthoritativeFrame { get; private set; }

        public uint LastRecoveryClientStateHash { get; private set; }

        public uint LastRecoveryAuthoritativeStateHash { get; private set; }

        public int CatchUpTargetFrame { get; private set; }

        public void SetState(SyncRecoveryState next)
        {
            var previous = _state;
            _state = next;
            if (previous == next)
            {
                return;
            }

            DriveFastReconnect(next);
        }

        public void EnterCatchUp(int authoritativeFrame)
        {
            CatchUpTargetFrame = authoritativeFrame;
            LastRecoveryAuthoritativeFrame = authoritativeFrame;
            SetState(SyncRecoveryState.CatchUp);
        }

        public void MarkFullSnapshotResyncNeeded(
            TReason reason,
            int clientFrame,
            int authoritativeFrame,
            uint clientStateHash,
            uint authoritativeStateHash)
        {
            NeedsFullSnapshotResync = true;
            LastRecoveryReason = reason;
            LastRecoveryClientFrame = clientFrame;
            LastRecoveryAuthoritativeFrame = authoritativeFrame;
            LastRecoveryClientStateHash = clientStateHash;
            LastRecoveryAuthoritativeStateHash = authoritativeStateHash;
            CatchUpTargetFrame = authoritativeFrame > clientFrame ? authoritativeFrame : clientFrame;
            SetState(SyncRecoveryState.AwaitingFullSnapshot);
        }

        public void ClearFullSnapshotResync()
        {
            NeedsFullSnapshotResync = false;
            LastRecoveryReason = _noneReason;
            LastRecoveryClientFrame = 0;
            LastRecoveryAuthoritativeFrame = 0;
            LastRecoveryClientStateHash = 0u;
            LastRecoveryAuthoritativeStateHash = 0u;
            CatchUpTargetFrame = 0;
        }

        public void HeartbeatFastReconnect(int authoritativeFrame)
        {
            _fastReconnect.ResetEventBuffer();
            _fastReconnect.Heartbeat(authoritativeFrame);
            CaptureHealthEvents();
        }

        private void DriveFastReconnect(SyncRecoveryState next)
        {
            _fastReconnect.ResetEventBuffer();
            switch (next)
            {
                case SyncRecoveryState.CatchUp:
                {
                    var currentFrame = _getCurrentFrame();
                    var gap = CatchUpTargetFrame > currentFrame ? CatchUpTargetFrame - currentFrame : 1;
                    _fastReconnect.Reconcile(
                        FastReconnectPhase.Resuming,
                        LastRecoveryAuthoritativeFrame,
                        gap);
                    break;
                }
                case SyncRecoveryState.AwaitingFullSnapshot:
                {
                    var gap = LastRecoveryAuthoritativeFrame - LastRecoveryClientFrame;
                    _fastReconnect.Reconcile(
                        FastReconnectPhase.AwaitingFullSnapshot,
                        LastRecoveryAuthoritativeFrame,
                        gap);
                    break;
                }
                case SyncRecoveryState.ApplyingFullSnapshot:
                    break;
                case SyncRecoveryState.Recovered:
                    _fastReconnect.Reconcile(
                        FastReconnectPhase.Recovered,
                        LastRecoveryAuthoritativeFrame,
                        0);
                    break;
                case SyncRecoveryState.Normal:
                default:
                    _fastReconnect.Reconcile(FastReconnectPhase.Connected, _getCurrentFrame(), 0);
                    break;
            }

            CaptureHealthEvents();
        }

        private void CaptureHealthEvents()
        {
            var collected = _fastReconnect.CollectedEvents;
            if (collected.Count == 0)
            {
                _lastHealthEvents = Array.Empty<SyncHealthEvent>();
                return;
            }

            var buffer = new SyncHealthEvent[collected.Count];
            for (var i = 0; i < collected.Count; i++)
            {
                buffer[i] = collected[i];
            }

            _lastHealthEvents = buffer;
        }
    }
}
