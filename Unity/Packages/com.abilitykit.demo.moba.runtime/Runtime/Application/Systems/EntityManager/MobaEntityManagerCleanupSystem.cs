using System.Collections.Generic;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World;

namespace AbilityKit.Demo.Moba.Systems.EntityManager
{
    [WorldSystem(order: MobaSystemOrder.EntityManagerCleanup, Phase = WorldSystemPhase.PostExecute)]
    public sealed class MobaEntityManagerCleanupSystem : WorldSystemBase
    {
        private MobaActorRegistry _registry;
        private MobaEntityManager _entities;
        private MobaActorSpawnRegistrar _registrar;
        private readonly List<int> _tmpIds = new List<int>(256);

        public MobaEntityManagerCleanupSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _registry);
            Services.TryResolve(out _entities);
            _registrar = new MobaActorSpawnRegistrar(_registry, _entities);
        }

        protected override void OnExecute()
        {
            if (_entities == null) return;

            _entities.GetRegisteredActorIds(_tmpIds);
            if (_tmpIds.Count == 0) return;

            for (int i = _tmpIds.Count - 1; i >= 0; i--)
            {
                var actorId = _tmpIds[i];
                if (!_entities.TryGetActorEntity(actorId, out var e) ||
                    e == null ||
                    !e.hasActorId ||
                    e.actorId.Value != actorId)
                {
                    _registrar.Unregister(
                        actorId,
                        out _,
                        publishDespawn: false);
                }
            }
        }
    }
}

