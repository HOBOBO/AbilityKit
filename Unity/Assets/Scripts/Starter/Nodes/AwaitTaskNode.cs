using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Ability.Flow;

namespace AbilityKit.Starter.Nodes
{
    public sealed class AwaitTaskNode : IFlowNode
    {
        private readonly Func<FlowContext, Task> _start;
        private volatile bool _done;
        private volatile bool _faulted;
        private Exception _exception;
        private SynchronizationContext _sync;

        public AwaitTaskNode(Func<FlowContext, Task> start)
        {
            _start = start ?? throw new ArgumentNullException(nameof(start));
        }

        public void Enter(FlowContext ctx)
        {
            _done = false;
            _faulted = false;
            _exception = null;
            _sync = SynchronizationContext.Current;

            Task task;
            try
            {
                task = _start(ctx);
            }
            catch (Exception e)
            {
                _faulted = true;
                _exception = e;
                _done = true;
                ctx.TryGet(out FlowWakeUp wakeUp);
                wakeUp?.Wake();
                return;
            }

            if (task == null)
            {
                _done = true;
                ctx.TryGet(out FlowWakeUp wu);
                wu?.Wake();
                return;
            }

            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    _faulted = true;
                    _exception = t.Exception;
                }

                _done = true;

                void Wake()
                {
                    if (!ctx.TryGet(out FlowWakeUp wakeUp)) return;
                    wakeUp.Wake();
                }

                if (_sync != null)
                {
                    _sync.Post(_ => Wake(), null);
                }
                else
                {
                    Wake();
                }
            }, TaskScheduler.Default);
        }

        public FlowStatus Tick(FlowContext ctx, float deltaTime)
        {
            if (!_done) return FlowStatus.Running;
            if (_faulted) return FlowStatus.Failed;
            return FlowStatus.Succeeded;
        }

        public void Exit(FlowContext ctx)
        {
        }

        public void Interrupt(FlowContext ctx)
        {
        }
    }
}
