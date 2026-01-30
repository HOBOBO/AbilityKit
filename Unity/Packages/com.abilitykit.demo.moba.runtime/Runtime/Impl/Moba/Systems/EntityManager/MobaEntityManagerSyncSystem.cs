using System;
using AbilityKit.Ability.Share.Impl.Moba.Services.EntityManager;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.Share.Impl.Moba;

namespace AbilityKit.Ability.Share.Impl.Moba.Systems.EntityManager
{
    [WorldSystem(order: MobaSystemOrder.EntityManagerSync, Phase = WorldSystemPhase.PreExecute)]
    public sealed class MobaEntityManagerSyncSystem : WorldSystemBase
    {
        private MobaEntityManager _entities;
        private global::Entitas.IGroup<global::ActorEntity> _group;

        public MobaEntityManagerSyncSystem(global::Entitas.IContexts contexts, IWorldServices services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _entities);
            _group = Contexts.Actor().GetGroup(global::ActorMatcher.AllOf(
                global::ActorComponentsLookup.ActorId,
                global::ActorComponentsLookup.Team,
                global::ActorComponentsLookup.EntityMainType,
                global::ActorComponentsLookup.UnitSubType,
                global::ActorComponentsLookup.OwnerPlayerId));
        }

        protected override void OnExecute()
        {
            if (_entities == null) return;
            var arr = _group.GetEntities();
            if (arr == null || arr.Length == 0) return;

            for (int i = 0; i < arr.Length; i++)
            {
                var e = arr[i];
                if (e == null) continue;
                _entities.TryRegisterFromEntity(e);
            }
        }
    }
}

