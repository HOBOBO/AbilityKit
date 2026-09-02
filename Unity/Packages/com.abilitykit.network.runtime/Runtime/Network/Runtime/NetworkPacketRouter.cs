using System;
using System.Collections.Generic;
using AbilityKit.Network.Protocol;

namespace AbilityKit.Network.Runtime
{
    public enum NetworkPacketDispatchKind
    {
        Request = 0,
        Response = 1,
        ServerPush = 2,
        Unknown = 3
    }

    public enum NetworkPacketDispatchOutcome
    {
        NoRoute = 0,
        Handled = 1,
        BoundaryRejected = 2
    }

    public readonly struct NetworkPacketDispatch
    {
        public NetworkPacketDispatch(NetworkPacketDispatchKind kind, NetworkPacketHeader header, ArraySegment<byte> payload)
        {
            Kind = kind;
            Header = header;
            Payload = payload;
        }

        public NetworkPacketDispatchKind Kind { get; }
        public NetworkPacketHeader Header { get; }
        public ArraySegment<byte> Payload { get; }
    }

    public readonly struct NetworkPacketRouteSnapshot
    {
        internal NetworkPacketRouteSnapshot(
            uint opCode,
            NetworkPacketDispatchKind kind,
            int handlerCount,
            long dispatchCount,
            long handledCount,
            long unknownCount,
            long exceptionCount,
            long lastDispatchUnixTimeMilliseconds)
        {
            OpCode = opCode;
            Kind = kind;
            HandlerCount = handlerCount;
            DispatchCount = dispatchCount;
            HandledCount = handledCount;
            UnknownCount = unknownCount;
            ExceptionCount = exceptionCount;
            LastDispatchUnixTimeMilliseconds = lastDispatchUnixTimeMilliseconds;
        }

        public uint OpCode { get; }
        public NetworkPacketDispatchKind Kind { get; }
        public int HandlerCount { get; }
        public long DispatchCount { get; }
        public long HandledCount { get; }
        public long UnknownCount { get; }
        public long ExceptionCount { get; }
        public long LastDispatchUnixTimeMilliseconds { get; }
    }

    public readonly struct NetworkPacketRouterSnapshot
    {
        internal NetworkPacketRouterSnapshot(
            IReadOnlyList<NetworkPacketRouteSnapshot> routes,
            long dispatchedCount,
            long handledCount,
            long unknownCount,
            long exceptionCount,
            long boundaryRejectedCount)
        {
            Routes = routes;
            DispatchedCount = dispatchedCount;
            HandledCount = handledCount;
            UnknownCount = unknownCount;
            ExceptionCount = exceptionCount;
            BoundaryRejectedCount = boundaryRejectedCount;
        }

        public IReadOnlyList<NetworkPacketRouteSnapshot> Routes { get; }
        public long DispatchedCount { get; }
        public long HandledCount { get; }
        public long UnknownCount { get; }
        public long ExceptionCount { get; }
        public long BoundaryRejectedCount { get; }
    }

    public delegate void NetworkPacketRouteHandler(NetworkPacketDispatch dispatch);

    /// <summary>
    /// Optional inbound boundary check. Returning false prevents route handlers from seeing the
    /// packet. The callback must be allocation-free and must not throw; exceptions are treated as
    /// rejected packets and reported through the router exception callback.
    /// </summary>
    public delegate bool NetworkPacketBoundaryValidator(
        NetworkPacketHeader header,
        ArraySegment<byte> payload);

    /// <summary>
    /// Framework-level opcode router. It is deliberately independent from Catalog and business protocol types.
    /// Legacy connection events remain responsible for compatibility while registered handlers receive a unified dispatch.
    /// </summary>
    public sealed class NetworkPacketRouter
    {
        private sealed class Route
        {
            public readonly List<NetworkPacketRouteHandler> Handlers = new List<NetworkPacketRouteHandler>();
            public long DispatchCount;
            public long HandledCount;
            public long UnknownCount;
            public long ExceptionCount;
            public long LastDispatchUnixTimeMilliseconds;
        }

        private readonly object _gate = new object();
        private readonly Dictionary<(uint OpCode, NetworkPacketDispatchKind Kind), Route> _routes =
            new Dictionary<(uint OpCode, NetworkPacketDispatchKind Kind), Route>();
        private readonly Action<Exception> _exceptionHandler;
        private readonly NetworkPacketBoundaryValidator _boundaryValidator;
        private long _dispatchedCount;
        private long _handledCount;
        private long _unknownCount;
        private long _exceptionCount;
        private long _boundaryRejectedCount;

        public NetworkPacketRouter(
            Action<Exception> exceptionHandler = null,
            NetworkPacketBoundaryValidator boundaryValidator = null)
        {
            _exceptionHandler = exceptionHandler;
            _boundaryValidator = boundaryValidator;
        }

