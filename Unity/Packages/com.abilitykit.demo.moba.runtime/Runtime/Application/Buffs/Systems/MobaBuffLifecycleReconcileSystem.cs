using AbilityKit.Ability.World;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services.Buffs;

namespace AbilityKit.Demo.Moba.Systems.Buffs
{
    [WorldSystem(order: MobaSystemOrder.BuffLifecycleReconcile, Phase = WorldSystemPhase.Execute)]
    public sealed class MobaBuffLifecycleReconcileSystem : WorldSystemBase
    {
        private MobaBuffService _buffs;
        private global::Entitas.IGroup<global::ActorEntity> _group;

        public MobaBuffLifecycleReconcileSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _buffs);
            _group = Contexts.Actor().GetGroup(ActorMatcher.AllOf(ActorComponentsLookup.ActorId, ActorComponentsLookup.Buffs));
        }

        protected override void OnExecute()
        {
            if (_buffs == null) return;

            var entities = _group?.GetEntities();
            if (entities == null || entities.Length == 0) return;

            for (var i = 0; i < entities.Length; i++)
            {
                _buffs.ReconcileActorBuffLifecycles(entities[i]);
            }
        }
    }
}
