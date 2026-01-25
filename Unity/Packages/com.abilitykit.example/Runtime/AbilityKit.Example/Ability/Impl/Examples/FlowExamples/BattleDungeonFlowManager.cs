using System;
using System.Collections.Generic;
using AbilityKit.Ability.Flow;
using AbilityKit.Ability.Flow.Stages;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Impl.FlowExamples
{
    public sealed class BattleDungeonFlowManager : IDisposable
    {
        private readonly FlowHost<BattleDungeonFlowArgs> _host;

        public event Action<FlowStatus> Finished;
        public event Action<FlowStatus, FlowStatus> StatusChanged;

        public BattleDungeonFlowManager()
        {
            _host = new FlowHost<BattleDungeonFlowArgs>(
                new StagedFlowRootProvider<BattleDungeonFlowArgs>(
                    core: new BattleDungeonFlowProvider(),
                    contributors: new List<IFlowStageContributor<BattleDungeonFlowArgs>>
                    {
                        new BattleDungeonStageLogContributor()
                    }
                )
            );

            _host.Finished += s => Finished?.Invoke(s);
            _host.StatusChanged += (p, n) => StatusChanged?.Invoke(p, n);
        }

        public FlowStatus Status => _host.Status;

        public void Start(WorldId worldId, float runSeconds)
        {
            _host.Start(new BattleDungeonFlowArgs(worldId, runSeconds));
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
