#nullable enable annotations

using System;
using System.Runtime.ExceptionServices;
using AbilityKit.Ability.Flow.Pooling;

namespace AbilityKit.Ability.Flow
{
    /// <summary>
    /// 持有一个池化 Runner，向宿主暴露流程推进、通知和释放入口。
    /// </summary>
    public sealed class FlowSession : IDisposable
    {
        private FlowRunner _runner;
        private bool _starting;
        private bool _disposed;

        /// <summary>
        /// 请求启动时同步通知，先于 Runner 停止旧运行和启动新运行。
        /// </summary>
        public event Action Started;

        /// <summary>
        /// 同步通知运行状态从旧值切换到新值。
        /// </summary>
        public event Action<FlowStatus, FlowStatus> StatusChanged;

        /// <summary>
        /// 同步通知运行已收尾，参数为最终状态。
        /// </summary>
        public event Action<FlowStatus> Finished;

        /// <summary>
        /// 转发 Runner 执行期间报告的异常。
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

        internal FlowSession(bool deferRent)
        {
            _disposed = true;
        }

        /// <summary>
        /// 创建 Session 并租借一个 Runner。
        /// </summary>
        public FlowSession()
        {
            ResetForRent(FlowPools.RentRunner());
        }

        /// <summary>
        /// 获取当前借用的上下文；不得在 Session 释放后继续使用。
        /// </summary>
        public FlowContext Context
        {
            get
            {
                ThrowIfDisposed();
                return _runner.Context;
            }
        }

        /// <summary>
        /// 获取当前运行状态；Session 释放后不可读取。
        /// </summary>
        public FlowStatus Status
        {
            get
            {
                ThrowIfDisposed();
                return _runner.Status;
            }
        }

        /// <summary>
        /// 先派发 Started，再同步启动根节点。流程回调不得重入生命周期操作。
        /// </summary>
        public void Start(IFlowNode root)
        {
            ThrowIfDisposed();
            EnsureLifecycleIdle();
            _starting = true;
            try
            {
                _runner.Observer = Observer;
                _runner.TraceRecorder = TraceRecorder;
                Started?.Invoke();
                _runner.Start(
                    root,
                    onFinished: status => Finished?.Invoke(status),
                    onStatusChanged: (previous, next) => StatusChanged?.Invoke(previous, next));
            }
            finally
            {
                _starting = false;
            }
        }

        /// <summary>
        /// 按指定秒数同步推进当前运行，返回推进后的状态。
        /// </summary>
        public FlowStatus Step(float deltaTime)
        {
            ThrowIfDisposed();
            return _runner.Step(deltaTime);
        }

        /// <summary>
        /// 中断当前运行并派发终态通知，保留 Session 供后续启动。
        /// </summary>
        public void Stop()
        {
            ThrowIfDisposed();
            EnsureLifecycleIdle();
            _runner.Stop();
        }

        /// <summary>
        /// 清除订阅并归还 Runner，不派发释放引起的终态通知；中断异常在归还完成后抛出。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            ResetForRelease()?.Throw();
        }

        internal void ResetForRent(FlowRunner runner)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _runner.UnhandledException += OnRunnerUnhandledException;
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
            _runner.UnhandledException -= OnRunnerUnhandledException;
            try
            {
                return FlowPools.ReleaseRunnerCore(_runner);
            }
            finally
            {
                _runner = null;
                _disposed = true;
            }
        }

        internal void EnsureLifecycleIdle()
        {
            if (_starting)
            {
                throw new InvalidOperationException("A flow callback cannot change the lifecycle of its starting session.");
            }

            _runner?.EnsureLifecycleIdle();
        }

        private void OnRunnerUnhandledException(Exception ex)
        {
            UnhandledException?.Invoke(ex);
        }

        private void ResetCallbacks()
        {
            Started = null;
            StatusChanged = null;
            Finished = null;
            UnhandledException = null;
            Observer = null;
            TraceRecorder = null;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FlowSession));
        }
    }
}
