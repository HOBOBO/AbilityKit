using System;
using System.Collections.Generic;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Network.Runtime
{
    public sealed class NetworkPipeline
    {
        private readonly object _gate = new object();
        private readonly List<Entry> _middlewares = new List<Entry>();
        private int _processing;

        private sealed class Entry
        {
            public Entry(string? name, INetworkMiddleware middleware)
            {
                Name = name;
                Middleware = middleware;
            }

            public string? Name { get; }
            public INetworkMiddleware Middleware { get; set; }
        }

        public void Add(INetworkMiddleware middleware)
        {
            if (middleware == null) throw new ArgumentNullException(nameof(middleware));
            lock (_gate) { EnsureMutable(); _middlewares.Add(new Entry(null, middleware)); }
        }

        public void AddNamed(string name, INetworkMiddleware middleware)
        {
            ValidateName(name);
            if (middleware == null) throw new ArgumentNullException(nameof(middleware));
            lock (_gate) { EnsureMutable(); EnsureUniqueName(name); _middlewares.Add(new Entry(name, middleware)); }
        }

        public void AddBefore(string existingName, string name, INetworkMiddleware middleware)
        {
            ValidateName(name);
            lock (_gate)
            {
                EnsureMutable();
                var index = FindIndex(existingName);
                if (index < 0) throw new InvalidOperationException($"Middleware '{existingName}' is not registered.");
                EnsureUniqueName(name);
                if (middleware == null) throw new ArgumentNullException(nameof(middleware));
                _middlewares.Insert(index, new Entry(name, middleware));
            }
        }

        public void AddAfter(string existingName, string name, INetworkMiddleware middleware)
        {
            ValidateName(name);
            lock (_gate)
            {
                EnsureMutable();
                var index = FindIndex(existingName);
                if (index < 0) throw new InvalidOperationException($"Middleware '{existingName}' is not registered.");
                EnsureUniqueName(name);
                if (middleware == null) throw new ArgumentNullException(nameof(middleware));
                _middlewares.Insert(index + 1, new Entry(name, middleware));
            }
        }

        public bool Replace(string name, INetworkMiddleware middleware)
        {
            if (middleware == null) throw new ArgumentNullException(nameof(middleware));
            lock (_gate)
            {
                EnsureMutable();
                var index = FindIndex(name);
                if (index < 0) return false;
                _middlewares[index].Middleware = middleware;
                return true;
            }
        }

        public bool Replace(string existingName, string replacementName, INetworkMiddleware middleware)
        {
            ValidateName(replacementName);
            lock (_gate)
            {
                EnsureMutable();
                var index = FindIndex(existingName);
                if (index < 0) return false;
                if (!string.Equals(existingName, replacementName, StringComparison.Ordinal))
                    EnsureUniqueName(replacementName);
                if (middleware == null) throw new ArgumentNullException(nameof(middleware));
                _middlewares[index] = new Entry(replacementName, middleware);
                return true;
            }
        }

        public bool RemoveByName(string name)
        {
            lock (_gate)
            {
                EnsureMutable();
                var index = FindIndex(name);
                if (index < 0) return false;
                _middlewares.RemoveAt(index);
                return true;
            }
        }

        /// <summary>
        /// 将中间件插入管线首部。适用于需要先于协议中间件处理全部流量的工具。
        /// </summary>
        public void AddFirst(INetworkMiddleware middleware)
        {
            if (middleware == null) throw new ArgumentNullException(nameof(middleware));
            lock (_gate) { EnsureMutable(); _middlewares.Insert(0, new Entry(null, middleware)); }
        }

        public void AddFirst(string name, INetworkMiddleware middleware)
        {
            ValidateName(name);
            if (middleware == null) throw new ArgumentNullException(nameof(middleware));
            lock (_gate) { EnsureMutable(); EnsureUniqueName(name); _middlewares.Insert(0, new Entry(name, middleware)); }
        }

        /// <summary>
        /// 从管线中移除指定中间件。用于运行时动态卸载（例如禁用网络调理模拟）。
        /// 若中间件不存在则无操作。
        /// </summary>
        /// <param name="middleware">要移除的中间件实例。</param>
        /// <returns>是否成功移除。</returns>
        public bool Remove(INetworkMiddleware middleware)
        {
            if (middleware == null) return false;
            lock (_gate)
            {
                EnsureMutable();
                for (var i = 0; i < _middlewares.Count; i++)
                {
                    if (!ReferenceEquals(_middlewares[i].Middleware, middleware)) continue;
                    _middlewares.RemoveAt(i);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 当前管线中的中间件数量。
        /// </summary>
        public int Count { get { lock (_gate) return _middlewares.Count; } }

        public void ProcessInbound(ISessionContext context, NetworkPacketHeader header, ArraySegment<byte> payload, Action<NetworkPacketHeader, ArraySegment<byte>> terminal)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (terminal == null) throw new ArgumentNullException(nameof(terminal));

            var middlewares = SnapshotAndMarkProcessing();
            try { InvokeInbound(middlewares, 0, context, header, payload, terminal); }
            finally { EndProcessing(); }
        }

        public void ProcessOutbound(ISessionContext context, NetworkPacketHeader header, ArraySegment<byte> payload, Action<NetworkPacketHeader, ArraySegment<byte>> terminal)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (terminal == null) throw new ArgumentNullException(nameof(terminal));

            var middlewares = SnapshotAndMarkProcessing();
            try { InvokeOutbound(middlewares, 0, context, header, payload, terminal); }
            finally { EndProcessing(); }
        }

        private void InvokeInbound(Entry[] middlewares, int index, ISessionContext context, NetworkPacketHeader header, ArraySegment<byte> payload, Action<NetworkPacketHeader, ArraySegment<byte>> terminal)
        {
            if (index >= middlewares.Length)
            {
                terminal(header, payload);
                return;
            }

            middlewares[index].Middleware.OnInbound(context, header, payload, (h, p) => InvokeInbound(middlewares, index + 1, context, h, p, terminal));
        }

        private void InvokeOutbound(Entry[] middlewares, int index, ISessionContext context, NetworkPacketHeader header, ArraySegment<byte> payload, Action<NetworkPacketHeader, ArraySegment<byte>> terminal)
        {
            if (index >= middlewares.Length)
            {
                terminal(header, payload);
                return;
            }

            middlewares[index].Middleware.OnOutbound(context, header, payload, (h, p) => InvokeOutbound(middlewares, index + 1, context, h, p, terminal));
        }

        private Entry[] SnapshotAndMarkProcessing()
        {
            lock (_gate) { _processing++; return _middlewares.ToArray(); }
        }

        private void EndProcessing() { lock (_gate) { if (_processing > 0) _processing--; } }

        private void EnsureMutable()
        {
            if (_processing > 0) throw new InvalidOperationException("NetworkPipeline cannot be modified while processing a packet.");
        }

        private int FindIndex(string name)
        {
            ValidateName(name);
            for (var i = 0; i < _middlewares.Count; i++)
                if (string.Equals(_middlewares[i].Name, name, StringComparison.Ordinal)) return i;
            return -1;
        }

        private void EnsureUniqueName(string name)
        {
            ValidateName(name);
            if (FindIndex(name) >= 0) throw new InvalidOperationException($"Middleware '{name}' is already registered.");
        }

        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Middleware name is required.", nameof(name));
        }
    }
}
