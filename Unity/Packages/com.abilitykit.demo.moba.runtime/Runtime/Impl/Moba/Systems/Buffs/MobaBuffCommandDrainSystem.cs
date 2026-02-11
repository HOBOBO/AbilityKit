using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems.Buffs
{
    [WorldSystem(order: MobaSystemOrder.BuffCommandsDrain, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaBuffCommandDrainSystem : WorldSystemBase
    {
        private MobaBuffService _buffs;

        public MobaBuffCommandDrainSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _buffs);
        }

        protected override void OnExecute()
        {
            _buffs?.DrainPending(maxCommands: 256);
        }
    }
}
