using System;
using System.Collections.Generic;
using AbilityKit.Network.Abstractions;

namespace AbilityKit.Network.Runtime
{
    public sealed class AbilityKitConnectionRegistry : IAbilityKitConnectionRegistry
    {
        private readonly Dictionary<AbilityKitConnectionRole, IConnection> _connections = new Dictionary<AbilityKitConnectionRole, IConnection>();
        private readonly Dictionary<AbilityKitConnectionRole, ConnectionLeaseEntry> _leaseEntries = new Dictionary<AbilityKitConnectionRole, ConnectionLeaseEntry>();
        private bool _disposed;

        public IAbilityKitConnectionLease Acquire(AbilityKitConnectionDescriptor descriptor, Func<AbilityKitConnectionDescriptor, IConnection> factory)
        {
            ThrowIfDisposed();
            if (_connections.TryGetValue(descriptor.Role, out var existing))
            {
                if (!_leaseEntries.TryGetValue(descriptor.Role, out var entry))
                {
                    entry = new ConnectionLeaseEntry(existing, null);
                    _leaseEntries.Add(descriptor.Role, entry);
                }

                if (entry.Retiring || (entry.Descriptor.HasValue && !SameEndpoint(entry.Descriptor.Value, descriptor)))
                    throw new InvalidOperationException($"Connection role '{descriptor.Role}' is already bound to another endpoint or is retiring.");

                entry.LeaseCount++;
                return new ConnectionLease(this, descriptor.Role, entry, false);
            }

            if (factory == null) throw new ArgumentNullException(nameof(factory));
            var connection = factory(descriptor) ?? throw new InvalidOperationException($"Connection factory returned null for role '{descriptor.Role}'.");
            var created = new ConnectionLeaseEntry(connection, descriptor) { LeaseCount = 1 };
            _connections.Add(descriptor.Role, connection);
            _leaseEntries.Add(descriptor.Role, created);
            return new ConnectionLease(this, descriptor.Role, created, true);
        }

        public bool TryGet(AbilityKitConnectionRole role, out IConnection connection)
        {
            ThrowIfDisposed();
            return _connections.TryGetValue(role, out connection);
        }

        public IConnection GetRequired(AbilityKitConnectionRole role)
        {
            ThrowIfDisposed();
            if (_connections.TryGetValue(role, out var connection)) return connection;
            throw new InvalidOperationException($"Connection role '{role}' is not registered.");
        }

        public void Register(AbilityKitConnectionRole role, IConnection connection, bool disposeOnReplace = true)
        {
            ThrowIfDisposed();
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (_connections.TryGetValue(role, out var registered) && ReferenceEquals(registered, connection))
                return;

            if (_connections.TryGetValue(role, out var existing) && !ReferenceEquals(existing, connection))
            {
                RetireEntry(role, existing, disposeOnReplace);
            }

            _connections[role] = connection;
            _leaseEntries.Remove(role);
        }

        public IConnection GetOrCreate(AbilityKitConnectionDescriptor descriptor, Func<AbilityKitConnectionDescriptor, IConnection> factory)
        {
            ThrowIfDisposed();
            if (_connections.TryGetValue(descriptor.Role, out var connection))
            {
                if (_leaseEntries.TryGetValue(descriptor.Role, out var existing) &&
                    (existing.Retiring ||
                     (existing.Descriptor.HasValue && !SameEndpoint(existing.Descriptor.Value, descriptor))))
                    throw new InvalidOperationException($"Connection role '{descriptor.Role}' is already bound to another endpoint.");
                return connection;
            }
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            connection = factory(descriptor);
            if (connection == null)
            {
                throw new InvalidOperationException($"Connection factory returned null for role '{descriptor.Role}'.");
            }

            _connections.Add(descriptor.Role, connection);
            _leaseEntries.Add(descriptor.Role, new ConnectionLeaseEntry(connection, descriptor));
            return connection;
        }

        public bool Remove(AbilityKitConnectionRole role, bool dispose = true)
        {
            ThrowIfDisposed();
            if (!_connections.TryGetValue(role, out var connection)) return false;

            _connections.Remove(role);
            RetireEntry(role, connection, dispose);
            _leaseEntries.Remove(role);

            return true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var pair in _connections)
            {
                if (_leaseEntries.TryGetValue(pair.Key, out var entry))
                    entry.Disposed = true;
                pair.Value.Dispose();
            }

            _connections.Clear();
            _leaseEntries.Clear();
        }

        private void Release(AbilityKitConnectionRole role, ConnectionLeaseEntry entry, bool owner)
        {
            var isCurrent = _leaseEntries.TryGetValue(role, out var current) && ReferenceEquals(current, entry);
            if (owner) entry.Retiring = true;
            entry.LeaseCount--;
            if (!entry.Retiring || entry.LeaseCount != 0) return;
            if (isCurrent)
            {
                _leaseEntries.Remove(role);
                _connections.Remove(role);
            }

            if (entry.Disposed || entry.SuppressDisposal) return;
            entry.Disposed = true;
            entry.Connection.Dispose();
        }

        private void RetireEntry(AbilityKitConnectionRole role, IConnection connection, bool dispose)
        {
            if (_leaseEntries.TryGetValue(role, out var entry) && entry.LeaseCount > 0)
            {
                entry.Retiring = true;
                entry.SuppressDisposal = !dispose;
                return;
            }

            if (dispose) connection.Dispose();
        }

        private static bool SameEndpoint(AbilityKitConnectionDescriptor left, AbilityKitConnectionDescriptor right) =>
            left.Role == right.Role && left.Port == right.Port &&
            string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(left.TransportName, right.TransportName, StringComparison.OrdinalIgnoreCase);

        private sealed class ConnectionLeaseEntry
        {
            internal readonly IConnection Connection;
            internal readonly AbilityKitConnectionDescriptor? Descriptor;
            internal int LeaseCount;
            internal bool Retiring;
            internal bool Disposed;
            internal bool SuppressDisposal;

            internal ConnectionLeaseEntry(IConnection connection, AbilityKitConnectionDescriptor? descriptor)
            {
                Connection = connection;
                Descriptor = descriptor;
            }
        }

        private sealed class ConnectionLease : IAbilityKitConnectionLease
        {
            private AbilityKitConnectionRegistry _registry;
            private readonly AbilityKitConnectionRole _role;
            private readonly ConnectionLeaseEntry _entry;

            internal ConnectionLease(AbilityKitConnectionRegistry registry, AbilityKitConnectionRole role, ConnectionLeaseEntry entry, bool ownsLifecycle)
            {
                _registry = registry;
                _role = role;
                _entry = entry;
                OwnsLifecycle = ownsLifecycle;
            }

            public IConnection Connection => _entry.Connection;
            public bool OwnsLifecycle { get; }

            public void Dispose()
            {
                var registry = _registry;
                if (registry == null) return;
                _registry = null;
                registry.Release(_role, _entry, OwnsLifecycle);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AbilityKitConnectionRegistry));
        }
    }
}
