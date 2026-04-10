using System.Collections.Generic;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Share.ECS.Entitas;
using AbilityKit.ECS;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Game.Battle
{
    public sealed class DefaultBattleDebugFacade : IBattleDebugFacade
    {
        private readonly List<EcsEntityId> _entityIdCache = new List<EcsEntityId>(256);

        public bool TryGetSession(out BattleLogicSession session)
        {
            session = BattleLogicSessionHost.Current;
            return session != null;
        }

        public bool TryListEntities(out IReadOnlyList<EcsEntityId> ids)
        {
            ids = null;

            if (!TryGetSession(out var session)) return false;
            if (!session.TryGetWorld(out var world) || world == null) return false;

            var services = world.Services;
            if (services == null) return false;

            if (!services.TryResolve<EntitasActorIdLookup>(out var lookup) || lookup == null) return false;

            _entityIdCache.Clear();
            foreach (var actorId in lookup.ActorIds)
            {
                _entityIdCache.Add(new EcsEntityId(actorId));
            }

            ids = _entityIdCache;
            return true;
        }

        public bool TryResolveUnit(EcsEntityId id, out IUnitFacade unit)
        {
            unit = null;

            if (!TryGetSession(out var session)) return false;
            if (!session.TryGetWorld(out var world) || world == null) return false;

            var services = world.Services;
            if (services == null) return false;

            if (!services.TryResolve<IUnitResolver>(out var resolver) || resolver == null) return false;

            return resolver.TryResolve(id, out unit);
        }
    }
}
