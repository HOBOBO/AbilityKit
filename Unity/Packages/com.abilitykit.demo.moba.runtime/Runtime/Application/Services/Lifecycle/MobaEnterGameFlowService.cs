using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.Runtime;
using AbilityKit.Ability.Host.Extensions.Moba.Snapshot;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Services.Map;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Protocol.Moba.CreateWorld;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(IMobaGameStartPort))]
    [WorldService(typeof(MobaEnterGameFlowService))]
    public sealed class MobaEnterGameFlowService : IService, IMobaGameStartPort
    {
        [WorldInject] private IMobaEnterGameSnapshotSink _snapshot = null;
        [WorldInject] private IWorldContext _worldContext = null;
        [WorldInject] private global::Entitas.IContexts _contexts = null;
        [WorldInject] private ActorIdAllocator _actorIds = null;
        [WorldInject] private MobaActorRegistry _registry = null;
        [WorldInject] private MobaEntityManager _entities = null;
        [WorldInject] private MobaPlayerActorMapService _playerActorMap = null;
        [WorldInject(required: false)] private ActorEntityInitPipeline _generator = null;
        [WorldInject] private MobaActorSpawnSnapshotService _spawn = null;
        [WorldInject(required: false)] private IWorldResolver _services = null;
        [WorldInject(required: false)] private MobaLogicWorldRunGateService _phase = null;
        [WorldInject(required: false)] private MobaGameplayService _gameplay = null;
        [WorldInject(required: false)] private IMobaMapRuntimeService _maps = null;

        private bool _started;

        public MobaGameStartResult TryStartGame(in MobaGameStartSpec spec)
        {
            var actorContext = (_contexts as global::Contexts)?.actor;
            if (actorContext == null)
            {
                return Fail(MobaGameStartFailureCode.MissingActorContext, "ActorContext is null");
            }

            return TryApplyGameStartSpec(actorContext, in spec);
        }

        private MobaGameStartResult TryApplyGameStartSpec(ActorContext actorContext, in MobaGameStartSpec spec)
        {
            var validation = ValidateStartRequest(actorContext, in spec, out var effectiveReq);
            if (!validation.Succeeded)
            {
                return validation;
            }

            Log.Info($"[MobaEnterGameFlowService] TryStartGame: begin (players={(effectiveReq.Players != null ? effectiveReq.Players.Length : 0)}, playerId={effectiveReq.PlayerId.Value})");

            var spawnEntries = new List<MobaActorSpawnSnapshotEntry>(effectiveReq.Players != null ? effectiveReq.Players.Length : 4);
            var buildResult = BuildEnterGameActors(actorContext, in effectiveReq, spawnEntries, out var built);
            if (!buildResult.Succeeded)
            {
                return buildResult;
            }

            var bindResult = BindPlayerActors(built.PlayerActors);
            if (!bindResult.Succeeded)
            {
                RollbackBuiltActors(built.PlayerActors);
                return bindResult;
            }

            var gameplayPreparation = PrepareGameplay(effectiveReq.GameplayId);
            if (!gameplayPreparation.Succeeded)
            {
                RollbackBuiltActors(built.PlayerActors);
                return gameplayPreparation;
            }

            var publishResult = PublishEnterGameSnapshots(in effectiveReq, in built, spawnEntries);
            if (!publishResult.Succeeded)
            {
                _gameplay.CancelPreparedStart();
                return publishResult;
            }

            CommitGameplay();
            MarkGameStarted();
            return MobaGameStartResult.Success;
        }

        private MobaGameStartResult ValidateStartRequest(ActorContext actorContext, in MobaGameStartSpec spec, out EnterMobaGameReq effectiveReq)
        {
            effectiveReq = default;

            if (actorContext == null)
            {
                return Fail(MobaGameStartFailureCode.MissingActorContext, "ActorContext is null");
            }

            if (_started)
            {
                return Fail(MobaGameStartFailureCode.AlreadyStarted, "game already started");
            }

            var envelopeValidation = MobaProtocolValidation.ValidateEnterGameReqEnvelope(in spec.EnterReq);
            if (!envelopeValidation.IsValid)
            {
                return Fail(MobaGameStartFailureCode.InvalidProtocol, envelopeValidation.ToString());
            }

            if (_generator == null)
            {
                return Fail(MobaGameStartFailureCode.MissingActorEntityInitPipeline, "ActorEntityInitPipeline not resolved; battle start is blocked to avoid partially initialized actors");
            }

            effectiveReq = spec.EnterReq;
            if (!TryResolveSpawnPositions(in effectiveReq, out effectiveReq, out var spawnError))
            {
                return Fail(MobaGameStartFailureCode.ActorBuildFailed, spawnError);
            }

            var requestValidation = MobaProtocolValidation.ValidateEnterGameReq(in effectiveReq);
            if (!requestValidation.IsValid)
            {
                return Fail(MobaGameStartFailureCode.InvalidProtocol, requestValidation.ToString());
            }

            return MobaGameStartResult.Success;
        }

        private bool TryResolveSpawnPositions(
            in EnterMobaGameReq request,
            out EnterMobaGameReq resolved,
            out string error)
        {
            resolved = request;
            error = null;

            var players = request.Players;
            if (players == null || players.Length == 0) return true;

            MobaPlayerLoadout[] normalized = null;
            for (var i = 0; i < players.Length; i++)
            {
                var loadout = players[i];
                if (loadout.HasSpawnPosition != 0) continue;

                if (_maps == null || _maps.CurrentMap == null)
                {
                    error = $"Map runtime is unavailable while resolving player spawn. playerId={loadout.PlayerId.Value}";
                    return false;
                }

                MapSpawnPointMO spawnPoint = null;
                var found = loadout.SpawnIndex > 0 &&
                    _maps.TryGetSpawnPointById(loadout.SpawnIndex, out spawnPoint) &&
                    spawnPoint.TeamId == loadout.TeamId;
                if (!found)
                {
                    found = _maps.TryGetTeamSpawnPoint(loadout.TeamId, loadout.SpawnIndex, out spawnPoint);
                }

                if (!found || spawnPoint == null)
                {
                    error = $"Map spawn point was not found. playerId={loadout.PlayerId.Value}, " +
                            $"teamId={loadout.TeamId}, spawnIndex={loadout.SpawnIndex}";
                    return false;
                }

                normalized ??= (MobaPlayerLoadout[])players.Clone();
                normalized[i] = new MobaPlayerLoadout(
                    loadout.PlayerId,
                    loadout.TeamId,
                    loadout.HeroId,
                    loadout.AttributeTemplateId,
                    loadout.Level,
                    loadout.BasicAttackSkillId,
                    loadout.SkillIds,
                    loadout.SpawnIndex,
                    loadout.UnitSubType,
                    loadout.MainType,
                    hasSpawnPosition: 1,
                    spawnX: spawnPoint.Position.X,
                    spawnY: spawnPoint.Position.Y,
                    spawnZ: spawnPoint.Position.Z,
                    brainId: loadout.BrainId,
                    enableBrainOnSpawn: loadout.EnableBrainOnSpawn);
            }

            if (normalized == null) return true;

            resolved = new EnterMobaGameReq(
                request.PlayerId,
                request.MatchId,
                request.MapId,
                request.RandomSeed,
                request.TickRate,
                request.InputDelayFrames,
                request.OpCode,
                request.Payload,
                normalized,
                request.GameplayId);
            return true;
        }

        private MobaGameStartResult BuildEnterGameActors(
            ActorContext actorContext,
            in EnterMobaGameReq effectiveReq,
            List<MobaActorSpawnSnapshotEntry> spawnEntries,
            out BuildActorsResult built)
        {
            built = default;

            try
            {
                built = ActorSpawnPipeline.BuildActorsFromEnterGameReqAndInitialize(
                    actorContext,
                    _actorIds,
                    _registry,
                    _entities,
                    effectiveReq,
                    initializer: (entity, loadout) =>
                    {
                        if (!_generator.TryInitializeFromLoadout(entity, in loadout, out var error))
                        {
                            throw new InvalidOperationException(
                                error ?? $"actor loadout initialization failed. playerId={loadout.PlayerId.Value} heroId={loadout.HeroId}");
                        }
                    },
                    onActorBuilt: (entity, loadout) =>
                    {
                        var actorId = entity != null && entity.hasActorId ? entity.actorId.Value : 0;
                        if (actorId <= 0)
                        {
                            throw new InvalidOperationException($"actor id is invalid after build. playerId={loadout.PlayerId.Value}, heroId={loadout.HeroId}");
                        }

                        spawnEntries.Add(new MobaActorSpawnSnapshotEntry
                        {
                            NetId = actorId,
                            Kind = (int)SpawnEntityKind.Character,
                            Code = loadout.HeroId,
                            OwnerNetId = 0,
                            X = loadout.SpawnX,
                            Y = loadout.SpawnY,
                            Z = loadout.SpawnZ
                        });
                    });
            }
            catch (Exception ex)
            {
                ReportStartupException(ex, MobaBattleExceptionDomain.Bootstrap, nameof(BuildEnterGameActors), MobaBattleExceptionSeverity.Critical, $"players={effectiveReq.Players.Length}");
                return Fail(MobaGameStartFailureCode.ActorBuildFailed, ex.Message);
            }

            var buildValidation = ValidateBuildResult(in built, effectiveReq.Players.Length);
            if (!buildValidation.Succeeded)
            {
                RollbackBuiltActors(built.PlayerActors);
                return buildValidation;
            }

            Log.Info($"[MobaEnterGameFlowService] TryStartGame: BuildEnterGameActors done (localActorId={built.LocalActorId})");
            return MobaGameStartResult.Success;
        }

        private MobaGameStartResult PublishEnterGameSnapshots(
            in EnterMobaGameReq effectiveReq,
            in BuildActorsResult built,
            List<MobaActorSpawnSnapshotEntry> spawnEntries)
        {
            var res = CreateEnterGameRes(in effectiveReq, in built);

            try
            {
                _snapshot.PublishEnterGameResPayload(EnterMobaGameCodec.SerializeRes(res));
            }
            catch (Exception ex)
            {
                ReportStartupException(ex, MobaBattleExceptionDomain.Snapshot, "PublishEnterGameSnapshot", MobaBattleExceptionSeverity.Critical, $"playerId={effectiveReq.PlayerId.Value} localActorId={built.LocalActorId}");
                return Fail(MobaGameStartFailureCode.PublishEnterGameSnapshotFailed, ex.Message);
            }

            try
            {
                var payload = MobaActorSpawnSnapshotCodec.Serialize(spawnEntries.ToArray());
                _spawn.PublishSpawnPayload(payload);
            }
            catch (Exception ex)
            {
                ReportStartupException(ex, MobaBattleExceptionDomain.Snapshot, "PublishActorSpawnSnapshot", MobaBattleExceptionSeverity.Critical, $"spawnCount={(spawnEntries != null ? spawnEntries.Count : 0)}");
                return Fail(MobaGameStartFailureCode.PublishSpawnSnapshotFailed, ex.Message);
            }

            return MobaGameStartResult.Success;
        }

        private EnterMobaGameRes CreateEnterGameRes(in EnterMobaGameReq effectiveReq, in BuildActorsResult built)
        {
            var position = built.LocalActorTransform.Position;
            var payload = MobaEnterGamePayloadCodec.Serialize(in position);

            return new EnterMobaGameRes(
                worldId: _worldContext.Id,
                playerId: effectiveReq.PlayerId,
                localActorId: built.LocalActorId,
                randomSeed: effectiveReq.RandomSeed,
                tickRate: effectiveReq.TickRate,
                inputDelayFrames: effectiveReq.InputDelayFrames,
                players: built.Players,
                opCode: MobaEnterGamePayloadCodec.PayloadOpCode,
                payload: payload,
                playersLoadout: effectiveReq.Players
            );
        }

        private void MarkGameStarted()
        {
            _phase?.SetInGame("game start applied");
            _started = true;
        }

        private static MobaGameStartResult ValidateBuildResult(in BuildActorsResult built, int expectedPlayerCount)
        {
            if (built.LocalActorId <= 0)
            {
                return Fail(MobaGameStartFailureCode.InvalidActorBuildResult, $"local actor id is invalid, actual={built.LocalActorId}");
            }

            if (built.Players == null || built.Players.Length != expectedPlayerCount)
            {
                return Fail(MobaGameStartFailureCode.InvalidActorBuildResult, $"player entry count mismatch, expected={expectedPlayerCount}, actual={(built.Players != null ? built.Players.Length : 0)}");
            }

            if (built.PlayerActors == null || built.PlayerActors.Length != expectedPlayerCount)
            {
                return Fail(MobaGameStartFailureCode.InvalidActorBuildResult, $"player actor count mismatch, expected={expectedPlayerCount}, actual={(built.PlayerActors != null ? built.PlayerActors.Length : 0)}");
            }

            return MobaGameStartResult.Success;
        }

        private static MobaGameStartResult Fail(MobaGameStartFailureCode failureCode, string message)
        {
            var result = MobaGameStartResult.Fail(failureCode, message);
            Log.Error($"[MobaEnterGameFlowService] ApplyGameStartSpec failed. {result}");
            return result;
        }

        private void ReportStartupException(
            Exception exception,
            MobaBattleExceptionDomain domain,
            string operation,
            MobaBattleExceptionSeverity severity,
            string detail)
        {
            if (exception == null) return;

            if (_services != null && _services.TryResolve<IMobaBattleExceptionPolicy>(out var policy) && policy != null)
            {
                policy.TryHandle(
                    exception,
                    new MobaBattleExceptionContext(domain, operation, detail: detail),
                    severity);
                return;
            }

            Log.Exception(exception, $"[MobaEnterGameFlowService] {operation} failed. {detail}");
        }

        private MobaGameStartResult PrepareGameplay(int gameplayId)
        {
            if (_gameplay == null)
            {
                return Fail(MobaGameStartFailureCode.MissingGameplayService, "MobaGameplayService is required to start battle gameplay.");
            }

            if (gameplayId <= 0)
            {
                return Fail(MobaGameStartFailureCode.InvalidGameplayId, $"gameplay id must be positive for formal battle start. gameplayId={gameplayId}");
            }

            if (!_gameplay.TryPrepareStart(gameplayId, out var error))
            {
                var detail = string.IsNullOrEmpty(error)
                    ? string.Empty
                    : $", detail={error}";
                return Fail(MobaGameStartFailureCode.GameplayStartFailed, $"gameplay preparation failed. gameplayId={gameplayId}, phase={_gameplay.Phase}, currentGameplayId={_gameplay.CurrentGameplayId}{detail}");
            }

            return MobaGameStartResult.Success;
        }

        private void CommitGameplay()
        {
            if (!_gameplay.CommitPreparedStart())
            {
                throw new InvalidOperationException(
                    "prepared gameplay commit failed after snapshots were published");
            }
        }

        private MobaGameStartResult BindPlayerActors(MobaPlayerActorEntry[] playerActors)
        {
            if (playerActors == null)
            {
                return Fail(MobaGameStartFailureCode.InvalidActorBuildResult, "player actor entries are null");
            }

            for (int i = 0; i < playerActors.Length; i++)
            {
                var entry = playerActors[i];
                if (entry.ActorId <= 0)
                {
                    return Fail(MobaGameStartFailureCode.InvalidActorBuildResult, $"player actor id is invalid. index={i}, playerId={entry.PlayerId.Value}, actorId={entry.ActorId}");
                }

                _playerActorMap.Bind(entry.PlayerId, entry.ActorId);
            }

            return MobaGameStartResult.Success;
        }

        private void RollbackBuiltActors(MobaPlayerActorEntry[] playerActors)
        {
            if (playerActors == null) return;

            for (var i = playerActors.Length - 1; i >= 0; i--)
            {
                var entry = playerActors[i];
                if (entry.ActorId <= 0) continue;

                _playerActorMap?.Unbind(entry.PlayerId, entry.ActorId);

                global::ActorEntity entity = null;
                if (_entities != null)
                {
                    _entities.TryGetActorEntity(entry.ActorId, out entity);
                    _entities.Unregister(entry.ActorId);
                }

                if (entity == null && _registry != null)
                {
                    _registry.TryGet(entry.ActorId, out entity);
                }

                _registry?.Unregister(entry.ActorId);
                ActorSpawnPipeline.DestroyBuiltEntity(entity);
            }
        }

        public void Dispose()
        {
        }
    }
}
