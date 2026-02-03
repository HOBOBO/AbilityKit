using System;

namespace AbilityKit.Ability.Flow
{
    public sealed class FlowRunner : IDisposable
    {
        private readonly FlowContext _ctx;
        private IFlowNode _root;
        private FlowStatus _status;
        private bool _entered;

        private IDisposable _rootScope;

        private int _pumpIterations;

        private Action<FlowStatus> _onFinished;
        private Action<FlowStatus, FlowStatus> _onStatusChanged;

        public event Action<Exception> UnhandledException;

        public Action<Exception> ExceptionHandler { get; set; }

        public int MaxPumpIterationsPerWake { get; set; } = 128;

        private readonly FlowWakeUp _wakeUp;
        private bool _wakeRequested;
        private bool _pumping;

        public FlowRunner(FlowContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _status = FlowStatus.NotStarted;

            _wakeUp = new FlowWakeUp(Wake);
        }

        public FlowContext Context => _ctx;
        public FlowStatus Status => _status;

        public void Start(IFlowNode root)
        {
            Start(root, null, null);
        }

        public void Start(IFlowNode root, Action<FlowStatus> onFinished, Action<FlowStatus, FlowStatus> onStatusChanged)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));

            Stop();
            _root = root;

            _onFinished = onFinished;
            _onStatusChanged = onStatusChanged;

            _rootScope?.Dispose();
            _rootScope = _ctx.BeginScope();
            _ctx.Set(_wakeUp);
            _wakeRequested = false;

            SetStatus(FlowStatus.Running);
            _entered = false;

            // Prime once so Enter() runs and nodes can subscribe to events immediately.
            // After this, event callbacks can call FlowWakeUp.Wake() to progress without continuous Step() calls.
            Step(0f);
        }

        public FlowStatus Step(float deltaTime)
        {
            if (_root == null) return _status;
            if (_status != FlowStatus.Running) return _status;

            try
            {
                if (!_entered)
                {
                    _root.Enter(_ctx);
                    _entered = true;
                }

                var s = _root.Tick(_ctx, deltaTime);
                if (s == FlowStatus.Running) return _status;

                SetStatus(s);

                try
                {
                    _root.Exit(_ctx);
                }
                finally
                {
                    _root = null;
                }

                _ctx.Remove<FlowWakeUp>();
                _rootScope?.Dispose();
                _rootScope = null;
                NotifyFinished();
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

            try
            {
                try
                {
                    _root.Interrupt(_ctx);
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
                _rootScope?.Dispose();
                _rootScope = null;
                NotifyFinished();
            }
        }

        private void Wake()
        {
            if (_status != FlowStatus.Running) return;
            _wakeRequested = true;
            if (_pumping) return;

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
            if (_root == null) return;

            try
            {
                _root.Interrupt(_ctx);
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

                _rootScope?.Dispose();
                _rootScope = null;

                NotifyFinished();
            }
        }

        private void SetStatus(FlowStatus next)
        {
            if (_status == next) return;
            var prev = _status;
            _status = next;
            _onStatusChanged?.Invoke(prev, next);
        }

        private void NotifyFinished()
        {
            if (_status == FlowStatus.Running || _status == FlowStatus.NotStarted) return;

            var cb = _onFinished;
            _onFinished = null;
            _onStatusChanged = null;
            cb?.Invoke(_status);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
