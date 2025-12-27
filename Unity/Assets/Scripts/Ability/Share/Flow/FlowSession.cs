using System;

namespace AbilityKit.Ability.Flow
{
    public sealed class FlowSession : IDisposable
    {
        private readonly FlowRunner _runner;

        public event Action Started;
        public event Action<FlowStatus, FlowStatus> StatusChanged;
        public event Action<FlowStatus> Finished;

        public FlowSession()
        {
            _runner = new FlowRunner(new FlowContext());
        }

        public FlowContext Context => _runner.Context;
        public FlowStatus Status => _runner.Status;

        public void Start(IFlowNode root)
        {
            _runner.Start(
                root,
                onFinished: s => Finished?.Invoke(s),
                onStatusChanged: (prev, next) => StatusChanged?.Invoke(prev, next)
            );
            Started?.Invoke();
        }

        public FlowStatus Step(float deltaTime)
        {
            return _runner.Step(deltaTime);
        }

        public void Stop()
        {
            _runner.Stop();
        }

        public void Dispose()
        {
            _runner.Dispose();
        }
    }
}
