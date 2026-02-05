using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host;

namespace AbilityKit.Ability.Share.Common.SnapshotRouting
{
    public sealed class SnapshotCmdHandler : IDisposable, ISnapshotCmdHandlerRegistry
    {
        private readonly object _ctx;
        private readonly FrameSnapshotDispatcher _dispatcher;
        private readonly Dictionary<int, IDisposable> _subscriptions = new Dictionary<int, IDisposable>();

        public SnapshotCmdHandler(object ctx, FrameSnapshotDispatcher dispatcher)
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
                    AbilityKit.Ability.Share.Common.Log.Log.Exception(ex);
                }
            }
            _subscriptions.Clear();
        }

        public void Register<T>(int opCode, Action<object, ISnapshotEnvelope, T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            if (_subscriptions.ContainsKey(opCode))
            {
                throw new InvalidOperationException($"CmdHandler already registered: opCode={opCode}");
            }

            var sub = _dispatcher.Subscribe<T>(opCode, (envelope, payload) =>
            {
                handler(_ctx, envelope, payload);
            });

            _subscriptions[opCode] = sub;
        }

        void ISnapshotCmdHandlerRegistry.RegisterCmdHandler<T>(int opCode, Action<object, ISnapshotEnvelope, T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            Register<T>(opCode, handler);
        }
    }
}
