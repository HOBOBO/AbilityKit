using System;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.Impl.Moba.Util.Generator;
using AbilityKit.Ability.Share.Impl.Moba.Services.EntityManager;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaEnterGameFlowService
    {
        private readonly MobaLobbyStateService _lobby;
        private readonly MobaEnterGameSnapshotService _snapshot;
        private readonly IWorldContext _worldContext;
        private readonly ActorIdAllocator _actorIds;
        private readonly MobaActorRegistry _registry;
        private readonly MobaEntityManager _entities;
        private readonly MobaPlayerActorMapService _playerActorMap;
        private readonly MobaSkillLoadoutService _skills;
        private readonly MobaConfigDatabase _config;

        public MobaEnterGameFlowService(
            MobaLobbyStateService lobby,
            MobaEnterGameSnapshotService snapshot,
            IWorldContext worldContext,
            ActorIdAllocator actorIds,
            MobaActorRegistry registry,
            MobaEntityManager entities,
            MobaPlayerActorMapService playerActorMap,
            MobaSkillLoadoutService skills)
            : this(lobby, snapshot, worldContext, actorIds, registry, entities, playerActorMap, skills, config: null)
        {
        }

        public MobaEnterGameFlowService(MobaLobbyStateService lobby, MobaEnterGameSnapshotService snapshot, IWorldContext worldContext, ActorIdAllocator actorIds, MobaActorRegistry registry, MobaEntityManager entities, MobaPlayerActorMapService playerActorMap, MobaSkillLoadoutService skills, MobaConfigDatabase config)
        {
            _lobby = lobby ?? throw new ArgumentNullException(nameof(lobby));
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _worldContext = worldContext ?? throw new ArgumentNullException(nameof(worldContext));
            _actorIds = actorIds ?? throw new ArgumentNullException(nameof(actorIds));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
            _playerActorMap = playerActorMap ?? throw new ArgumentNullException(nameof(playerActorMap));
            _skills = skills ?? throw new ArgumentNullException(nameof(skills));
            _config = config;
        }

        public bool TryStartGame(ActorContext actorContext)
        {
            if (actorContext == null) throw new ArgumentNullException(nameof(actorContext));
            if (_lobby.Started) return false;
            if (!_lobby.CanStartGame()) return false;
            if (!_lobby.TryMarkStarted()) return false;

            if (!_lobby.TryGetEnterGameReq(out var req)) return false;

            var built = EntityBuilder.BuildEnterGameActors(actorContext, _actorIds, _registry, _entities, req);

            if (_config != null)
            {
                try
                {
                    var heroId = 0;
                    if (req.Players != null && req.Players.Length > 0)
                    {
                        heroId = req.Players[0].HeroId;
                        for (int i = 0; i < req.Players.Length; i++)
                        {
                            if (req.Players[i].PlayerId.Equals(req.PlayerId))
                            {
                                heroId = req.Players[i].HeroId;
                                break;
                            }
                        }
                    }

                    var character = _config.GetCharacter(heroId);
                    var ids = character.SkillIds == null ? Array.Empty<int>() : new System.Collections.Generic.List<int>(character.SkillIds).ToArray();
                    _skills.SetLoadout(built.LocalActorId, ids);
                }
                catch
                {
                }
            }

            _playerActorMap.Bind(req.PlayerId, built.LocalActorId);

            var payload = new byte[12];
            var p = built.LocalActorTransform.Position;
            Buffer.BlockCopy(BitConverter.GetBytes(p.X), 0, payload, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(p.Y), 0, payload, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(p.Z), 0, payload, 8, 4);

            var res = new EnterMobaGameRes(
                worldId: _worldContext.Id,
                playerId: req.PlayerId,
                localActorId: built.LocalActorId,
                randomSeed: req.RandomSeed,
                tickRate: req.TickRate,
                inputDelayFrames: req.InputDelayFrames,
                players: built.Players,
                opCode: 0,
                payload: payload,
                playersLoadout: req.Players
            );

            _snapshot.PublishEnterGameResPayload(EnterMobaGameCodec.SerializeRes(res));
            return true;
        }
    }
}
