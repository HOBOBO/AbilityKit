using System;

namespace AbilityKit.Ability.World.DI
{
    public interface IWorldModuleInfo
    {
        string Id { get; }
        int Order { get; }
        Type[] DependsOn { get; }
        Type[] ConflictsWith { get; }
    }
}
