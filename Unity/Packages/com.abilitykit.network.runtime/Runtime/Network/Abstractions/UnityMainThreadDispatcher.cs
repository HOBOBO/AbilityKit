using System;
using System.Threading;

namespace AbilityKit.Network.Abstractions
{
    public sealed class UnityMainThreadDispatcher : IDispatcher
    {
        private readonly SynchronizationContext _sync;

        public UnityMainThreadDispatcher(SynchronizationContext sync)
        {
            _sync = sync ?? throw new ArgumentNullException(nameof(sync));
        }

        public void Post(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            _sync.Post(_ => action.Invoke(), null);
        }

        public static UnityMainThreadDispatcher CaptureCurrent()
        {
            var sync = SynchronizationContext.Current;
            if (sync == null)
            {
                throw new InvalidOperationException("SynchronizationContext.Current is null. Capture must be called on Unity main thread.");
            }
            return new UnityMainThreadDispatcher(sync);
        }
    }
}