        public void Register(uint opCode, NetworkPacketDispatchKind kind, NetworkPacketRouteHandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (kind == NetworkPacketDispatchKind.Unknown)
                throw new ArgumentException("Unknown is not a registerable route kind.", nameof(kind));

            lock (_gate)
            {
                var key = (opCode, kind);
                if (!_routes.TryGetValue(key, out var route))
                {
                    route = new Route();
                    _routes.Add(key, route);
                }

                if (!route.Handlers.Contains(handler)) route.Handlers.Add(handler);
            }
        }

        public bool Unregister(uint opCode, NetworkPacketDispatchKind kind, NetworkPacketRouteHandler handler)
        {
            if (handler == null) return false;
            lock (_gate)
            {
                if (!_routes.TryGetValue((opCode, kind), out var route)) return false;
                var removed = route.Handlers.Remove(handler);
                if (route.Handlers.Count == 0 && route.DispatchCount == 0)
                    _routes.Remove((opCode, kind));
                return removed;
            }
        }

        public bool Dispatch(NetworkPacketHeader header, ArraySegment<byte> payload)
        {
            return DispatchDetailed(header, payload) == NetworkPacketDispatchOutcome.Handled;
        }

        /// <summary>Dispatches and preserves the reason a packet was not delivered.</summary>
        public NetworkPacketDispatchOutcome DispatchDetailed(
            NetworkPacketHeader header,
            ArraySegment<byte> payload)
        {
            lock (_gate) _dispatchedCount++;

            if (_boundaryValidator != null)
            {
                bool accepted;
                try
                {
                    accepted = _boundaryValidator(header, payload);
                }
                catch (Exception exception)
                {
                    lock (_gate) _exceptionCount++;
                    lock (_gate) _boundaryRejectedCount++;
                    try { _exceptionHandler?.Invoke(exception); }
                    catch { }
                    return NetworkPacketDispatchOutcome.BoundaryRejected;
                }

                if (!accepted)
                {
                    lock (_gate) _unknownCount++;
                    lock (_gate) _boundaryRejectedCount++;
                    return NetworkPacketDispatchOutcome.BoundaryRejected;
                }
            }

            var kind = ResolveKind(header.Flags);
            NetworkPacketRouteHandler[] handlers;
            Route route;

            lock (_gate)
            {
                if (!_routes.TryGetValue((header.OpCode, kind), out route))
                {
                    _unknownCount++;
                    return NetworkPacketDispatchOutcome.NoRoute;
                }

                route.DispatchCount++;
                route.LastDispatchUnixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                handlers = route.Handlers.ToArray();
                if (handlers.Length == 0)
                {
                    route.UnknownCount++;
                    _unknownCount++;
                    return NetworkPacketDispatchOutcome.NoRoute;
                }
            }

            var dispatch = new NetworkPacketDispatch(kind, header, payload);
            var handled = false;
            for (var i = 0; i < handlers.Length; i++)
            {
                try
                {
                    handlers[i](dispatch);
                    handled = true;
                }
                catch (Exception exception)
                {
                    lock (_gate)
                    {
                        route.ExceptionCount++;
                        _exceptionCount++;
                    }
                    try { _exceptionHandler?.Invoke(exception); }
                    catch { }
                }
            }

            if (handled)
            {
                lock (_gate)
                {
                    route.HandledCount++;
                    _handledCount++;
                }
            }
            return handled
                ? NetworkPacketDispatchOutcome.Handled
                : NetworkPacketDispatchOutcome.NoRoute;
        }

        public NetworkPacketRouterSnapshot GetSnapshot()
        {
            lock (_gate)
            {
                var routes = new List<NetworkPacketRouteSnapshot>(_routes.Count);
                foreach (var pair in _routes)
                {
                    var route = pair.Value;
                    routes.Add(new NetworkPacketRouteSnapshot(
                        pair.Key.OpCode,
                        pair.Key.Kind,
                        route.Handlers.Count,
                        route.DispatchCount,
                        route.HandledCount,
                        route.UnknownCount,
                        route.ExceptionCount,
                        route.LastDispatchUnixTimeMilliseconds));
                }
                return new NetworkPacketRouterSnapshot(
                    routes.AsReadOnly(),
                    _dispatchedCount,
                    _handledCount,
                    _unknownCount,
                    _exceptionCount,
                    _boundaryRejectedCount);
            }
        }

        public static NetworkPacketDispatchKind ResolveKind(NetworkPacketFlags flags)
        {
            if ((flags & NetworkPacketFlags.ServerPush) != 0) return NetworkPacketDispatchKind.ServerPush;
            if ((flags & NetworkPacketFlags.Response) != 0) return NetworkPacketDispatchKind.Response;
            if ((flags & NetworkPacketFlags.Request) != 0) return NetworkPacketDispatchKind.Request;
            return NetworkPacketDispatchKind.Unknown;
        }
    }
}
