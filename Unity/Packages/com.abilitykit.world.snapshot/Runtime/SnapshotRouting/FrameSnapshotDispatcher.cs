using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host;

namespace AbilityKit.Ability.Share.Common.SnapshotRouting
{
    /// <summary>
    /// Dispatches snapshot envelopes by opCode.
    ///
    /// Notes:
    /// - This type is transport/session agnostic. Call <see cref="Feed"/> to push envelopes into it.
    /// - Decoding is registered per opCode, and handlers subscribe to typed payloads.
    /// </summary>
    public sealed class FrameSnapshotDispatcher : IDisposable, ISnapshotDecoderRegistry
    {
        private readonly Dictionary<int, IRoute> _routes = new Dictionary<int, IRoute>();

        public event Action<ISnapshotEnvelope> FrameReceived;
        public event Action<ISnapshotEnvelope, WorldStateSnapshot> SnapshotReceived;

        public delegate bool TryDecode<T>(in WorldStateSnapshot snap, out T value);

        void ISnapshotDecoderRegistry.RegisterDecoder<T>(int opCode, ISnapshotDecoderRegistry.TryDecode<T> decoder)
        {
            if (decoder == null) throw new ArgumentNullException(nameof(decoder));
            Register<T>(opCode, (in WorldStateSnapshot snap, out T value) => decoder(in snap, out value));
        }

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

        public IDisposable Subscribe<T>(int opCode, Action<ISnapshotEnvelope, T> handler)
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
            // No external subscription to cleanup. Keep for symmetry.
        }

        public void Feed(ISnapshotEnvelope envelope)
        {
            if (envelope == null) return;
            OnEnvelope(envelope);
        }

        private void OnEnvelope(ISnapshotEnvelope envelope)
        {
            FrameReceived?.Invoke(envelope);

            if (!envelope.Snapshot.HasValue) return;
            var snap = envelope.Snapshot.Value;

            SnapshotReceived?.Invoke(envelope, snap);

            if (_routes.TryGetValue(snap.OpCode, out var route) && route != null)
            {
                route.Dispatch(envelope, in snap);
            }
        }

        private interface IRoute
        {
            Type PayloadType { get; }
            void Dispatch(ISnapshotEnvelope envelope, in WorldStateSnapshot snap);
        }

        private sealed class Route<T> : IRoute
        {
            private readonly List<Action<ISnapshotEnvelope, T>> _handlers = new List<Action<ISnapshotEnvelope, T>>(4);

            public Route(TryDecode<T> decoder)
            {
                Decoder = decoder;
            }

            public TryDecode<T> Decoder { get; set; }
            public Type PayloadType => typeof(T);

            public void Add(Action<ISnapshotEnvelope, T> handler)
            {
                _handlers.Add(handler);
            }

            public void Remove(Action<ISnapshotEnvelope, T> handler)
            {
                _handlers.Remove(handler);
            }

            public void Dispatch(ISnapshotEnvelope envelope, in WorldStateSnapshot snap)
            {
                if (_handlers.Count == 0) return;

                if (Decoder == null) return;
                if (!Decoder(in snap, out var payload)) return;

                for (int i = 0; i < _handlers.Count; i++)
                {
                    var h = _handlers[i];
                    try
                    {
                        h?.Invoke(envelope, payload);
                    }
                    catch (Exception ex)
                    {
                        AbilityKit.Ability.Share.Common.Log.Log.Exception(ex);
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
