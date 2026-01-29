using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Ability.Impl.Moba.Conponents
{
    [Actor]
    public sealed class ProjectileLauncherComponent : IComponent
    {
        public int LauncherId;
        public int ProjectileId;
        public int RootActorId;

        public long EndTimeMs;
        public int ActiveBullets;

        public int ScheduleId;
        public int IntervalFrames;
        public int TotalCount;
    }
}
