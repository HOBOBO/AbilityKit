using System;
using System.Collections.Concurrent;
using System.Threading;
using AbilityKit.Ability.Share.Common.Log;

namespace AbilityKit.Network.Abstractions
{
    public sealed class DedicatedThreadDispatcher : IDispatcher, IDisposable
    {
        private readonly BlockingCollection<Action> _queue;
        private readonly Thread _thread;
        private bool _disposed;

        public DedicatedThreadDispatcher(string name = "NetworkThread")
        {
            _queue = new BlockingCollection<Action>(new ConcurrentQueue<Action>());
            _thread = new Thread(Run)
            {
                IsBackground = true,
                Name = name
            };
            _thread.Start();
        }

        public void Post(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (_disposed) throw new ObjectDisposedException(nameof(DedicatedThreadDispatcher));
            _queue.Add(action);
        }

        private void Run()
        {
            foreach (var a in _queue.GetConsumingEnumerable())
            {
                try
                {
                    a.Invoke();
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, "[DedicatedThreadDispatcher] Action threw");
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _queue.CompleteAdding();
        }
    }
}
