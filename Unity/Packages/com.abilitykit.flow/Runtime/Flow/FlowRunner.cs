using System;

namespace AbilityKit.Ability.Flow
{
    public sealed class FlowRunner : IDisposable
    {
        private readonly FlowContext _ctx;
        private IFlowNode _root;
        private FlowStatus _status;
        private bool _entered;

        private Action<FlowStatus> _onFinished;
        private Action<FlowStatus, FlowStatus> _onStatusChanged;

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

            NotifyFinished();

            return _status;
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
                while (_wakeRequested && _root != null && _status == FlowStatus.Running)
                {
                    _wakeRequested = false;
                    Step(0f);
                }
            }
            finally
            {
                _pumping = false;
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
