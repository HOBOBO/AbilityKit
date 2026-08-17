using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Share.ECS.Entitas;
using AbilityKit.ECS;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Game.Battle
{
    public sealed class DefaultBattleDebugFacade : IBattleDebugFacade
    {
        private readonly List<BattleDebugEntityId> _entityIdCache =
            new List<BattleDebugEntityId>(256);
        private readonly Func<BattleLogicSession> _sessionProvider;
 
        public DefaultBattleDebugFacade(Func<BattleLogicSession> sessionProvider = null)
        {
            _sessionProvider = sessionProvider;
        }
 
        public bool TryGetSession(out BattleLogicSession session)
        {
            session = _sessionProvider != null ? _sessionProvider() : null;
            return session != null;
        }

        public bool TryListEntities(
            out IReadOnlyList<BattleDebugEntityId> ids)
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
                _entityIdCache.Add(
                    new BattleDebugEntityId(actorId));
            }

            ids = _entityIdCache;
            return true;
        }

        public bool TryResolveUnit(
            BattleDebugEntityId id,
            out IUnitFacade unit)
        {
            unit = null;

            if (!TryGetSession(out var session)) return false;
            if (!session.TryGetWorld(out var world) || world == null) return false;

            var services = world.Services;
            if (services == null) return false;

            if (!services.TryResolve<IUnitResolver>(out var resolver) ||
                resolver == null)
            {
                return false;
            }

#pragma warning disable CS0618
            return resolver.TryResolve(
                new AbilityKit.Ability.Share.ECS.EcsEntityId(
                    id.ActorId),
                out unit);
#pragma warning restore CS0618
        }
    }
}
