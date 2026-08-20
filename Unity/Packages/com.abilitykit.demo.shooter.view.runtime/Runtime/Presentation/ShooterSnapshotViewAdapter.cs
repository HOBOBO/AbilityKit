using System;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.View
{
    public sealed class ShooterSnapshotViewAdapter
    {
        private readonly ShooterSnapshotViewModelMapper _mapper;
        private readonly ShooterSnapshotViewModel _viewModel = new ShooterSnapshotViewModel();

        public ShooterSnapshotViewAdapter()
            : this(new ShooterSnapshotViewModelMapper())
        {
        }

        public ShooterSnapshotViewAdapter(ShooterSnapshotViewModelMapper mapper)
        {
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public ShooterSnapshotViewModel ViewModel => _viewModel;

        public ShooterSnapshotViewBatch CurrentBatch => _viewModel.Current;

        public ShooterSnapshotViewBatch ApplyPayload(byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            var snapshot = ShooterStateSnapshotCodec.Deserialize(payload);
            return ApplySnapshot(in snapshot);
        }

        public ShooterSnapshotViewBatch ApplySnapshot(in ShooterStateSnapshotPayload snapshot)
        {
            return ApplySnapshot(in snapshot, ShooterViewBatchSource.LocalPrediction);
        }

        public ShooterSnapshotViewBatch ApplySnapshot(in ShooterStateSnapshotPayload snapshot, ShooterViewBatchSource source)
        {
            var batch = _mapper.Map(in snapshot, source);
            _viewModel.Apply(in batch);
            return batch;
        }

        public ShooterSnapshotViewBatch ApplyControlledPlayerPrediction(
            in ShooterStateSnapshotPayload snapshot,
            int controlledPlayerId)
        {
            var batch = _mapper.MapControlledPlayerPrediction(in snapshot, controlledPlayerId);
            _viewModel.Apply(in batch);
            return batch;
        }

        public ShooterSnapshotViewBatch ApplyPureStateSnapshot(in ShooterPureStateSnapshotPayload snapshot, int controlledPlayerId = -1)
        {
            var batch = _mapper.Map(in snapshot, controlledPlayerId);
            _viewModel.Apply(in batch);
            return batch;
        }

        public ShooterSnapshotViewBatch ApplyGatewaySnapshot(in ShooterGatewaySnapshot snapshot)
        {
            var batch = _mapper.Map(in snapshot);
            _viewModel.Apply(in batch);
            return batch;
        }

        public ShooterSnapshotViewBatch ApplyGatewaySnapshot(in ShooterGatewaySnapshot snapshot, int controlledPlayerId)
        {
            var batch = _mapper.Map(in snapshot, controlledPlayerId);
            _viewModel.Apply(in batch);
            return batch;
        }

        public ShooterSnapshotViewBatch Clear()
        {
            _mapper.ClearTrackedState();
            _viewModel.Clear();
            return _viewModel.Current;
        }
    }
}
