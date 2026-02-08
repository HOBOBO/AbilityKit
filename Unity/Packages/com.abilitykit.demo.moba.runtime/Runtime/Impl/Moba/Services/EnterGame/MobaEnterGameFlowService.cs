using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Share.Common.Log;
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
        private readonly ActorEntityInitPipeline _generator;
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
            ActorEntityInitPipeline generator)
            : this(lobby, snapshot, spawn, worldContext, actorIds, registry, entities, playerActorMap, skills, generator, config: null)
        {
        }

        public MobaEnterGameFlowService(MobaLobbyStateService lobby, MobaEnterGameSnapshotService snapshot, MobaActorSpawnSnapshotService spawn, IWorldContext worldContext, ActorIdAllocator actorIds, MobaActorRegistry registry, MobaEntityManager entities, MobaPlayerActorMapService playerActorMap, MobaSkillLoadoutService skills, ActorEntityInitPipeline generator, MobaConfigDatabase config)
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
            if (_lobby.Started)
            {
                Log.Info("[MobaEnterGameFlowService] TryStartGame: already started");
                return false;
            }
            if (!_lobby.CanStartGame())
            {
                Log.Info($"[MobaEnterGameFlowService] TryStartGame: CanStartGame=false (playerCount={_lobby.PlayerCount}, allReady={_lobby.AllReady})");
                return false;
            }
            if (!_lobby.TryMarkStarted())
            {
                Log.Info("[MobaEnterGameFlowService] TryStartGame: TryMarkStarted failed");
                return false;
            }

            if (!_lobby.TryGetEnterGameReq(out var req))
            {
                Log.Info("[MobaEnterGameFlowService] TryStartGame: EnterGameReq not found");
                return false;
            }

            var effectiveReq = NormalizeEnterGameReq(_config, in req);

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

        /*
         * 职责边界/数据流：
         * 1) EnterMobaGameReq 是“外部传入初始实体信息”的一种入口（adapter）。
         *    外部可以动态提供出生点/队伍/英雄/等级等信息，本函数不会改写这些关键输入。
         * 2) 逻辑层允许部分字段缺省（例如 AttributeTemplateId、SkillIds）。
         *    当缺省时，本函数使用 MobaConfigDatabase（读表结果）进行兜底补齐。
         * 3) 该步骤位于 Spawn 之前：
         *    - Spawn（ActorSpawnPipeline）只消费 loadout 并创建“骨架实体”，不做读表。
         *    - Init（ActorEntityInitPipeline）会根据 loadout/template 初始化属性/技能。
         *    因此必须在进入 Spawn/Init 管线前把 loadout 尽量规范化。
         */
        private static EnterMobaGameReq NormalizeEnterGameReq(MobaConfigDatabase config, in EnterMobaGameReq req)
        {
            if (config == null) return req;
            if (req.Players == null || req.Players.Length == 0) return req;

            try
            {
                var src = req.Players;
                var dst = new MobaPlayerLoadout[src.Length];

                for (int i = 0; i < src.Length; i++)
                {
                    var p = src[i];

                    /*
                     * - Spawn/坐标等动态信息来自外部输入，保持不变。
                     * - AttributeTemplateId/SkillIds 如果外部不填，则从角色表兜底。
                     */
                    var attributeTemplateId = p.AttributeTemplateId;
                    int[] skillIds = p.SkillIds;

                    if ((attributeTemplateId <= 0 || skillIds == null) && config.TryGetCharacter(p.HeroId, out var character) && character != null)
                    {
                        if (attributeTemplateId <= 0)
                        {
                            attributeTemplateId = character.AttributeTemplateId;
                        }

                        if (skillIds == null)
                        {
                            var list = character.SkillIds;
                            if (list is int[] arr) skillIds = arr;
                            else if (list == null || list.Count == 0) skillIds = Array.Empty<int>();
                            else
                            {
                                var tmp = new int[list.Count];
                                for (int j = 0; j < list.Count; j++) tmp[j] = list[j];
                                skillIds = tmp;
                            }
                        }
                    }

                    dst[i] = new MobaPlayerLoadout(
                        playerId: p.PlayerId,
                        teamId: p.TeamId,
                        heroId: p.HeroId,
                        attributeTemplateId: attributeTemplateId,
                        level: p.Level,
                        basicAttackSkillId: p.BasicAttackSkillId,
                        skillIds: skillIds,
                        spawnIndex: p.SpawnIndex,
                        unitSubType: p.UnitSubType,
                        mainType: p.MainType,
                        hasSpawnPosition: p.HasSpawnPosition,
                        spawnX: p.SpawnX,
                        spawnY: p.SpawnY,
                        spawnZ: p.SpawnZ);
                }

                return new EnterMobaGameReq(
                    playerId: req.PlayerId,
                    matchId: req.MatchId,
                    mapId: req.MapId,
                    randomSeed: req.RandomSeed,
                    tickRate: req.TickRate,
                    inputDelayFrames: req.InputDelayFrames,
                    opCode: req.OpCode,
                    payload: req.Payload,
                    players: dst);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[MobaEnterGameFlowService] NormalizeEnterGameReq failed");
                return req;
            }
        }

        public void Dispose()
        {
        }
    }
}
