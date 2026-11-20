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
        private readonly object _restoreSync = new object();
        private readonly RollbackRegistry _registry;
        private readonly RollbackSnapshotRingBuffer _buffer;
        private RollbackOperationResult _lastOperationResult;
        private FrameIndex? _restoreFloor;
        private int _restoreDepth;

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

        /// <summary>
        /// 读取本协调器已生效的最早可恢复帧。未安装权威结算边界时返回 false。
        /// </summary>
        public bool TryGetRestoreFloor(out FrameIndex frame)
        {
            lock (_restoreSync)
            {
                frame = _restoreFloor.GetValueOrDefault();
                return _restoreFloor.HasValue;
            }
        }

        /// <summary>
        /// 提交已验证的权威锚点。调用方必须在会话帧边界确认锚点位于有效轨迹，
        /// 并排除正在等待或重演的旧纠偏；协调器只验证本地快照存在性和恢复准入。
        /// </summary>
        public bool TryAdvanceRestoreFloor(FrameIndex frame)
        {
            lock (_restoreSync)
            {
                if (_restoreDepth != 0 || frame.Value < 0) return false;
                if (_restoreFloor is FrameIndex current)
                {
                    if (frame.Value < current.Value) return false;
                    if (frame.Value == current.Value) return true;
                }

                if (!_buffer.Contains(frame)) return false;
                _restoreFloor = frame;
                return true;
            }
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

        public bool TryRestore(FrameIndex frame, out RollbackOperationResult result) =>
            TryRestore(frame, discardFuture: false, out result);

        /// <summary>
        /// 恢复预测分支并在发布结果前丢弃恢复点之后的历史；失败时保留已有快照。
        /// </summary>
        public bool TryRestoreAndDiscardFuture(FrameIndex frame, out RollbackOperationResult result) =>
            TryRestore(frame, discardFuture: true, out result);

        private bool TryRestore(FrameIndex frame, bool discardFuture, out RollbackOperationResult result)
        {
            bool restored;
            lock (_restoreSync)
            {
                if (IsBelowRestoreFloor(frame, out result)) restored = false;
                else if (_buffer.TryGet(frame, out var snapshot))
                {
                    restored = RestoreAdmittedSnapshot(snapshot, out result);
                    if (restored && discardFuture) _buffer.DiscardAfter(frame);
                }
                else
                {
                    result = RollbackOperationResult.Failure(
                        RollbackOperationKind.Restore,
                        RollbackOperationStatus.SnapshotNotFound,
                        frame,
                        $"Rollback snapshot not found. frame={frame.Value}");
                    restored = false;
                }
            }

            Publish(result);
            return restored;
        }

        public bool TryRestore(in WorldRollbackSnapshot snapshot, out RollbackOperationResult result)
        {
            var restored = RestoreWithAdmission(snapshot, out result);
            Publish(result);
            return restored;
        }

        public void Restore(in WorldRollbackSnapshot snapshot)
        {
            if (RestoreWithAdmission(snapshot, out var result)) return;

            if (result.Exception != null)
            {
                throw new InvalidOperationException(result.Message, result.Exception);
            }

            throw new InvalidOperationException(result.Message);
        }

        public void ClearHistory()
        {
            lock (_restoreSync) _buffer.Clear();
            Publish(RollbackOperationResult.Success(RollbackOperationKind.Clear, default));
        }

        private bool RestoreWithAdmission(in WorldRollbackSnapshot snapshot, out RollbackOperationResult result)
        {
            lock (_restoreSync)
            {
                if (IsBelowRestoreFloor(snapshot.Frame, out result)) return false;
                return RestoreAdmittedSnapshot(snapshot, out result);
            }
        }

        private bool IsBelowRestoreFloor(FrameIndex frame, out RollbackOperationResult result)
        {
            if (_restoreFloor is FrameIndex floor && frame.Value < floor.Value)
            {
                result = RollbackOperationResult.Failure(
                    RollbackOperationKind.Restore, RollbackOperationStatus.BeforeRestoreFloor, frame,
                    $"Rollback frame is before the restore floor. frame={frame.Value} floor={floor.Value}");
                return true;
            }

            result = default;
            return false;
        }

        private bool RestoreAdmittedSnapshot(in WorldRollbackSnapshot snapshot, out RollbackOperationResult result)
        {
            _restoreDepth++;
            try { return TryRestoreCore(snapshot, out result); }
            finally { _restoreDepth--; }
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
            foreach (var requiredKey in _registry.RequiredStateKeys)
            {
                var count = 0;
                if (entries != null)
                    for (var i = 0; i < entries.Length; i++)
                        if (entries[i].Key == requiredKey) count++;
                if (count == 1) continue;
                result = RollbackOperationResult.Failure(
                    RollbackOperationKind.Restore,
                    RollbackOperationStatus.ProviderMissing,
                    snapshot.Frame,
                    $"Rollback snapshot requires exactly one state entry. key={requiredKey} count={count}",
                    requiredKey);
                return false;
            }
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
                var providerMap = new Dictionary<int, IRollbackStateProvider>(entries.Length);
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
                    providerMap[key] = provider;
                }

                RollbackSnapshotPreflightContext snapshotContext;
                try
                {
                    snapshotContext = new RollbackSnapshotPreflightContext(
                        snapshot.Frame, entries, providerMap, _registry.StructureRestoreOrder);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"Rollback snapshot preflight context failed. frame={snapshot.Frame.Value}");
                    result = new RollbackOperationResult(
                        RollbackOperationKind.Restore,
                        RollbackOperationStatus.ProviderFailed,
                        snapshot.Frame,
                        providerCount: 0,
                        payloadBytes: CountPayloadBytes(entries),
                        message: $"Rollback snapshot preflight failed before any provider import. {ex.Message}",
                        exception: ex);
                    return false;
                }

                for (int i = 0; i < entries.Length; i++)
                {
                    if (!(providers[i] is IRollbackSnapshotPreflightProvider snapshotPreflight)) continue;

                    try
                    {
                        snapshotPreflight.ValidateSnapshot(snapshotContext);
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex, $"Rollback snapshot preflight failed. key={entries[i].Key} frame={snapshot.Frame.Value}");
                        result = new RollbackOperationResult(
                            RollbackOperationKind.Restore,
                            RollbackOperationStatus.ProviderFailed,
                            snapshot.Frame,
                            providerKey: entries[i].Key,
                            providerCount: 0,
                            payloadBytes: CountPayloadBytes(entries),
                            message: $"Rollback snapshot preflight failed before any provider import. {ex.Message}",
                            exception: ex);
                        return false;
                    }
                }

                for (int i = 0; i < entries.Length; i++)
                {
                    if (!(providers[i] is IRollbackStatePreflightProvider preflight)) continue;

                    var entry = entries[i];
                    try
                    {
                        preflight.ValidateImport(snapshot.Frame, entry.Payload);
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex, $"Rollback preflight failed. key={entry.Key} frame={snapshot.Frame.Value} payloadLen={(entry.Payload != null ? entry.Payload.Length : 0)}");
                        result = new RollbackOperationResult(
                            RollbackOperationKind.Restore,
                            RollbackOperationStatus.ProviderFailed,
                            snapshot.Frame,
                            providerKey: entry.Key,
                            providerCount: 0,
                            payloadBytes: CountPayloadBytes(entries),
                            message: $"Rollback provider preflight failed before any provider import. {ex.Message}",
                            exception: ex);
                        return false;
                    }
                }

                var importedCount = 0;
                if (!TryImportProviders(
                        snapshot,
                        entries,
                        providers,
                        snapshotContext.StructureRestoreEntryIndices,
                        structuralPhase: true,
                        ref importedCount,
                        out result))
                {
                    return false;
                }

                if (!TryImportProviders(
                        snapshot,
                        entries,
                        providers,
                        snapshotContext.StructureRestoreEntryIndices,
                        structuralPhase: false,
                        ref importedCount,
                        out result))
                {
                    return false;
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

        private static bool TryImportProviders(
            in WorldRollbackSnapshot snapshot,
            WorldRollbackSnapshotEntry[] entries,
            IRollbackStateProvider[] providers,
            IReadOnlyList<int> structureRestoreEntryIndices,
            bool structuralPhase,
            ref int importedCount,
            out RollbackOperationResult result)
        {
            /*
             * 结构阶段执行预检所见的同一份有效计划；普通项继续保留输入相对次序。
             * 导入失败沿用部分恢复的失败合同，不继续后续结构或字段导入。
             */
            var count = structuralPhase ? structureRestoreEntryIndices.Count : entries.Length;
            for (var position = 0; position < count; position++)
            {
                var i = structuralPhase ? structureRestoreEntryIndices[position] : position;
                var provider = providers[i];
                if (!structuralPhase && provider is IRollbackStructureRestoreProvider) continue;

                var entry = entries[i];
                try
                {
                    provider.Import(snapshot.Frame, entry.Payload);
                    importedCount++;
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"Rollback Import failed. key={entry.Key} frame={snapshot.Frame.Value} payloadLen={(entry.Payload != null ? entry.Payload.Length : 0)}");
                    result = new RollbackOperationResult(
                        RollbackOperationKind.Restore,
                        RollbackOperationStatus.ProviderFailed,
                        snapshot.Frame,
                        providerKey: entry.Key,
                        providerCount: importedCount,
                        payloadBytes: CountPayloadBytes(entries),
                        message: $"Rollback provider import failed after {importedCount} provider(s). The world may be partially restored. {ex.Message}",
                        exception: ex);
                    return false;
                }
            }

            result = default;
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
