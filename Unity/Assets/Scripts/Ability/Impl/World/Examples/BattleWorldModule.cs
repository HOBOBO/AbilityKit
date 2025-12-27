using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.World.Entitas.Systems;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Impl.World.Examples
{
    public sealed class BattleWorldModule : IWorldModule, IEntitasSystemsInstaller
    {
        private readonly TickCounterWorldModule _tickCounter = new TickCounterWorldModule();

        public void Configure(WorldContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            _tickCounter.Configure(builder);
        }

        public void Install(global::Contexts contexts, global::Entitas.Systems systems, IWorldServices services)
        {
            _tickCounter.Install(contexts, systems, services);
        }
    }
}
