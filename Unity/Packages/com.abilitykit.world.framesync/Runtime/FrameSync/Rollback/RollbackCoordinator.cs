using System;
using System.Buffers;
using System.Collections.Generic;
using AbilityKit.Core.Pooling;
using AbilityKit.Core.Logging;

namespace AbilityKit.Ability.FrameSync.Rollback
{
    public sealed class RollbackCoordinator
    {
        private static readonly ObjectPool<List<WorldRollbackSnapshotEntry>> s_entriesListPool = Pools.GetPool(
            createFunc: () => new List<WorldRollbackSnapshotEntry>(16),
            onRelease: list => list.Clear(),
            defaultCapacity: 16,
            maxSize: 256,
            collectionCheck: false);

        private readonly object _operationSync = new object();
        private readonly RollbackRegistry _registry;
        private readonly RollbackSnapshotRingBuffer _buffer;
        private RollbackOperationResult _lastOperationResult;

        public event Action<RollbackOperationResult> OperationCompleted;

        public RollbackOperationResult LastOperationResult
        {
            get
            {
                lock (_operationSync)
                {
                    return _lastOperationResult;
                }
            }
        }

        public RollbackCoordinator(RollbackRegistry registry, RollbackSnapshotRingBuffer buffer)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));

            _registry.Seal();
        }

        public bool CaptureAndStore(FrameIndex frame)
        {
            return TryCaptureAndStore(frame, out _);
        }

        public bool TryCaptureAndStore(FrameIndex frame, out RollbackOperationResult result)
        {
            // Capture directly into the pooled list and hand it to the ring buffer as a span, avoiding
            // Capture's ToArray: the buffer deep-copies regardless, so the intermediate owned array is waste
            // on this hot (per-capture) path. Public Capture still ToArrays for external callers.
            var providers = _registry.Providers;
            var entries = s_entriesListPool.Get();
            if (entries.Capacity < providers.Count) entries.Capacity = providers.Count;
            WorldRollbackSnapshotEntry[] rentedEntries = null;
            try
            {
                FillEntries(frame, providers, entries);
                rentedEntries = ArrayPool<WorldRollbackSnapshotEntry>.Shared.Rent(entries.Count);
                entries.CopyTo(rentedEntries, 0);
                var entrySpan = new ReadOnlySpan<WorldRollbackSnapshotEntry>(rentedEntries, 0, entries.Count);

                // Preserve the Capture result publication (cheap struct) that the public Capture() emits,
                // so observers see the same Capture+Store sequence without an owned intermediate array.
                Publish(RollbackOperationResult.Success(
                    RollbackOperationKind.Capture,
                    frame,
                    entries.Count,
                    CountPayloadBytes(entrySpan)));
                _buffer.Store(frame, entrySpan, WorldRollbackSnapshotCodec.CurrentVersion);
                result = RollbackOperationResult.Success(
                    RollbackOperationKind.Store,
                    frame,
                    entries.Count,
                    CountPayloadBytes(entrySpan));
                Publish(result);
                return true;
            }
            catch (Exception ex)
            {
                result = RollbackOperationResult.Failure(
                    RollbackOperationKind.Store,
                    RollbackOperationStatus.Failed,
                    frame,
                    ex.Message,
                    exception: ex);
                Publish(result);
                return false;
            }
            finally
            {
                if (rentedEntries != null)
                {
                    ArrayPool<WorldRollbackSnapshotEntry>.Shared.Return(rentedEntries, clearArray: true);
                }

                s_entriesListPool.Release(entries);
            }
        }

        public void StoreSnapshot(in WorldRollbackSnapshot snapshot)
        {
            _buffer.Store(snapshot);
            Publish(RollbackOperationResult.Success(
                RollbackOperationKind.Store,
                snapshot.Frame,
                snapshot.Entries != null ? snapshot.Entries.Length : 0,
                CountPayloadBytes(snapshot.Entries)));
        }

        public WorldRollbackSnapshot Capture(FrameIndex frame)
        {
            var providers = _registry.Providers;
            var entries = s_entriesListPool.Get();
            if (entries.Capacity < providers.Count) entries.Capacity = providers.Count;

            try
            {
                FillEntries(frame, providers, entries);

                // Public contract returns a detached, owned array (the snapshot outlives the pooled list).
                var arr = entries.ToArray();
                Publish(RollbackOperationResult.Success(
                    RollbackOperationKind.Capture,
                    frame,
                    entries.Count,
                    CountPayloadBytes(arr)));
                return new WorldRollbackSnapshot(WorldRollbackSnapshotCodec.CurrentVersion, frame, arr);
            }
            finally
            {
                s_entriesListPool.Release(entries);
            }
        }

        private static void FillEntries(
            FrameIndex frame,
            IReadOnlyList<IRollbackStateProvider> providers,
            List<WorldRollbackSnapshotEntry> entries)
        {
            for (int i = 0; i < providers.Count; i++)
            {
                var p = providers[i];
                if (p == null) continue;
                byte[] payload;
                try
                {
                    payload = p.Export(frame) ?? Array.Empty<byte>();
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"Rollback Export failed. key={p.Key} frame={frame.Value}");
                    throw;
                }
                entries.Add(new WorldRollbackSnapshotEntry(p.Key, payload));
            }
        }

        public bool TryRestore(FrameIndex frame)
        {
            return TryRestore(frame, out _);
        }

        public bool TryRestore(FrameIndex frame, out RollbackOperationResult result)
        {
            if (!_buffer.TryGet(frame, out var snapshot))
            {
                result = RollbackOperationResult.Failure(
                    RollbackOperationKind.Restore,
                    RollbackOperationStatus.SnapshotNotFound,
                    frame,
                    $"Rollback snapshot not found. frame={frame.Value}");
                Publish(result);
                return false;
            }

            return TryRestore(snapshot, out result);
        }

        public bool TryRestore(in WorldRollbackSnapshot snapshot, out RollbackOperationResult result)
        {
            var restored = TryRestoreCore(snapshot, out result);
            Publish(result);
            return restored;
        }

        public void Restore(in WorldRollbackSnapshot snapshot)
        {
            if (TryRestoreCore(snapshot, out var result)) return;

            if (result.Exception != null)
            {
                throw new InvalidOperationException(result.Message, result.Exception);
            }

            throw new InvalidOperationException(result.Message);
        }

        public void ClearHistory()
        {
            _buffer.Clear();
            Publish(RollbackOperationResult.Success(RollbackOperationKind.Clear, default));
        }

        private bool TryRestoreCore(
            in WorldRollbackSnapshot snapshot,
            out RollbackOperationResult result)
        {
            if (snapshot.Version != WorldRollbackSnapshotCodec.CurrentVersion)
            {
                result = RollbackOperationResult.Failure(
                    RollbackOperationKind.Restore,
                    RollbackOperationStatus.UnsupportedVersion,
                    snapshot.Frame,
                    $"Unsupported rollback snapshot version: {snapshot.Version}");
                return false;
            }

            var entries = snapshot.Entries;
            if (entries == null || entries.Length == 0)
            {
                result = RollbackOperationResult.Success(RollbackOperationKind.Restore, snapshot.Frame);
                return true;
            }

            // Pooled provider buffer: restore runs on every authoritative mispredict, so avoid a
            // fresh array per call. Returned in finally (cleared — it holds provider references).
            var providers = System.Buffers.ArrayPool<IRollbackStateProvider>.Shared.Rent(entries.Length);
            try
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    var key = entries[i].Key;
                    if (!_registry.TryGet(key, out var provider) || provider == null)
                    {
                        result = RollbackOperationResult.Failure(
                            RollbackOperationKind.Restore,
                            RollbackOperationStatus.ProviderMissing,
                            snapshot.Frame,
                            $"Rollback provider not found. key={key} frame={snapshot.Frame.Value}",
                            key);
                        return false;
                    }

                    providers[i] = provider;
                }

                for (int i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i];
                    try
                    {
                        providers[i].Import(snapshot.Frame, entry.Payload);
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex, $"Rollback Import failed. key={entry.Key} frame={snapshot.Frame.Value} payloadLen={(entry.Payload != null ? entry.Payload.Length : 0)}");
                        result = new RollbackOperationResult(
                            RollbackOperationKind.Restore,
                            RollbackOperationStatus.ProviderFailed,
                            snapshot.Frame,
                            providerKey: entry.Key,
                            providerCount: i,
                            payloadBytes: CountPayloadBytes(entries),
                            message: $"Rollback provider import failed after {i} provider(s). The world may be partially restored. {ex.Message}",
                            exception: ex);
                        return false;
                    }
                }
            }
            finally
            {
                System.Buffers.ArrayPool<IRollbackStateProvider>.Shared.Return(providers, clearArray: true);
            }

            result = RollbackOperationResult.Success(
                RollbackOperationKind.Restore,
                snapshot.Frame,
                entries.Length,
                CountPayloadBytes(entries));
            return true;
        }

        private void Publish(in RollbackOperationResult result)
        {
            lock (_operationSync)
            {
                _lastOperationResult = result;
            }

            var handlers = OperationCompleted;
            if (handlers == null) return;

            foreach (Action<RollbackOperationResult> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(result);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"Rollback operation observer failed. kind={result.Kind} frame={result.Frame.Value}");
                }
            }
        }

        private static int CountPayloadBytes(ReadOnlySpan<WorldRollbackSnapshotEntry> entries)
        {
            if (entries.IsEmpty) return 0;

            var total = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                var payload = entries[i].Payload;
                if (payload != null) total += payload.Length;
            }

            return total;
        }
    }
}
