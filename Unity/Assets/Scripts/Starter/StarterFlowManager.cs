using System;
using System.Collections.Generic;
using AbilityKit.Ability.Flow;
using AbilityKit.Ability.Flow.Stages;

namespace AbilityKit.Starter
{
    public sealed class StarterFlowManager : IDisposable
    {
        private readonly FlowHost<StarterArgs> _host;

        public event Action<FlowStatus> Finished;
        public event Action<FlowStatus, FlowStatus> StatusChanged;

        public StarterFlowManager(IReadOnlyList<IFlowStageContributor<StarterArgs>> contributors = null)
        {
            _host = new FlowHost<StarterArgs>(
                new OrderedStagedFlowRootProvider<StarterArgs>(
                    core: new StarterFlowProvider(),
                    contributors: contributors,
                    tryStages: StarterStages.TryStages,
                    finallyStages: StarterStages.FinallyStages
                )
            );

            _host.Finished += s => Finished?.Invoke(s);
            _host.StatusChanged += (p, n) => StatusChanged?.Invoke(p, n);
        }

        public FlowStatus Status => _host.Status;

        public void Start(StarterArgs args)
        {
            _host.Start(args);
        }

        public void Stop()
        {
            _host.Stop();
        }

        public void Dispose()
        {
            _host.Dispose();
        }
    }
}
