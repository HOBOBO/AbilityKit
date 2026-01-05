using System;

namespace AbilityKit.Ability.World.Entitas
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class WorldSystemAttribute : Attribute
    {
        public WorldSystemAttribute(int order = 0)
        {
            Order = order;
        }

        public int Order { get; }

        public WorldSystemPhase Phase { get; set; } = WorldSystemPhase.Execute;
    }

    public enum WorldSystemPhase
    {
        PreExecute = 0,
        Execute = 1,
        PostExecute = 2,
    }
}
