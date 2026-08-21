#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Network.Sdk
{
    /// <summary>
    /// Stable multi-project identity for one reusable SDK client. Role is intentionally part of
    /// the key so Room control and Battle data-plane clients cannot be merged accidentally.
    /// </summary>
    public readonly struct NetworkSdkClientKey : IEquatable<NetworkSdkClientKey>
    {
        public NetworkSdkClientKey(string projectId, string role, string instanceId = "default")
        {
            ProjectId = Require(projectId, nameof(projectId));
            Role = Require(role, nameof(role));
            InstanceId = Require(instanceId, nameof(instanceId));
        }

        public string ProjectId { get; }
        public string Role { get; }
        public string InstanceId { get; }

        public bool Equals(NetworkSdkClientKey other) =>
            string.Equals(ProjectId, other.ProjectId, StringComparison.Ordinal) &&
            string.Equals(Role, other.Role, StringComparison.Ordinal) &&
            string.Equals(InstanceId, other.InstanceId, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is NetworkSdkClientKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = StringComparer.Ordinal.GetHashCode(ProjectId ?? string.Empty);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Role ?? string.Empty);
                return (hash * 397) ^ StringComparer.Ordinal.GetHashCode(InstanceId ?? string.Empty);
            }
        }

        public override string ToString() => $"{ProjectId}/{Role}/{InstanceId}";

        public static bool operator ==(NetworkSdkClientKey left, NetworkSdkClientKey right) => left.Equals(right);
        public static bool operator !=(NetworkSdkClientKey left, NetworkSdkClientKey right) => !left.Equals(right);

        private static string Require(string value, string parameterName) =>
            string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("A non-empty value is required.", parameterName)
                : value.Trim();
    }

    /// <summary>
    /// Owns reusable SDK clients and provides explicit leases to feature/session consumers.
    /// Entries remain cached after the last lease is released until Remove or Dispose is called.
    /// </summary>
    public sealed class NetworkSdkClientHub : IDisposable
    {
        private readonly object _gate = new object();
        private readonly Dictionary<NetworkSdkClientKey, Entry> _entries = new();
        private bool _disposed;

        public int Count
        {
            get
            {
                lock (_gate) return _entries.Count;
            }
        }

        public NetworkSdkClientLease Acquire(
            NetworkSdkClientKey key,
            Func<NetworkSdkClient> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            lock (_gate)
            {
                ThrowIfDisposed();
                if (!_entries.TryGetValue(key, out var entry))
                {
                    var client = factory()
                        ?? throw new InvalidOperationException($"SDK client factory returned null for '{key}'.");
                    entry = new Entry(client);
                    _entries.Add(key, entry);
                }

                entry.LeaseCount++;
                return new NetworkSdkClientLease(this, key, entry);
            }
        }

        public NetworkSdkClientLease Acquire(NetworkSdkClientKey key, NetworkSdkBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            return Acquire(key, builder.Build);
        }

        public bool TryGet(NetworkSdkClientKey key, out NetworkSdkClient client)
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                if (_entries.TryGetValue(key, out var entry))
                {
                    client = entry.Client;
                    return true;
                }

                client = null!;
                return false;
            }
        }

        public int GetLeaseCount(NetworkSdkClientKey key)
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                return _entries.TryGetValue(key, out var entry) ? entry.LeaseCount : 0;
            }
        }

        public bool Remove(NetworkSdkClientKey key, bool dispose = true)
        {
            NetworkSdkClient? client = null;
            lock (_gate)
            {
                ThrowIfDisposed();
                if (!_entries.TryGetValue(key, out var entry)) return false;
                if (entry.LeaseCount != 0)
                {
                    throw new InvalidOperationException(
                        $"Cannot remove SDK client '{key}' while {entry.LeaseCount} lease(s) are active.");
                }

                _entries.Remove(key);
                if (dispose) client = entry.Client;
            }

            client?.Dispose();
            return true;
        }

        public void Dispose()
        {
            NetworkSdkClient[] clients;
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                clients = new NetworkSdkClient[_entries.Count];
                var index = 0;
                foreach (var entry in _entries.Values)
                {
                    entry.IsDisposed = true;
                    clients[index++] = entry.Client;
                }
                _entries.Clear();
            }

            foreach (var client in clients)
            {
                client.Dispose();
            }
        }

        internal void Release(NetworkSdkClientKey key, Entry entry)
        {
            lock (_gate)
            {
                if (entry.LeaseCount == 0) return;
                entry.LeaseCount--;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NetworkSdkClientHub));
        }

        internal sealed class Entry
        {
            public Entry(NetworkSdkClient client) => Client = client;
            public NetworkSdkClient Client { get; }
            public int LeaseCount { get; set; }
            public bool IsDisposed { get; set; }
        }
    }

    /// <summary>Scoped access token for a client owned by <see cref="NetworkSdkClientHub"/>.</summary>
    public sealed class NetworkSdkClientLease : IDisposable
    {
        private readonly NetworkSdkClientHub _hub;
        private readonly NetworkSdkClientHub.Entry _entry;
        private bool _disposed;

        internal NetworkSdkClientLease(
            NetworkSdkClientHub hub,
            NetworkSdkClientKey key,
            NetworkSdkClientHub.Entry entry)
        {
            _hub = hub;
            Key = key;
            _entry = entry;
        }

        public NetworkSdkClientKey Key { get; }

        public NetworkSdkClient Client
        {
            get
            {
                if (_disposed || _entry.IsDisposed)
                    throw new ObjectDisposedException(nameof(NetworkSdkClientLease));
                return _entry.Client;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _hub.Release(Key, _entry);
        }
    }
}
