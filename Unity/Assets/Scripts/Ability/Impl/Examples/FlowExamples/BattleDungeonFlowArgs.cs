using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Impl.FlowExamples
{
    public readonly struct BattleDungeonFlowArgs
    {
        public readonly WorldId WorldId;
        public readonly float RunSeconds;

        public BattleDungeonFlowArgs(WorldId worldId, float runSeconds)
        {
            WorldId = worldId;
            RunSeconds = runSeconds;
        }
    }
}
