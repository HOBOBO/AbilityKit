using System;
using System.Threading;

namespace AbilityKit.Core.Lifetime
{
    /// <summary>
    /// Creates disposable registrations whose release callback runs at most once.
    /// </summary>
    public static class DisposableRegistration
    {
        /// <summary>
        /// Creates a registration that invokes <paramref name="release"/> at most once.
        /// </summary>
        public static IDisposable Create(Action release)
        {
            if (release == null) throw new ArgumentNullException(nameof(release));
            return new CallbackRegistration(release);
        }

        /// <summary>
        /// Creates a registration that passes <paramref name="state"/> to
        /// <paramref name="release"/> at most once without requiring a capturing closure.
        /// </summary>
        public static IDisposable Create<TState>(TState state, Action<TState> release)
        {
            if (release == null) throw new ArgumentNullException(nameof(release));
            return new StateRegistration<TState>(state, release);
        }

        private sealed class CallbackRegistration : IDisposable
        {
            private Action? _release;

            public CallbackRegistration(Action release)
            {
                _release = release;
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref _release, null)?.Invoke();
            }
        }

        private sealed class StateRegistration<TState> : IDisposable
        {
            private TState _state;
            private Action<TState>? _release;

            public StateRegistration(TState state, Action<TState> release)
            {
                _state = state;
                _release = release;
            }

            public void Dispose()
            {
                var release = Interlocked.Exchange(ref _release, null);
                if (release == null) return;

                var state = _state;
                _state = default!;
                release(state);
            }
        }
    }
}
