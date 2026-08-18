using System;
using AbilityKit.Ability.Flow.Pooling;

namespace AbilityKit.Ability.Flow
{
    public sealed class FlowSession : IDisposable
    {
        private FlowRunner _runner;
        private bool _disposed;

        public event Action Started;
        public event Action<FlowStatus, FlowStatus> StatusChanged;
        public event Action<FlowStatus> Finished;

        public event Action<Exception> UnhandledException;

        public IFlowObserver Observer { get; set; }
        public IFlowTraceRecorder TraceRecorder { get; set; }

        internal FlowSession(bool deferRent)
        {
            _disposed = true;
        }

        public FlowSession()
        {
            ResetForRent(FlowPools.RentRunner());
        }

        public FlowContext Context
        {
            get
            {
                ThrowIfDisposed();
                return _runner.Context;
            }
        }

        public FlowStatus Status
        {
            get
            {
                ThrowIfDisposed();
                return _runner.Status;
            }
        }

        public void Start(IFlowNode root)
        {
            ThrowIfDisposed();
            _runner.Observer = Observer;
            _runner.TraceRecorder = TraceRecorder;
            // Started 先于 runner.Start 触发：立即完成的流程 Finished 会在 Start 内触发，
            // 若 Started 放在后面，订阅者会先收到 Finished 再收到 Started（时序倒置）。
            Started?.Invoke();
            _runner.Start(
                root,
                onFinished: s => Finished?.Invoke(s),
                onStatusChanged: (prev, next) => StatusChanged?.Invoke(prev, next)
            );
        }

        public FlowStatus Step(float deltaTime)
        {
            ThrowIfDisposed();
            return _runner.Step(deltaTime);
        }

        public void Stop()
        {
            ThrowIfDisposed();
            _runner.Stop();
        }

        public void Dispose()
        {
            if (_disposed) return;
            ResetCallbacks();
            FlowPools.ReleaseRunner(_runner);
            _runner = null;
            _disposed = true;
        }

        internal void ResetForRent(FlowRunner runner)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _runner.UnhandledException += OnRunnerUnhandledException;
            _disposed = false;
        }

        internal void ResetForRelease()
        {
            if (!_disposed)
            {
                Dispose();
                return;
            }

            ResetCallbacks();
            _runner = null;
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
