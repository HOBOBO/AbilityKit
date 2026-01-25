using System;
using System.Threading;

namespace AbilityKit.Ability.Flow.Nodes
{
    public sealed class WaitSecondsEventNode : IFlowNode
    {
        private readonly float _seconds;
        private volatile bool _done;
        private Timer _timer;
        private SynchronizationContext _sync;

        public WaitSecondsEventNode(float seconds)
        {
            if (seconds < 0f) throw new ArgumentOutOfRangeException(nameof(seconds));
            _seconds = seconds;
        }

        public void Enter(FlowContext ctx)
        {
            _done = false;
            _sync = SynchronizationContext.Current;

            if (_seconds <= 0f)
            {
                _done = true;
                ctx.TryGet(out FlowWakeUp wakeUp);
                wakeUp?.Wake();
                return;
            }

            _timer?.Dispose();
            _timer = new Timer(
                _ =>
                {
                    _done = true;

                    void WakeOnContext()
                    {
                        if (!ctx.TryGet(out FlowWakeUp wakeUp)) return;
                        wakeUp.Wake();
                    }

                    if (_sync != null)
                    {
                        _sync.Post(_ => WakeOnContext(), null);
                    }
                    else
                    {
                        WakeOnContext();
                    }
                },
                state: null,
                dueTime: TimeSpan.FromSeconds(_seconds),
                period: Timeout.InfiniteTimeSpan
            );
        }

        public FlowStatus Tick(FlowContext ctx, float deltaTime)
        {
            return _done ? FlowStatus.Succeeded : FlowStatus.Running;
        }

        public void Exit(FlowContext ctx)
        {
            _timer?.Dispose();
            _timer = null;
        }

        public void Interrupt(FlowContext ctx)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }
}
