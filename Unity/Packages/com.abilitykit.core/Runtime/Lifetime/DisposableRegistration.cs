using System;
using System.Threading;

namespace AbilityKit.Core.Lifetime
{
    /// <summary>
    /// 创建可释放的注册句柄，其释放回调最多执行一次。
    /// </summary>
    public static class DisposableRegistration
    {
        /// <summary>
        /// 创建最多调用一次 <paramref name="release"/> 的注册句柄。
        /// </summary>
        /// <param name="release">释放回调，不能是空引用。</param>
        /// <returns>由调用方持有并负责释放的注册句柄。</returns>
        /// <remarks>
        /// 支持并发释放；回调在取得唯一执行权后同步执行。回调异常向调用方传播，
        /// 但句柄仍视为已释放，后续释放不会重试回调。
        /// </remarks>
        public static IDisposable Create(Action release)
        {
            if (release == null) throw new ArgumentNullException(nameof(release));
            return new CallbackRegistration(release);
        }

        /// <summary>
        /// 创建携带状态的注册句柄，最多向释放回调传入一次状态，无需捕获闭包。
        /// </summary>
        /// <typeparam name="TState">释放回调所需的状态类型。</typeparam>
        /// <param name="state">句柄持有到释放时的状态。</param>
        /// <param name="release">接收状态的释放回调，不能是空引用。</param>
        /// <returns>由调用方持有并负责释放的注册句柄。</returns>
        /// <remarks>
        /// 支持并发释放，状态在唯一回调执行前从句柄移除。回调异常向调用方传播，
        /// 后续释放不重试；状态对象及其回调自身的线程安全由调用方负责。
        /// </remarks>
        public static IDisposable Create<TState>(TState state, Action<TState> release)
        {
            if (release == null) throw new ArgumentNullException(nameof(release));
            return new StateRegistration<TState>(state, release);
        }

        /// <summary>
        /// 通过原子撤销回调引用，实现不携带状态的单次释放句柄。
        /// </summary>
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

        /// <summary>
        /// 持有释放状态，通过原子取得回调执行权保证状态只交付一次。
        /// </summary>
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
