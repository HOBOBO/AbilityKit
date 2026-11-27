#nullable enable annotations

using System;
using System.Runtime.ExceptionServices;
using AbilityKit.Ability.Flow.Pooling;

namespace AbilityKit.Ability.Flow
{
    /// <summary>
    /// 通过根节点提供器构建流程，持有并转发底层 Session 的运行通知。
    /// </summary>
    public sealed class FlowHost<TArgs> : IDisposable
    {
        private IFlowRootProvider<TArgs> _provider;
        private FlowSession _session;
        private bool _starting;
        private bool _disposed;

        /// <summary>
        /// 根节点创建后、Runner 启动前同步通知。
        /// </summary>
        public event Action Started;

        /// <summary>
        /// 同步转发运行状态从旧值到新值的变化。
        /// </summary>
        public event Action<FlowStatus, FlowStatus> StatusChanged;

        /// <summary>
        /// 同步转发当前运行的完成通知，参数为最终状态。
        /// </summary>
        public event Action<FlowStatus> Finished;

        /// <summary>
        /// 转发 Runner 执行期间报告的异常；不处理提供器抛出的异常。
        /// </summary>
        public event Action<Exception> UnhandledException;

        /// <summary>
        /// 指定下一次运行使用的诊断观察器。
        /// </summary>
        public IFlowObserver Observer { get; set; }

        /// <summary>
        /// 指定下一次运行使用的跟踪记录器。
        /// </summary>
        public IFlowTraceRecorder TraceRecorder { get; set; }

        internal FlowHost(bool deferRent)
        {
            _disposed = true;
        }

        /// <summary>
        /// 使用指定提供器创建宿主，并持有一个池化 Session。
        /// </summary>
        public FlowHost(IFlowRootProvider<TArgs> provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            ResetForRent(provider, FlowPools.RentSession());
        }

        /// <summary>
        /// 获取底层 Session 的运行状态；Host 释放后不可读取。
        /// </summary>
        public FlowStatus Status
        {
            get
            {
                ThrowIfDisposed();
                return _session.Status;
            }
        }

        /// <summary>
        /// 获取底层 Session 的上下文；不得在 Host 释放后继续使用。
        /// </summary>
        public FlowContext Context
        {
            get
            {
                ThrowIfDisposed();
                return _session.Context;
            }
        }

        /// <summary>
        /// 同步创建并启动根节点；提供器及流程回调不得重入生命周期操作。
        /// </summary>
        public void Start(TArgs args)
        {
            ThrowIfDisposed();
            EnsureLifecycleIdle();
            _starting = true;
            try
            {
                var root = _provider.CreateRoot(args);
                _session.Observer = Observer;
                _session.TraceRecorder = TraceRecorder;
                _session.Start(root);
            }
            finally
            {
                _starting = false;
            }
        }

        internal FlowStatus Step(float deltaTime)
        {
            ThrowIfDisposed();
            return _session.Step(deltaTime);
        }

        /// <summary>
        /// 中断当前运行并派发终态通知，保留 Host 和提供器供后续启动。
        /// </summary>
        public void Stop()
        {
            ThrowIfDisposed();
            EnsureLifecycleIdle();
            _session.Stop();
        }

        /// <summary>
        /// 清除订阅并归还 Session，不派发释放引起的终态通知；中断异常在归还完成后抛出。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            ResetForRelease()?.Throw();
        }

        internal void ResetForRent(IFlowRootProvider<TArgs> provider, FlowSession session)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _session.Started += OnSessionStarted;
            _session.StatusChanged += OnSessionStatusChanged;
            _session.Finished += OnSessionFinished;
            _session.UnhandledException += OnSessionUnhandledException;
            _disposed = false;
        }

        internal ExceptionDispatchInfo? ResetForRelease()
        {
            if (_disposed)
            {
                ResetCallbacks();
                return null;
            }

            EnsureLifecycleIdle();
            ResetCallbacks();
            try
            {
                return FlowPools.ReleaseSessionCore(_session);
            }
            finally
            {
                _session = null;
                _provider = null;
                _disposed = true;
            }
        }

        private void OnSessionStarted()
        {
            Started?.Invoke();
        }

        private void OnSessionStatusChanged(FlowStatus previous, FlowStatus next)
        {
            StatusChanged?.Invoke(previous, next);
        }

        private void OnSessionFinished(FlowStatus status)
        {
            Finished?.Invoke(status);
        }

        private void OnSessionUnhandledException(Exception ex)
        {
            UnhandledException?.Invoke(ex);
        }

        private void ResetCallbacks()
        {
            if (_session != null)
            {
                _session.Started -= OnSessionStarted;
                _session.StatusChanged -= OnSessionStatusChanged;
                _session.Finished -= OnSessionFinished;
                _session.UnhandledException -= OnSessionUnhandledException;
            }

            Started = null;
            StatusChanged = null;
            Finished = null;
            UnhandledException = null;
            Observer = null;
            TraceRecorder = null;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FlowHost<TArgs>));
        }

        private void EnsureLifecycleIdle()
        {
            if (_starting)
            {
                throw new InvalidOperationException("A flow callback cannot change the lifecycle of its starting host.");
            }

            _session.EnsureLifecycleIdle();
        }
    }
}
