using System;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.View
{
    public sealed class ShooterPresentationFacade
    {
        private readonly ShooterGatewaySnapshotDecoder _gatewayDecoder;
        private readonly ShooterSnapshotViewAdapter _adapter;
        private readonly ShooterSnapshotStream _stream;
        private readonly ShooterReconciliationDiagnosticsStream _diagnosticsStream;
        private readonly ShooterPureStateSnapshotSyncController _pureStateSync;
        private int _controlledPlayerId;

        public ShooterPresentationFacade()
            : this(
                new ShooterGatewaySnapshotDecoder(),
                new ShooterSnapshotViewAdapter(),
                new ShooterSnapshotStream(),
                new ShooterReconciliationDiagnosticsStream())
        {
        }

        public ShooterPresentationFacade(
            ShooterGatewaySnapshotDecoder gatewayDecoder,
            ShooterSnapshotViewAdapter adapter,
            ShooterSnapshotStream stream)
            : this(gatewayDecoder, adapter, stream, new ShooterReconciliationDiagnosticsStream())
        {
        }

        public ShooterPresentationFacade(
            ShooterGatewaySnapshotDecoder gatewayDecoder,
            ShooterSnapshotViewAdapter adapter,
            ShooterSnapshotStream stream,
            ShooterReconciliationDiagnosticsStream diagnosticsStream)
        {
            _gatewayDecoder = gatewayDecoder ?? throw new ArgumentNullException(nameof(gatewayDecoder));
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _diagnosticsStream = diagnosticsStream ?? throw new ArgumentNullException(nameof(diagnosticsStream));
            _pureStateSync = new ShooterPureStateSnapshotSyncController(snapshot => ApplyPureStateSnapshot(in snapshot), _gatewayDecoder);
        }

        public ShooterSnapshotViewModel ViewModel => _adapter.ViewModel;

        public ShooterSnapshotStream Snapshots => _stream;

        public ShooterReconciliationDiagnosticsStream ReconciliationDiagnostics => _diagnosticsStream;

        public bool NeedsPureStateFullBaselineResync => _pureStateSync.NeedsFullBaselineResync;

        public ShooterPureStateResyncReason LastPureStateResyncReason => _pureStateSync.LastResyncReason;

        public int LastPureStateAppliedFrame => _pureStateSync.LastAppliedFrame;

        public uint LastPureStateAppliedStateHash => _pureStateSync.LastAppliedStateHash;

        public int LastPureStateResyncFrame => _pureStateSync.LastResyncFrame;

        public uint LastPureStateResyncStateHash => _pureStateSync.LastResyncStateHash;

        public ShooterPureStateSyncDiagnostics LastPureStateSyncDiagnostics => _pureStateSync.LastDiagnostics;

        public int ControlledPlayerId
        {
            get => _controlledPlayerId;
            set => _controlledPlayerId = value;
        }

        public void PublishReconciliation(in ShooterClientReconciliationResult result)
        {
            _diagnosticsStream.Publish(in result);
        }

        public bool TryApplyGatewayPush(uint opCode, ArraySegment<byte> payload)
        {
            if (!_gatewayDecoder.IsSnapshotPush(opCode))
            {
                return false;
            }

            var snapshot = _gatewayDecoder.Decode(payload);
            if (snapshot.PureStateSnapshot.HasValue)
            {
                var result = _pureStateSync.ApplyGatewaySnapshot(in snapshot);
                return result != ShooterPureStateSnapshotApplyResult.Ignored &&
                       result != ShooterPureStateSnapshotApplyResult.UnsupportedVersion;
            }

            ApplyGatewaySnapshot(in snapshot);
            return true;
        }

        public void ApplyGatewaySnapshot(in ShooterGatewaySnapshot snapshot)
        {
            var batch = _adapter.ApplyGatewaySnapshot(in snapshot);
            _stream.Publish(in batch);
        }

        public void ApplyInterpolatedGatewaySnapshot(in ShooterGatewaySnapshot snapshot)
        {
            var batch = _adapter.ApplyGatewaySnapshot(in snapshot, _controlledPlayerId);
            _stream.Publish(in batch);
        }

        public ShooterPureStateSnapshotApplyResult ApplyPureStateGatewaySnapshot(in ShooterGatewaySnapshot snapshot)
        {
            return _pureStateSync.ApplyGatewaySnapshot(in snapshot);
        }

        public void ApplyPureStateSnapshot(in ShooterPureStateSnapshotPayload snapshot)
        {
            _stream.InterpolationDelayFrames = Math.Max(0, snapshot.Settings.InterpolationDelayFrames);
            var batch = _adapter.ApplyPureStateSnapshot(in snapshot, _controlledPlayerId);
            _stream.Publish(in batch);
        }

        public void ApplyShooterPayload(byte[] payload)
        {
            var batch = _adapter.ApplyPayload(payload);
            _stream.Publish(in batch);
        }

        public void ApplyShooterSnapshot(in ShooterStateSnapshotPayload snapshot)
        {
            ApplyLocalPredictionSnapshot(in snapshot);
        }
 
        public void ApplyLocalPredictionSnapshot(in ShooterStateSnapshotPayload snapshot)
        {
            var batch = _adapter.ApplySnapshot(in snapshot, ShooterViewBatchSource.LocalPrediction);
            _stream.Publish(in batch);
        }

        public void ApplyLocalAuthoritativeSnapshot(in ShooterStateSnapshotPayload snapshot)
        {
            var batch = _adapter.ApplySnapshot(in snapshot, ShooterViewBatchSource.LocalAuthoritative);
            _stream.Publish(in batch);
        }

        public void Clear()
        {
            var batch = _adapter.Clear();
            _stream.Publish(in batch);
        }
    }
}
