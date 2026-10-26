using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Owns asynchronous presentation handles by logical key. A session is the
    /// consumer-side counterpart of the snapshot registry: replacing a key first
    /// stops its previous handle, while transient requests are never retained.
    /// </summary>
    public sealed class PresentationSession<TBackendHandle>
    {
        private readonly IPresentationBackend<TBackendHandle> _backend;
        private readonly Dictionary<string, Entry> _active = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _versions = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly object _gate = new object();
        private long _epoch;

        private sealed class Entry
        {
            public PresentationRequest Request;
            public PresentationHandle<TBackendHandle> Handle;
        }

        public PresentationSession(IPresentationBackend<TBackendHandle> backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public int ActiveCount
        {
            get { lock (_gate) return _active.Count; }
        }

        public bool TryGet(string logicalKey, out PresentationHandle<TBackendHandle> handle)
        {
            lock (_gate)
            {
                if (!string.IsNullOrEmpty(logicalKey) && _active.TryGetValue(logicalKey, out var entry))
                {
                    handle = entry.Handle;
                    return true;
                }
            }

            handle = null;
            return false;
        }

        public async Task<PresentationHandle<TBackendHandle>> PlayAsync(
            PresentationRequest request,
            bool replaceExisting = true,
            CancellationToken cancellationToken = default)
        {
            if (!request.IsValid) return null;
            cancellationToken.ThrowIfCancellationRequested();

            Entry previous = null;
            long version;
            long epoch;
            lock (_gate)
            {
                version = NextVersion(request.LogicalKey);
                epoch = _epoch;
                if (replaceExisting && _active.TryGetValue(request.LogicalKey, out previous))
                    _active.Remove(request.LogicalKey);
            }

            if (previous != null)
                await StopEntryAsync(previous).ConfigureAwait(false);

            var handle = await _backend.PlayAsync(in request, cancellationToken).ConfigureAwait(false);
            if (handle == null) return null;

            if (cancellationToken.IsCancellationRequested)
            {
                await handle.StopAsync().ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }

            var stale = false;
            lock (_gate)
            {
                if (epoch != _epoch ||
                    !_versions.TryGetValue(request.LogicalKey, out var currentVersion) ||
                    currentVersion != version)
                {
                    stale = true;
                }
                else
                {
                    if (_active.TryGetValue(request.LogicalKey, out var replaced))
                    {
                        _active.Remove(request.LogicalKey);
                        _ = StopEntrySafelyAsync(replaced);
                    }
                    _active[request.LogicalKey] = new Entry { Request = request, Handle = handle };
                }
            }

            if (stale)
            {
                await handle.StopAsync().ConfigureAwait(false);
                return null;
            }

            ObserveFailure(request.LogicalKey, version, handle);

            return handle;
        }

        public async Task<PresentationHandle<TBackendHandle>> EnsureAsync(
            PresentationRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!request.IsValid) return null;

            PresentationHandle<TBackendHandle> existing = null;
            lock (_gate)
            {
                if (_active.TryGetValue(request.LogicalKey, out var entry)) existing = entry.Handle;
            }

            if (existing != null &&
                existing.State != PresentationHandleState.Failed &&
                existing.State != PresentationHandleState.Stopped &&
                _backend.IsAlive(existing.BackendHandle))
            {
                _backend.Update(existing.BackendHandle, in request);
                lock (_gate)
                {
                    if (_active.TryGetValue(request.LogicalKey, out var entry) &&
                        ReferenceEquals(entry.Handle, existing))
                        entry.Request = request;
                }
                return existing;
            }

            return await PlayAsync(request, replaceExisting: true, cancellationToken).ConfigureAwait(false);
        }

        public async Task<PresentationHandle<TBackendHandle>> EmitTransientAsync(
            PresentationRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!request.IsValid) return null;
            cancellationToken.ThrowIfCancellationRequested();
            var handle = await _backend.PlayAsync(in request, cancellationToken).ConfigureAwait(false);
            if (handle != null && cancellationToken.IsCancellationRequested)
            {
                await handle.StopAsync().ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }
            ObserveTransientFailure(handle);
            return handle;
        }

        public async Task<bool> StopAsync(string logicalKey)
        {
            if (string.IsNullOrEmpty(logicalKey)) return false;

            Entry entry;
            lock (_gate)
            {
                NextVersion(logicalKey);
                if (!_active.TryGetValue(logicalKey, out entry)) return false;
                _active.Remove(logicalKey);
            }

            await StopEntryAsync(entry).ConfigureAwait(false);
            return true;
        }

        public async Task ClearAsync()
        {
            Entry[] entries;
            lock (_gate)
            {
                _epoch++;
                entries = new Entry[_active.Count];
                _active.Values.CopyTo(entries, 0);
                _active.Clear();
            }

            List<Exception> failures = null;
            for (var i = 0; i < entries.Length; i++)
            {
                try { await StopEntryAsync(entries[i]).ConfigureAwait(false); }
                catch (Exception exception)
                {
                    failures ??= new List<Exception>();
                    failures.Add(exception);
                }
            }

            if (failures != null) throw new AggregateException("One or more presentation handles failed to stop.", failures);
        }

        private long NextVersion(string logicalKey)
        {
            if (!_versions.TryGetValue(logicalKey, out var version)) version = 0;
            version++;
            _versions[logicalKey] = version;
            return version;
        }

        private static Task StopEntryAsync(Entry entry)
        {
            return entry?.Handle == null ? Task.CompletedTask : entry.Handle.StopAsync();
        }

        private static async Task StopEntrySafelyAsync(Entry entry)
        {
            try { await StopEntryAsync(entry).ConfigureAwait(false); }
            catch { }
        }

        private void ObserveFailure(
            string logicalKey,
            long version,
            PresentationHandle<TBackendHandle> handle)
        {
            if (handle?.Ready == null ||
                (handle.Ready.IsCompleted && !handle.Ready.IsFaulted && !handle.Ready.IsCanceled)) return;
            _ = ObserveFailureAsync(logicalKey, version, handle);
        }

        private void ObserveTransientFailure(PresentationHandle<TBackendHandle> handle)
        {
            if (handle?.Ready == null ||
                (handle.Ready.IsCompleted && !handle.Ready.IsFaulted && !handle.Ready.IsCanceled)) return;
            _ = ObserveTransientFailureAsync(handle);
        }

        private static async Task ObserveTransientFailureAsync(PresentationHandle<TBackendHandle> handle)
        {
            try { await handle.Ready.ConfigureAwait(false); }
            catch { await StopHandleSafelyAsync(handle).ConfigureAwait(false); }
        }

        private static async Task StopHandleSafelyAsync(PresentationHandle<TBackendHandle> handle)
        {
            try { await handle.StopAsync().ConfigureAwait(false); }
            catch { }
        }

        private async Task ObserveFailureAsync(
            string logicalKey,
            long version,
            PresentationHandle<TBackendHandle> handle)
        {
            try
            {
                await handle.Ready.ConfigureAwait(false);
            }
            catch
            {
                var remove = false;
                lock (_gate)
                {
                    if (_versions.TryGetValue(logicalKey, out var currentVersion) &&
                        currentVersion == version &&
                        _active.TryGetValue(logicalKey, out var entry) &&
                        ReferenceEquals(entry.Handle, handle))
                    {
                        _active.Remove(logicalKey);
                        NextVersion(logicalKey);
                        remove = true;
                    }
                }

                if (remove) await StopEntrySafelyAsync(new Entry { Handle = handle }).ConfigureAwait(false);
            }
        }

        public async Task<PresentationCompositeHandle<TBackendHandle>> PlayCompositeAsync(
            PresentationCompositeTemplate template,
            CancellationToken cancellationToken = default)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));

            var handles = new List<PresentationHandle<TBackendHandle>>(template.Children.Count);
            try
            {
                for (var i = 0; i < template.Children.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var child = template.Children[i];
                    var handle = await EmitTransientAsync(child, cancellationToken).ConfigureAwait(false);
                    if (handle == null) throw new InvalidOperationException("Unable to create composite presentation child.");
                    handles.Add(handle);
                }

                return new PresentationCompositeHandle<TBackendHandle>(handles);
            }
            catch
            {
                for (var i = handles.Count - 1; i >= 0; i--) await handles[i].StopAsync().ConfigureAwait(false);
                throw;
            }
        }
    }
}
