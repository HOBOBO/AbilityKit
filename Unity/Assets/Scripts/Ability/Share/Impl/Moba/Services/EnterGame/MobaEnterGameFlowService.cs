using System;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.Impl.Moba.Util.Generator;
using AbilityKit.Ability.Share.Impl.Moba.Services.EntityManager;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaEnterGameFlowService : IService
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
        private readonly MobaActorEntityGenerator _generator;
        private readonly MobaActorSpawnSnapshotService _spawn;

        public MobaEnterGameFlowService(
            MobaLobbyStateService lobby,
            MobaEnterGameSnapshotService snapshot,
            MobaActorSpawnSnapshotService spawn,
            IWorldContext worldContext,
            ActorIdAllocator actorIds,
            MobaActorRegistry registry,
            MobaEntityManager entities,
            MobaPlayerActorMapService playerActorMap,
            MobaSkillLoadoutService skills,
            MobaActorEntityGenerator generator)
            : this(lobby, snapshot, spawn, worldContext, actorIds, registry, entities, playerActorMap, skills, generator, config: null)
        {
        }

        public MobaEnterGameFlowService(MobaLobbyStateService lobby, MobaEnterGameSnapshotService snapshot, MobaActorSpawnSnapshotService spawn, IWorldContext worldContext, ActorIdAllocator actorIds, MobaActorRegistry registry, MobaEntityManager entities, MobaPlayerActorMapService playerActorMap, MobaSkillLoadoutService skills, MobaActorEntityGenerator generator, MobaConfigDatabase config)
        {
            _lobby = lobby ?? throw new ArgumentNullException(nameof(lobby));
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _spawn = spawn ?? throw new ArgumentNullException(nameof(spawn));
            _worldContext = worldContext ?? throw new ArgumentNullException(nameof(worldContext));
            _actorIds = actorIds ?? throw new ArgumentNullException(nameof(actorIds));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
            _playerActorMap = playerActorMap ?? throw new ArgumentNullException(nameof(playerActorMap));
            _skills = skills ?? throw new ArgumentNullException(nameof(skills));
            _generator = generator;
            _config = config;
        }

        public bool TryStartGame(ActorContext actorContext)
        {
            if (actorContext == null) throw new ArgumentNullException(nameof(actorContext));
            if (_lobby.Started) return false;
            if (!_lobby.CanStartGame()) return false;
            if (!_lobby.TryMarkStarted()) return false;

            if (!_lobby.TryGetEnterGameReq(out var req)) return false;

            var spawnEntries = new System.Collections.Generic.List<MobaActorSpawnSnapshotCodec.Entry>(req.Players != null ? req.Players.Length : 4);

            var built = EntityBuilder.BuildEnterGameActors(
                actorContext,
                _actorIds,
                _registry,
                _entities,
                req,
                onActorBuilt: (entity, loadout) =>
                {
                    if (_generator == null) return;
                    _generator.InitializeFromLoadout(entity, loadout);

                    try
                    {
                        if (entity == null) return;
                        var actorId = entity.hasActorId ? entity.actorId.Value : 0;
                        if (actorId <= 0) return;

                        if (entity.hasSkillLoadout)
                        {
                            var skills = entity.skillLoadout.ActiveSkills;
                            if (skills == null || skills.Length == 0)
                            {
                                _skills.SetLoadout(actorId, Array.Empty<int>());
                            }
                            else
                            {
                                var ids = new int[skills.Length];
                                for (int i = 0; i < skills.Length; i++)
                                {
                                    ids[i] = skills[i] != null ? skills[i].SkillId : 0;
                                }
                                _skills.SetLoadout(actorId, ids);
                            }
                        }
                    }
                    catch
                    {
                    }

                    try
                    {
                        var actorId = entity != null && entity.hasActorId ? entity.actorId.Value : 0;
                        if (actorId > 0)
                        {
                            spawnEntries.Add(new MobaActorSpawnSnapshotCodec.Entry(
                                netId: actorId,
                                kind: (int)SpawnEntityKind.Character,
                                code: loadout.HeroId,
                                ownerNetId: 0,
                                x: loadout.SpawnX,
                                y: loadout.SpawnY,
                                z: loadout.SpawnZ));
                        }
                    }
                    catch
                    {
                    }
                });

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

            var p = built.LocalActorTransform.Position;
            var payload = EnterMobaGamePayloadCodec.Serialize(in p);

            var res = new EnterMobaGameRes(
                worldId: _worldContext.Id,
                playerId: req.PlayerId,
                localActorId: built.LocalActorId,
                randomSeed: req.RandomSeed,
                tickRate: req.TickRate,
                inputDelayFrames: req.InputDelayFrames,
                players: built.Players,
                opCode: EnterMobaGamePayloadCodec.PayloadOpCode,
                payload: payload,
                playersLoadout: req.Players
            );

            _snapshot.PublishEnterGameResPayload(EnterMobaGameCodec.SerializeRes(res));

            try
            {
                var payload2 = MobaActorSpawnSnapshotCodec.Serialize(spawnEntries.ToArray());
                _spawn.PublishSpawnPayload(payload2);
            }
            catch
            {
            }
            return true;
        }

        public void Dispose()
        {
        }
    }
}
