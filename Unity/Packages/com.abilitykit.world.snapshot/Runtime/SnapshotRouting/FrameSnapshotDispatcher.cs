using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Lifetime;

namespace AbilityKit.Core.Snapshots.Routing
{
    /// <summary>
    /// 按 opCode 派发快照信封。
    ///
    /// 说明：
    /// - 该类型与传输层和会话无关，调用 <see cref="Feed"/> 将信封推入派发器。
    /// - 解码器按 opCode 注册，处理器订阅类型化载荷。
    /// </summary>
    public sealed class FrameSnapshotDispatcher : IDisposable, ISnapshotDecoderRegistry, ISnapshotDispatcher
    {
        private readonly Dictionary<int, IRoute> _routes = new Dictionary<int, IRoute>();

        public event Action<ISnapshotEnvelope> FrameReceived;
        public event Action<ISnapshotEnvelope, WorldStateSnapshot> SnapshotReceived;
        /// <summary>当收到未注册路由的 OpCode 快照时触发（用于诊断 OpCode 漂移）。</summary>
        public event Action<int> NoRouteForOpCode;

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

        /// <summary>
        /// 为已注册的路由追加类型化回调；相同回调可独立注册多次，按注册顺序执行。
        /// </summary>
        /// <returns>仅撤销本次注册的令牌，可重复释放。</returns>
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

            var registration = new HandlerRegistration<T>(route, handler);
            route.Add(registration);
            return DisposableRegistration.Create(
                registration,
                static entry => entry.Route.Remove(entry));
        }

        public void Dispose()
        {
            // 没有需要清理的外部订阅；保留该方法以保持生命周期对称。
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
            else
            {
                NoRouteForOpCode?.Invoke(snap.OpCode);
            }
        }

        private interface IRoute
        {
            Type PayloadType { get; }
            void Dispatch(ISnapshotEnvelope envelope, in WorldStateSnapshot snap);
        }

        private sealed class Route<T> : IRoute
        {
            private readonly List<HandlerRegistration<T>> _handlers = new List<HandlerRegistration<T>>(4);

            public Route(TryDecode<T> decoder)
            {
                Decoder = decoder;
            }

            public TryDecode<T> Decoder { get; set; }
            public Type PayloadType => typeof(T);

            public void Add(HandlerRegistration<T> registration)
            {
                _handlers.Add(registration);
            }

            public void Remove(HandlerRegistration<T> registration)
            {
                _handlers.Remove(registration);
            }

            public void Dispatch(ISnapshotEnvelope envelope, in WorldStateSnapshot snap)
            {
                if (_handlers.Count == 0) return;

                if (Decoder == null) return;
                if (!Decoder(in snap, out var payload)) return;

                for (int i = 0; i < _handlers.Count; i++)
                {
                    var handler = _handlers[i].Handler;
                    try
                    {
                        handler(envelope, payload);
                    }
                    catch (Exception ex)
                    {
                        AbilityKit.Core.Logging.Log.Exception(ex);
                    }
                }
            }
        }

        /// <summary>
        /// 一次订阅的独立标识，即使委托相等也不会混淆不同的注册。
        /// </summary>
        private sealed class HandlerRegistration<T>
        {
            public readonly Route<T> Route;
            public readonly Action<ISnapshotEnvelope, T> Handler;

            public HandlerRegistration(Route<T> route, Action<ISnapshotEnvelope, T> handler)
            {
                Route = route;
                Handler = handler;
            }
        }
    }
}
