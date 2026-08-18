using System;

namespace AbilityKit.Ability.Flow
{
    public sealed class FlowRunner : IDisposable
    {
        private FlowContext _ctx;
        private IFlowNode _root;
        private FlowStatus _status;
        private bool _entered;

        private IDisposable _rootScope;

        private int _pumpIterations;
        private FlowRuntimeDiagnostics _diagnostics;

        private Action<FlowStatus> _onFinished;
        private Action<FlowStatus, FlowStatus> _onStatusChanged;

        public event Action<Exception> UnhandledException;

        public Action<Exception> ExceptionHandler { get; set; }
        public IFlowObserver Observer { get; set; }
        public IFlowTraceRecorder TraceRecorder { get; set; }

        public int MaxPumpIterationsPerWake { get; set; } = 128;

        private readonly FlowWakeUp _wakeUp;
        private bool _wakeRequested;
        private bool _pumping;
        private bool _stepping;
        private bool _disposed;

        internal FlowRunner()
        {
            _wakeUp = new FlowWakeUp(Wake);
            _disposed = true;
        }

        public FlowRunner(FlowContext ctx)
            : this()
        {
            ResetForRent(ctx);
        }

        public FlowContext Context => _ctx;
        public FlowStatus Status => _status;
        public FlowRuntimeDiagnostics Diagnostics => _diagnostics;

        public void Start(IFlowNode root)
        {
            Start(root, null, null);
        }

        public void Start(IFlowNode root, Action<FlowStatus> onFinished, Action<FlowStatus, FlowStatus> onStatusChanged)
        {
            ThrowIfDisposed();
            if (root == null) throw new ArgumentNullException(nameof(root));

            Stop();
            _root = root;

            _onFinished = onFinished;
            _onStatusChanged = onStatusChanged;

            _rootScope?.Dispose();
            _rootScope = _ctx.BeginScope();
            _ctx.Set(_wakeUp);
            _diagnostics = new FlowRuntimeDiagnostics(Observer, TraceRecorder);
            _ctx.Set(_diagnostics);
            _wakeRequested = false;

            SetStatus(FlowStatus.Running);
            _entered = false;
            _diagnostics.Statistics.RunsStarted++;
            _diagnostics.Observer.OnRunStarted(_diagnostics.RunId, _root, _ctx);
            _diagnostics.Record(FlowTraceEventType.RunStarted, _root, _status, 0f, 0L);

            // Prime once so Enter() runs and nodes can subscribe to events immediately.
            // After this, event callbacks can call FlowWakeUp.Wake() to progress without continuous Step() calls.
            Step(0f);
        }

        public FlowStatus Step(float deltaTime)
        {
            ThrowIfDisposed();
            if (_stepping)
            {
                // 重入（节点回调里再触发 Wake/Step）：登记唤醒，由外层步进结束后统一推进，
                // 避免对外层尚未完成 Enter 的节点树发起嵌套 Tick/重复 Enter。
                _wakeRequested = true;
                return _status;
            }

            _stepping = true;
            try
            {
                return StepCore(deltaTime);
            }
            finally
            {
                _stepping = false;
                if (!_pumping && _wakeRequested && _root != null && _status == FlowStatus.Running)
                {
                    Pump();
                }
            }
        }

        private FlowStatus StepCore(float deltaTime)
        {
            if (_root == null) return _status;
            if (_status != FlowStatus.Running) return _status;

            try
            {
                if (!_entered)
                {
                    FlowDiagnostics.Enter(_ctx, _root);
                    _entered = true;
                }

                var s = FlowDiagnostics.Tick(_ctx, _root, deltaTime);
                if (s == FlowStatus.Running) return _status;

                SetStatus(s);

                var finishedRoot = _root;
                try
                {
                    FlowDiagnostics.Exit(_ctx, finishedRoot, _status);
                }
                catch (Exception exitEx)
                {
                    // 根节点 Exit 抛异常不改变已达成的终态：按二级异常上报，
                    // 收尾流程（清理 ctx/rootScope、NotifyFinished）照常完成。
                    HandleUnhandledException(exitEx);
                }
                finally
                {
                    _root = null;
                }

                _ctx.Remove<FlowWakeUp>();
                _ctx.Remove<FlowRuntimeDiagnostics>();
                _rootScope?.Dispose();
                _rootScope = null;
                NotifyFinished(finishedRoot);
                return _status;
            }
            catch (Exception ex)
            {
                HandleUnhandledException(ex);
                AbortDueToException();
                return _status;
            }
        }

        private void AbortDueToException()
        {
            if (_root == null) return;

            var finishedRoot = _root;
            try
            {
                try
                {
                    FlowDiagnostics.Interrupt(_ctx, finishedRoot, FlowStatus.Failed);
                }
                catch (Exception ex)
                {
                    HandleUnhandledException(ex);
                }
            }
            finally
            {
                _root = null;
                _entered = false;
                if (_status == FlowStatus.Running)
                {
                    SetStatus(FlowStatus.Failed);
                }

                _ctx.Remove<FlowWakeUp>();
                _ctx.Remove<FlowRuntimeDiagnostics>();
                _rootScope?.Dispose();
                _rootScope = null;
                NotifyFinished(finishedRoot);
            }
        }

