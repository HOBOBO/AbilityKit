using System;
using AbilityKit.Ability.World.Management;

namespace AbilityKit.Ability.FrameSync
{
    public sealed class WorldManagerFrameDriver : IFrameDriver
    {
        private readonly IWorldManager _worlds;
        private FrameIndex _frame;

        public Action<FrameIndex, float> PostStep;

        public WorldManagerFrameDriver(IWorldManager worlds)
        {
            _worlds = worlds ?? throw new ArgumentNullException(nameof(worlds));
            _frame = new FrameIndex(0);
        }

        public FrameIndex Frame => _frame;

        public void Step(float deltaTime)
        {
            _worlds.Tick(deltaTime);
            _frame = new FrameIndex(_frame.Value + 1);

            PostStep?.Invoke(_frame, deltaTime);
        }
    }
}
