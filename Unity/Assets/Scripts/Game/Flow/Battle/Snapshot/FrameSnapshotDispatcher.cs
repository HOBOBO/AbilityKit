using System;
using System.Collections.Generic;
using AbilityKit.Ability.Server;
using AbilityKit.Game.Battle;

namespace AbilityKit.Game.Flow.Snapshot
{
    public sealed class FrameSnapshotDispatcher : IDisposable
    {
        private readonly BattleLogicSession _session;
        private readonly Dictionary<int, IRoute> _routes = new Dictionary<int, IRoute>();

        public FrameSnapshotDispatcher(BattleLogicSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _session.FrameReceived += OnFrame;
        }

        public event Action<FramePacket> FrameReceived;
        public event Action<FramePacket, WorldStateSnapshot> SnapshotReceived;

        public delegate bool TryDecode<T>(in WorldStateSnapshot snap, out T value);

        public void Register<T>(int opCode, TryDecode<T> decoder)
        {
            if (decoder == null) throw new ArgumentNullException(nameof(decoder));

            if (_routes.TryGetValue(opCode, out var existing))
            {
                if (existing is Route<T> typed)
                {
                    typed.Decoder = decoder;
                    return;
                }

                throw new InvalidOperationException($"Snapshot route type mismatch: opCode={opCode} existing={existing.PayloadType.FullName} new={typeof(T).FullName}");
            }

            _routes[opCode] = new Route<T>(decoder);
        }

        public IDisposable Subscribe<T>(int opCode, Action<FramePacket, T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            if (!_routes.TryGetValue(opCode, out var raw))
            {
                throw new InvalidOperationException($"Snapshot route not registered: opCode={opCode} type={typeof(T).FullName}");
            }

            if (raw is not Route<T> route)
            {
                throw new InvalidOperationException($"Snapshot route type mismatch: opCode={opCode} expected={typeof(T).FullName} actual={raw.PayloadType.FullName}");
            }

            route.Add(handler);
            return new Subscription(() => route.Remove(handler));
        }

        public void Dispose()
        {
            try
            {
                _session.FrameReceived -= OnFrame;
            }
            catch
            {
            }
        }

        private void OnFrame(FramePacket packet)
        {
            FrameReceived?.Invoke(packet);

            if (!packet.Snapshot.HasValue) return;
            var snap = packet.Snapshot.Value;

            SnapshotReceived?.Invoke(packet, snap);

            if (_routes.TryGetValue(snap.OpCode, out var route) && route != null)
            {
                route.Dispatch(packet, in snap);
            }
        }

        private interface IRoute
        {
            Type PayloadType { get; }
            void Dispatch(FramePacket packet, in WorldStateSnapshot snap);
        }

        private sealed class Route<T> : IRoute
        {
            private readonly List<Action<FramePacket, T>> _handlers = new List<Action<FramePacket, T>>(4);

            public Route(TryDecode<T> decoder)
            {
                Decoder = decoder;
            }

            public TryDecode<T> Decoder { get; set; }
            public Type PayloadType => typeof(T);

            public void Add(Action<FramePacket, T> handler)
            {
                _handlers.Add(handler);
            }

            public void Remove(Action<FramePacket, T> handler)
            {
                _handlers.Remove(handler);
            }

            public void Dispatch(FramePacket packet, in WorldStateSnapshot snap)
            {
                if (_handlers.Count == 0) return;

                if (Decoder == null) return;
                if (!Decoder(in snap, out var payload)) return;

                for (int i = 0; i < _handlers.Count; i++)
                {
                    var h = _handlers[i];
                    try
                    {
                        h?.Invoke(packet, payload);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private sealed class Subscription : IDisposable
        {
            private Action _dispose;

            public Subscription(Action dispose)
            {
                _dispose = dispose;
            }

            public void Dispose()
            {
                var d = _dispose;
                if (d == null) return;
                _dispose = null;
                d();
            }
        }
    }
}
