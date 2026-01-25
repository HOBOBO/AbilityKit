using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Impl.Moba.Services.EntityManager;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems.EntityManager
{
    [WorldSystem(order: MobaSystemOrder.EntityManagerCleanup, Phase = WorldSystemPhase.PostExecute)]
    public sealed class MobaEntityManagerCleanupSystem : WorldSystemBase
    {
        private MobaEntityManager _entities;
        private readonly List<int> _tmpIds = new List<int>(256);

        public MobaEntityManagerCleanupSystem(global::Entitas.IContexts contexts, IWorldServices services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryGet(out _entities);
        }

        protected override void OnExecute()
        {
            if (_entities == null) return;

            _entities.GetRegisteredActorIds(_tmpIds);
            if (_tmpIds.Count == 0) return;

            for (int i = _tmpIds.Count - 1; i >= 0; i--)
            {
                var actorId = _tmpIds[i];
                if (!_entities.TryGetActorEntity(actorId, out var e) || e == null)
                {
                    _entities.Unregister(actorId);
                    continue;
                }

                if (!e.hasActorId || e.actorId.Value != actorId)
                {
                    _entities.Unregister(actorId);
                }
            }
        }
    }
}

