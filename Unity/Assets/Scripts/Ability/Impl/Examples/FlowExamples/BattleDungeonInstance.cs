using System;
using AbilityKit.Ability.Flow;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Impl.FlowExamples
{
    public sealed class BattleDungeonInstance : IDisposable
    {
        private readonly BattleDungeonFlowManager _manager;

        public event Action<FlowStatus> Finished;
        public event Action<FlowStatus, FlowStatus> StatusChanged;

        public BattleDungeonInstance()
        {
            _manager = new BattleDungeonFlowManager();
            _manager.Finished += s => Finished?.Invoke(s);
            _manager.StatusChanged += (prev, next) => StatusChanged?.Invoke(prev, next);
        }

        public FlowStatus Status => _manager.Status;

        public void Start(float runSeconds)
        {
            _manager.Start(new WorldId("room_1"), runSeconds);
        }

        public void Stop()
        {
            _manager.Stop();
        }

        public void Dispose()
        {
            _manager.Dispose();
        }

    }
}