        private void Wake()
        {
            if (_disposed) return;
            if (_status != FlowStatus.Running) return;
            _wakeRequested = true;
            if (_pumping || _stepping) return;

            Pump();
        }

        private void Pump()
        {
            if (_root == null) return;
            if (_status != FlowStatus.Running) return;

            _pumping = true;
            try
            {
                _pumpIterations = 0;
                while (_wakeRequested && _root != null && _status == FlowStatus.Running)
                {
                    _pumpIterations++;
                    if (MaxPumpIterationsPerWake > 0 && _pumpIterations > MaxPumpIterationsPerWake)
                    {
                        _diagnostics?.Record(FlowTraceEventType.PumpLimitExceeded, _root, _status, 0f, 0L, $"FlowRunner pump iteration limit exceeded: limit={MaxPumpIterationsPerWake}");
                        HandleUnhandledException(new InvalidOperationException($"FlowRunner pump iteration limit exceeded: limit={MaxPumpIterationsPerWake}"));
                        AbortDueToException();
                        return;
                    }

                    _wakeRequested = false;
                    Step(0f);
                }
            }
            finally
            {
                _pumping = false;
            }
        }

        private void HandleUnhandledException(Exception ex)
        {
            try
            {
                if (_diagnostics != null)
                {
                    _diagnostics.Statistics.UnhandledExceptions++;
                    _diagnostics.Observer.OnUnhandledException(_diagnostics.RunId, ex, _ctx);
                    _diagnostics.Record(FlowTraceEventType.UnhandledException, _root, _status, 0f, 0L, ex.Message, ex);
                }

                ExceptionHandler?.Invoke(ex);
            }
            catch
            {
                // Swallow secondary exceptions from user handlers.
            }

            try
            {
                UnhandledException?.Invoke(ex);
            }
            catch
            {
                // Swallow secondary exceptions from user handlers.
            }
        }

        public void Stop()
        {
            ThrowIfDisposed();
            if (_root == null) return;

            var finishedRoot = _root;
            try
            {
                FlowDiagnostics.Interrupt(_ctx, finishedRoot, FlowStatus.Canceled);
            }
            finally
            {
                _root = null;
                _entered = false;
                if (_status == FlowStatus.Running)
                {
                    SetStatus(FlowStatus.Canceled);
                }

                _ctx.Remove<FlowWakeUp>();
                _ctx.Remove<FlowRuntimeDiagnostics>();

                _rootScope?.Dispose();
                _rootScope = null;

                NotifyFinished(finishedRoot);
            }
        }

        private void SetStatus(FlowStatus next)
        {
            if (_status == next) return;
            var prev = _status;
            _status = next;
            if (_diagnostics != null)
            {
                _diagnostics.Statistics.LastStatus = next;
                _diagnostics.Observer.OnStatusChanged(_diagnostics.RunId, prev, next, _ctx);
                _diagnostics.Record(FlowTraceEventType.StatusChanged, _root, next, 0f, 0L, prev + " -> " + next);
            }
            _onStatusChanged?.Invoke(prev, next);
        }

        private void NotifyFinished(IFlowNode finishedRoot)
        {
            if (_status == FlowStatus.Running || _status == FlowStatus.NotStarted) return;

            if (_diagnostics != null)
            {
                _diagnostics.Statistics.RunsFinished++;
                _diagnostics.Observer.OnRunFinished(_diagnostics.RunId, _status, _ctx);
                _diagnostics.Record(FlowTraceEventType.RunFinished, finishedRoot, _status, 0f, 0L);
            }

            var cb = _onFinished;
            _onFinished = null;
            _onStatusChanged = null;
            cb?.Invoke(_status);
        }

        public void Dispose()
        {
            if (_disposed) return;
            Stop();
            ResetCallbacks();
            _ctx?.Clear();
            _ctx = null;
            _disposed = true;
        }

        internal void ResetForRent(FlowContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            _ctx = context;
            _ctx.Clear();
            _root = null;
            _status = FlowStatus.NotStarted;
            _entered = false;
            _rootScope = null;
            _pumpIterations = 0;
            _wakeRequested = false;
            _pumping = false;
            _stepping = false;
            MaxPumpIterationsPerWake = 128;
            _diagnostics = null;
            ResetCallbacks();
            _disposed = false;
        }

        internal FlowContext ResetForRelease()
        {
            if (!_disposed)
            {
                Stop();
            }

            ResetCallbacks();
            var context = _ctx;
            context?.Clear();
            _ctx = null;
            _root = null;
            _status = FlowStatus.NotStarted;
            _entered = false;
            _rootScope = null;
            _pumpIterations = 0;
            _wakeRequested = false;
            _pumping = false;
            _stepping = false;
            MaxPumpIterationsPerWake = 128;
            _diagnostics = null;
            _disposed = true;
            return context;
        }

        private void ResetCallbacks()
        {
            _onFinished = null;
            _onStatusChanged = null;
            UnhandledException = null;
            ExceptionHandler = null;
            Observer = null;
            TraceRecorder = null;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FlowRunner));
        }
    }
}
