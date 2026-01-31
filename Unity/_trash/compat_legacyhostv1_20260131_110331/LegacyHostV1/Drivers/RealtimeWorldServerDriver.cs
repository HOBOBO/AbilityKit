using AbilityKit.Ability.FrameSync;
using System;
using AbilityKit.Ability.World.Management;

namespace AbilityKit.Ability.Host.Drivers
{
    public sealed class RealtimeWorldServerDriver : IWorldServerDriver
    {
        private readonly IWorldManager _worlds;
        private FrameIndex _frame = new FrameIndex(0);

        public FrameIndex Frame => _frame;

        public void Tick(float deltaTime)
        {
            _frame = new FrameIndex(_frame.Value + 1);
        }
    }
}
