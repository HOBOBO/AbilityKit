#nullable enable

using System;

namespace AbilityKit.Demo.Shooter.View
{
    public sealed class ShooterDotsSnapshotViewBinder : IShooterViewBinder, IDisposable
    {
        private readonly ShooterPresentationFacade _presentation;
        private readonly IShooterSnapshotViewSink _sink;
        private readonly ShooterSnapshotViewProjection _projection = new ShooterSnapshotViewProjection();
        private bool _disposed;
 
        public bool InterpolationEnabled { get; set; } = true;

        public bool HasBufferedSnapshots => _presentation.Snapshots.BufferedSnapshotCount > 0;

        public ShooterViewEntityStore Store => _projection.Store;

        public ShooterViewProjectionApplyResult LastApplyResult => _projection.LastApplyResult;

        public int AppliedBatchCount { get; private set; }

        public int AppliedEntityChangeCount { get; private set; }

        public int AppliedComponentChangeCount { get; private set; }

        public ShooterDotsSnapshotViewBinder(ShooterPresentationFacade presentation, IShooterSnapshotViewSink? sink)
        {
            _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            _sink = sink ?? ShooterNullSnapshotViewSink.Instance;
            _presentation.Snapshots.SnapshotApplied += OnSnapshotApplied;
        }

        public void Sync(in ShooterSnapshotViewBatch batch)
        {
            _projection.Apply(in batch);
            AppliedBatchCount++;
            AppliedEntityChangeCount += batch.EntityChangeCount + batch.RemovedEntityCount;
            AppliedComponentChangeCount += batch.ComponentChangeCount;
            _sink.ApplySnapshot(in batch);
        }

        public void TickInterpolation(float deltaTime)
        {
            if (!InterpolationEnabled)
            {
                return;
            }

            if (_presentation.Snapshots.TryAdvancePlaybackTransient(deltaTime, out var batch))
            {
                Sync(in batch);
            }
        }

        public void RebindAll()
        {
            if (_presentation.Snapshots.TrySampleLatest(out var batch))
            {
                Sync(in batch);
            }
        }

        public void Clear()
        {
            _presentation.Snapshots.Reset();
            _projection.Clear();
            AppliedBatchCount = 0;
            AppliedEntityChangeCount = 0;
            AppliedComponentChangeCount = 0;
            _sink.Clear();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _presentation.Snapshots.SnapshotApplied -= OnSnapshotApplied;
            Clear();
        }

        private void OnSnapshotApplied(ShooterSnapshotViewBatch batch)
        {
            if (!InterpolationEnabled)
            {
                Sync(in batch);
            }
        }
    }
}
