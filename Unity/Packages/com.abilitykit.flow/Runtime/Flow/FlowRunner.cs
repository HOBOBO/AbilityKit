#nullable enable annotations

using System;
using System.Runtime.ExceptionServices;

namespace AbilityKit.Ability.Flow
{
    /// <summary>
    /// 串行推进一棵流程节点树，持有本次运行的上下文作用域；不提供跨线程同步。
    /// </summary>
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

        private FlowWakeUp? _wakeUp;
        private bool _wakeRequested;
        private bool _pumping;
        private bool _stepping;
        private bool _transitioning;
        private bool _disposed;

        internal FlowRunner()
        {
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

        /// <summary>
        /// 停止旧运行并同步启动根节点，初次推进使用零秒增量。
        /// </summary>
        public void Start(IFlowNode root)
        {
            Start(root, null, null);
        }

        /// <summary>
        /// 停止旧运行并启动根节点。状态与完成通知在调用线程同步派发，不能重入生命周期操作。
        /// </summary>
        public void Start(IFlowNode root, Action<FlowStatus> onFinished, Action<FlowStatus, FlowStatus> onStatusChanged)
        {
            ThrowIfDisposed();
            EnsureLifecycleIdle();
            if (root == null) throw new ArgumentNullException(nameof(root));

            _transitioning = true;
            try
            {
                FinishRun(FlowStatus.Canceled, interrupt: true, rethrowCleanupError: true);
                _rootScope = _ctx.BeginRunScope();
                _root = root;
                _onFinished = onFinished;
                _onStatusChanged = onStatusChanged;
                _wakeUp = new FlowWakeUp(this);
                _ctx.Set(_wakeUp);
                _diagnostics = new FlowRuntimeDiagnostics(Observer, TraceRecorder);
                _ctx.Set(_diagnostics);

                SetStatus(FlowStatus.Running);
                _diagnostics.Statistics.RunsStarted++;
                _diagnostics.Observer.OnRunStarted(_diagnostics.RunId, root, _ctx);
                _diagnostics.Record(FlowTraceEventType.RunStarted, root, _status, 0f, 0L);
            }
            catch (Exception exception)
            {
                if (_root != null)
                {
                    HandleUnhandledException(exception);
                    FinishRun(FlowStatus.Failed, interrupt: true);
                }

                throw;
            }
            finally
            {
                _transitioning = false;
                _wakeRequested = false;
            }

            Step(0f);
        }

        /// <summary>
        /// 推进指定秒数；回调中的重入推进只登记唤醒，不执行嵌套的节点遍历。
        /// </summary>
        public FlowStatus Step(float deltaTime)
        {
            ThrowIfDisposed();
            if (_stepping || _transitioning || _pumping)
            {
                _wakeRequested = true;
                return _status;
            }

            return Advance(deltaTime);
        }

        private FlowStatus Advance(float deltaTime)
        {
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
                    _entered = true;
                    FlowDiagnostics.Enter(_ctx, _root);
                }

                var s = FlowDiagnostics.Tick(_ctx, _root, deltaTime);
                if (s == FlowStatus.Running) return _status;

                FinishRun(s, interrupt: false);
                return _status;
            }
            catch (Exception ex)
            {
                HandleUnhandledException(ex);
                FinishRun(FlowStatus.Failed, interrupt: true);
                return _status;
            }
        }

        private void FinishRun(FlowStatus status, bool interrupt, bool rethrowCleanupError = false)
        {
            if (_root == null) return;

            var finishedRoot = _root;
            _wakeUp?.Invalidate();
            _wakeUp = null;
            try
            {
                if (interrupt)
                {
                    if (_entered) FlowDiagnostics.Interrupt(_ctx, finishedRoot, status);
                }
                else
                {
                    PublishTerminalStatus(status);
                    FlowDiagnostics.Exit(_ctx, finishedRoot, status);
                }
            }
            catch (Exception exception)
            {
                HandleUnhandledException(exception);
                if (rethrowCleanupError) throw;
            }
            finally
            {
                if (interrupt) PublishTerminalStatus(status);
                _root = null;
                _entered = false;
                _wakeRequested = false;

                var scope = _rootScope;
                _rootScope = null;
                scope?.Dispose();
                NotifyFinished(finishedRoot);
            }
        }

        internal void Wake()
        {
            if (_disposed) return;
            if (_status != FlowStatus.Running) return;
            _wakeRequested = true;
            if (_pumping || _stepping || _transitioning) return;

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
                        var message = $"FlowRunner pump iteration limit exceeded: limit={MaxPumpIterationsPerWake}";
                        try
                        {
                            _diagnostics?.Record(FlowTraceEventType.PumpLimitExceeded, _root, _status, 0f, 0L, message);
                        }
                        catch (Exception exception)
                        {
                            HandleUnhandledException(exception);
                        }
                        HandleUnhandledException(new InvalidOperationException(message));
                        FinishRun(FlowStatus.Failed, interrupt: true);
                        return;
                    }

                    _wakeRequested = false;
                    Advance(0f);
                }
            }
            finally
            {
                _pumping = false;
            }
        }

        private void HandleUnhandledException(Exception ex)
        {
            /*
             * 各上报渠道隔离；二次异常不能盖过原始错误或阻断收尾。
             */
            try
            {
                if (_diagnostics != null)
                {
                    _diagnostics.Statistics.UnhandledExceptions++;
                    _diagnostics.Observer.OnUnhandledException(_diagnostics.RunId, ex, _ctx);
                    _diagnostics.Record(FlowTraceEventType.UnhandledException, _root, _status, 0f, 0L, ex.Message, ex);
                }
            }
            catch
            {
            }

            try { ExceptionHandler?.Invoke(ex); }
            catch { }

            try
            {
                UnhandledException?.Invoke(ex);
            }
            catch
            {
            }
        }

        /// <summary>
        /// 中断当前运行并释放其作用域；根节点中断异常在完成收尾后向调用方传播。
        /// </summary>
        public void Stop()
        {
            ThrowIfDisposed();
            if (_root == null) return;
            EnsureLifecycleIdle();

            _transitioning = true;
            try
            {
                FinishRun(FlowStatus.Canceled, interrupt: true, rethrowCleanupError: true);
            }
            finally
            {
                _transitioning = false;
            }
        }

        private void PublishTerminalStatus(FlowStatus status)
        {
            try { SetStatus(status); }
            catch (Exception exception) { HandleUnhandledException(exception); }
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

            var callback = _onFinished;
            _onFinished = null;
            _onStatusChanged = null;

            try
            {
                if (_diagnostics != null)
                {
                    _diagnostics.Statistics.RunsFinished++;
                    _diagnostics.Observer.OnRunFinished(_diagnostics.RunId, _status, _ctx);
                    _diagnostics.Record(FlowTraceEventType.RunFinished, finishedRoot, _status, 0f, 0L);
                }
            }
            catch (Exception exception) { HandleUnhandledException(exception); }

            try { callback?.Invoke(_status); }
            catch (Exception exception) { HandleUnhandledException(exception); }
        }

        /// <summary>
        /// 停止运行并清空上下文；即使中断抛出异常也会解除持有。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            EnsureLifecycleIdle();
            try { Stop(); }
            finally
            {
                ResetCallbacks();
                _ctx?.Clear();
                _ctx = null;
                _disposed = true;
            }
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

        internal FlowContext? ResetForRelease(out ExceptionDispatchInfo? cleanupError)
        {
            EnsureLifecycleIdle();
            cleanupError = null;
            if (!_disposed)
            {
                try
                {
                    Stop();
                }
                catch (Exception exception)
                {
                    /*
                     * 保留清理异常，归还方完成入池后再抛出。重入拒绝不走清理路径。
                     */
                    cleanupError = ExceptionDispatchInfo.Capture(exception);
                }
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

        internal void EnsureLifecycleIdle()
        {
            if (_transitioning || _stepping || _pumping)
            {
                throw new InvalidOperationException("A flow callback cannot change the lifecycle of its active runner.");
            }
        }
    }
}
