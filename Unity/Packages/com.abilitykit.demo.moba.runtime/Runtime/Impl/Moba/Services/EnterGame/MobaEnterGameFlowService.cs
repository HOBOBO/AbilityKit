using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;
using AbilityKit.Core.Common.Log;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Core.Math;
using AbilityKit.Ability.Share.Impl.Moba.Util.Generator;
using AbilityKit.Ability.Share.Impl.Moba.Services.EntityManager;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    using AbilityKit.Ability.Share.Impl.Moba.CreateWorld;
    public sealed class MobaEnterGameFlowService : IService
    {
        private readonly MobaEnterGameSnapshotService _snapshot;
        private readonly IWorldContext _worldContext;
        private readonly ActorIdAllocator _actorIds;
        private readonly MobaActorRegistry _registry;
        private readonly MobaEntityManager _entities;
        private readonly MobaPlayerActorMapService _playerActorMap;
        private readonly MobaSkillLoadoutService _skills;
        private readonly MobaConfigDatabase _config;
        private readonly ActorEntityInitPipeline _generator;
        private readonly MobaActorSpawnSnapshotService _spawn;

        public MobaEnterGameFlowService(
            MobaEnterGameSnapshotService snapshot,
            MobaActorSpawnSnapshotService spawn,
            IWorldContext worldContext,
            ActorIdAllocator actorIds,
            MobaActorRegistry registry,
            MobaEntityManager entities,
            MobaPlayerActorMapService playerActorMap,
            MobaSkillLoadoutService skills,
            ActorEntityInitPipeline generator)
            : this(snapshot, spawn, worldContext, actorIds, registry, entities, playerActorMap, skills, generator, config: null)
        {
        }

        public MobaEnterGameFlowService(MobaEnterGameSnapshotService snapshot, MobaActorSpawnSnapshotService spawn, IWorldContext worldContext, ActorIdAllocator actorIds, MobaActorRegistry registry, MobaEntityManager entities, MobaPlayerActorMapService playerActorMap, MobaSkillLoadoutService skills, ActorEntityInitPipeline generator, MobaConfigDatabase config)
        {
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

        public bool ApplyGameStartSpec(ActorContext actorContext, in MobaGameStartSpec spec)
        {
            if (actorContext == null) throw new ArgumentNullException(nameof(actorContext));

            var req = spec.EnterReq;

            var effectiveReq = MobaGameStartSpecNormalizer.Normalize(_config, in req);

            Log.Info($"[MobaEnterGameFlowService] TryStartGame: begin (players={(effectiveReq.Players != null ? effectiveReq.Players.Length : 0)}, playerId={effectiveReq.PlayerId.Value})");

            var spawnEntries = new System.Collections.Generic.List<MobaActorSpawnSnapshotCodec.Entry>(effectiveReq.Players != null ? effectiveReq.Players.Length : 4);

            var built = ActorSpawnPipeline.BuildActorsFromEnterGameReqAndInitialize(
                actorContext,
                _actorIds,
                _registry,
                _entities,
                effectiveReq,
                initializer: (entity, loadout) =>
                {
                    if (_generator == null) return;
                    _generator.InitializeFromLoadout(entity, loadout);
                },
                onActorBuilt: (entity, loadout) =>
                {
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
                    catch (Exception ex)
                    {
                        Log.Exception(ex, "[MobaEnterGameFlowService] build spawn entry failed");
                    }
                });

            Log.Info($"[MobaEnterGameFlowService] TryStartGame: BuildEnterGameActors done (localActorId={built.LocalActorId})");

            _playerActorMap.Bind(req.PlayerId, built.LocalActorId);

            var p = built.LocalActorTransform.Position;
            var payload = EnterMobaGamePayloadCodec.Serialize(in p);

            var res = new EnterMobaGameRes(
                worldId: _worldContext.Id,
                playerId: effectiveReq.PlayerId,
                localActorId: built.LocalActorId,
                randomSeed: effectiveReq.RandomSeed,
                tickRate: effectiveReq.TickRate,
                inputDelayFrames: effectiveReq.InputDelayFrames,
                players: built.Players,
                opCode: EnterMobaGamePayloadCodec.PayloadOpCode,
                payload: payload,
                playersLoadout: effectiveReq.Players
            );

            _snapshot.PublishEnterGameResPayload(EnterMobaGameCodec.SerializeRes(res));

            try
            {
                var payload2 = MobaActorSpawnSnapshotCodec.Serialize(spawnEntries.ToArray());
                _spawn.PublishSpawnPayload(payload2);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[MobaEnterGameFlowService] publish spawn payload failed");
            }
            return true;
        }

        public void Dispose()
        {
        }
    }
}
