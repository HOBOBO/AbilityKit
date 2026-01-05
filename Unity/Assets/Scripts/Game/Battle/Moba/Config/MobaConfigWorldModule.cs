using System;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Game.Battle.Moba.Config
{
    public sealed class MobaConfigWorldModule : IWorldModule
    {
        public void Configure(WorldContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            builder.TryRegister<MobaConfigDatabase>(WorldLifetime.Singleton, _ => MobaConfigLoader.LoadDefault());
        }
    }
}
