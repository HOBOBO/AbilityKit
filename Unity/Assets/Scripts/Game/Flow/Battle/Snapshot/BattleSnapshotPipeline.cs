using System;
using System.Collections.Generic;
using AbilityKit.Ability.Server;
using AbilityKit.Game.Flow;

namespace AbilityKit.Game.Flow.Snapshot
{
    public sealed class BattleSnapshotPipeline : IDisposable
    {
        private readonly BattleContext _ctx;
        private readonly FrameSnapshotDispatcher _dispatcher;

        private readonly Dictionary<int, IRoute> _routes = new Dictionary<int, IRoute>();

        public BattleSnapshotPipeline(BattleContext ctx, FrameSnapshotDispatcher dispatcher)
        {
            _ctx = ctx;
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _dispatcher.SnapshotReceived += OnSnapshot;
        }

        public void Dispose()
        {
            try
            {
                _dispatcher.SnapshotReceived -= OnSnapshot;
            }
            catch
            {
            }
        }

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

                throw new InvalidOperationException($"Pipeline route type mismatch: opCode={opCode} existing={existing.PayloadType.FullName} new={typeof(T).FullName}");
            }

            _routes[opCode] = new Route<T>(decoder);
        }

        public IDisposable AddStage<T>(int opCode, int order, Action<BattleContext, FramePacket, T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            if (!_routes.TryGetValue(opCode, out var raw))
            {
                throw new InvalidOperationException($"Pipeline route not registered: opCode={opCode} type={typeof(T).FullName}");
            }

            if (raw is not Route<T> route)
            {
                throw new InvalidOperationException($"Pipeline route type mismatch: opCode={opCode} expected={typeof(T).FullName} actual={raw.PayloadType.FullName}");
            }

            var stage = new Stage<T>(order, handler);
            route.Add(stage);
            return new Subscription(() => route.Remove(stage));
        }

        private void OnSnapshot(FramePacket packet, WorldStateSnapshot snap)
        {
            if (_routes.TryGetValue(snap.OpCode, out var route) && route != null)
            {
                route.Dispatch(_ctx, packet, in snap);
            }
        }

        private interface IRoute
        {
            Type PayloadType { get; }
            void Dispatch(BattleContext ctx, FramePacket packet, in WorldStateSnapshot snap);
        }

        private sealed class Route<T> : IRoute
        {
            private readonly List<Stage<T>> _stages = new List<Stage<T>>(4);

            public Route(TryDecode<T> decoder)
            {
                Decoder = decoder;
            }

            public TryDecode<T> Decoder { get; set; }
            public Type PayloadType => typeof(T);

            public void Add(Stage<T> stage)
            {
                int idx = _stages.Count;
                for (int i = 0; i < _stages.Count; i++)
                {
                    if (stage.Order < _stages[i].Order)
                    {
                        idx = i;
                        break;
                    }
                }
                _stages.Insert(idx, stage);
            }

            public void Remove(Stage<T> stage)
            {
                _stages.Remove(stage);
            }

            public void Dispatch(BattleContext ctx, FramePacket packet, in WorldStateSnapshot snap)
            {
                if (_stages.Count == 0) return;
                if (Decoder == null) return;
                if (!Decoder(in snap, out var payload)) return;

                for (int i = 0; i < _stages.Count; i++)
                {
                    var s = _stages[i];
                    try
                    {
                        s.Handler?.Invoke(ctx, packet, payload);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private readonly struct Stage<T>
        {
            public readonly int Order;
            public readonly Action<BattleContext, FramePacket, T> Handler;

            public Stage(int order, Action<BattleContext, FramePacket, T> handler)
            {
                Order = order;
                Handler = handler;
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
