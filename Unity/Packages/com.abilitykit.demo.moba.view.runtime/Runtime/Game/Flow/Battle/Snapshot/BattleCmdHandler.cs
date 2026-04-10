using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Common.Log;
using AbilityKit.Core.Common.SnapshotRouting;
using AbilityKit.Game.Flow;

namespace AbilityKit.Game.Flow.Snapshot
{
    public sealed class BattleCmdHandler : IDisposable, ISnapshotCmdHandlerRegistry
    {
        private readonly BattleContext _ctx;
        private readonly FrameSnapshotDispatcher _dispatcher;
        private readonly Dictionary<int, IDisposable> _subscriptions = new Dictionary<int, IDisposable>();

        public BattleCmdHandler(BattleContext ctx, FrameSnapshotDispatcher dispatcher)
        {
            _ctx = ctx;
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public void Dispose()
        {
            foreach (var kv in _subscriptions)
            {
                try
                {
                    kv.Value?.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Exception(ex);
                }
            }
            _subscriptions.Clear();
        }

        public void Register<T>(int opCode, Action<BattleContext, ISnapshotEnvelope, T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            if (_subscriptions.ContainsKey(opCode))
            {
                throw new InvalidOperationException($"CmdHandler already registered: opCode={opCode}");
            }

            var sub = _dispatcher.Subscribe<T>(opCode, (packet, payload) =>
            {
                handler(_ctx, packet, payload);
            });

            _subscriptions[opCode] = sub;
        }

        void ISnapshotCmdHandlerRegistry.RegisterCmdHandler<T>(int opCode, Action<object, ISnapshotEnvelope, T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            Register<T>(opCode, (ctx, packet, payload) => handler(ctx, packet, payload));
        }
    }
}
