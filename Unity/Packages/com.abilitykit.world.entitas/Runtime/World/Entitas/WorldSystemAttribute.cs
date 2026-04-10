using System;
using AbilityKit.Core.Common.Marker;

namespace AbilityKit.Ability.World.Entitas
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class WorldSystemAttribute : MarkerAttribute
    {
        public int Order { get; }
        public WorldSystemPhase Phase { get; set; } = WorldSystemPhase.Execute;

        public WorldSystemAttribute(int order = 0)
        {
            Order = order;
        }
    }

    public enum WorldSystemPhase
    {
        PreExecute = 0,
        Execute = 1,
        PostExecute = 2,
    }
}
