using System;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core
{
    public interface IMobaConfigTableRegistry
    {
        BattleDemo.MobaRuntimeConfigTableRegistry.Entry[] Tables { get; }
    }
}
