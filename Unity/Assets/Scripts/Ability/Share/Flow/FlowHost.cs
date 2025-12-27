using System;

namespace AbilityKit.Ability.Flow
{
    public sealed class FlowHost<TArgs> : IDisposable
    {
        private readonly IFlowRootProvider<TArgs> _provider;
        private readonly FlowSession _session;

        public event Action Started;
        public event Action<FlowStatus, FlowStatus> StatusChanged;
        public event Action<FlowStatus> Finished;

        public FlowHost(IFlowRootProvider<TArgs> provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _session = new FlowSession();

            _session.Started += () => Started?.Invoke();
            _session.StatusChanged += (p, n) => StatusChanged?.Invoke(p, n);
            _session.Finished += s => Finished?.Invoke(s);
        }

        public FlowStatus Status => _session.Status;
        public FlowContext Context => _session.Context;

        public void Start(TArgs args)
        {
            var root = _provider.CreateRoot(args);
            _session.Start(root);
        }

        internal FlowStatus Step(float deltaTime)
        {
            return _session.Step(deltaTime);
        }

        public void Stop()
        {
            _session.Stop();
        }

        public void Dispose()
        {
            _session.Dispose();
        }
    }
}
