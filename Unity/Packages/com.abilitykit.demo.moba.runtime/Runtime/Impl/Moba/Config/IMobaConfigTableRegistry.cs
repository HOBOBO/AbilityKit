using System;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config
{
    public interface IMobaConfigTableRegistry
    {
        MobaRuntimeConfigTableRegistry.Entry[] Tables { get; }
    }
}
