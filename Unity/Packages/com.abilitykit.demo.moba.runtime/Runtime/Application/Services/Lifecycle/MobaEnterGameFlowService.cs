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
using AbilityKit.Demo.Moba.Util.Converter;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Protocol.Moba.CreateWorld;

namespace AbilityKit.Demo.Moba.Services
{
    public readonly struct MobaEnterGameStartupSnapshotBatch
    {
        public readonly byte[] EnterGamePayload;
        public readonly byte[] SpawnPayload;

        public MobaEnterGameStartupSnapshotBatch(
            byte[] enterGamePayload,
            byte[] spawnPayload)
        {
            EnterGamePayload = enterGamePayload;
            SpawnPayload = spawnPayload;
        }
    }

    public interface IMobaEnterGameStartupSnapshotTransaction : IService
    {
        bool TryPrepare(
            in EnterMobaGameRes response,
            IReadOnlyList<MobaActorSpawnSnapshotEntry> spawnEntries,
            out MobaEnterGameStartupSnapshotBatch batch,
            out MobaGameStartFailureCode failureCode,
            out string error);

        void Commit(in MobaEnterGameStartupSnapshotBatch batch);

        void Rollback();
    }

    [WorldService(typeof(IMobaEnterGameStartupSnapshotTransaction))]
    public sealed class MobaEnterGameStartupSnapshotTransaction
        : IMobaEnterGameStartupSnapshotTransaction
    {
        private readonly IMobaEnterGameSnapshotSink _enterGameSnapshots;
        private readonly MobaActorSpawnSnapshotService _spawnSnapshots;

        public MobaEnterGameStartupSnapshotTransaction(
            IMobaEnterGameSnapshotSink enterGameSnapshots,
            MobaActorSpawnSnapshotService spawnSnapshots)
        {
            _enterGameSnapshots = enterGameSnapshots ??
                throw new ArgumentNullException(nameof(enterGameSnapshots));
            _spawnSnapshots = spawnSnapshots ??
                throw new ArgumentNullException(nameof(spawnSnapshots));
        }

        public bool TryPrepare(
            in EnterMobaGameRes response,
            IReadOnlyList<MobaActorSpawnSnapshotEntry> spawnEntries,
            out MobaEnterGameStartupSnapshotBatch batch,
            out MobaGameStartFailureCode failureCode,
            out string error)
        {
            batch = default;
            failureCode = MobaGameStartFailureCode.None;
            error = null;
            try
            {
                var enterGamePayload = EnterMobaGameCodec.SerializeRes(response);
                if (enterGamePayload == null || enterGamePayload.Length == 0)
                {
                    failureCode = MobaGameStartFailureCode.PublishEnterGameSnapshotFailed;
                    error = "enter-game snapshot serialization returned an empty payload";
                    return false;
                }

                var entries = CopySpawnEntries(spawnEntries);
                var spawnPayload = MobaActorSpawnSnapshotCodec.Serialize(entries);
                if (spawnPayload == null || spawnPayload.Length == 0)
                {
                    failureCode = MobaGameStartFailureCode.PublishSpawnSnapshotFailed;
                    error = "actor-spawn snapshot serialization returned an empty payload";
                    return false;
                }

                batch = new MobaEnterGameStartupSnapshotBatch(
                    enterGamePayload,
                    spawnPayload);
                return true;
            }
            catch (Exception ex)
            {
                failureCode = MobaGameStartFailureCode.PublishEnterGameSnapshotFailed;
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        public void Commit(in MobaEnterGameStartupSnapshotBatch batch)
        {
            _enterGameSnapshots.PublishEnterGameResPayload(batch.EnterGamePayload);
            _spawnSnapshots.PublishSpawnPayload(batch.SpawnPayload);
        }

        public void Rollback()
        {
            _enterGameSnapshots.PublishEnterGameResPayload(null);
            _spawnSnapshots.PublishSpawnPayload(null);
        }

        private static MobaActorSpawnSnapshotEntry[] CopySpawnEntries(
            IReadOnlyList<MobaActorSpawnSnapshotEntry> spawnEntries)
        {
            if (spawnEntries == null || spawnEntries.Count == 0)
            {
                return Array.Empty<MobaActorSpawnSnapshotEntry>();
            }

            var entries = new MobaActorSpawnSnapshotEntry[spawnEntries.Count];
            for (var i = 0; i < entries.Length; i++)
            {
                entries[i] = spawnEntries[i];
            }

            return entries;
        }

        public void Dispose()
        {
        }
    }

    [WorldService(typeof(IMobaGameStartPort))]
    [WorldService(typeof(MobaEnterGameFlowService))]
    public sealed class MobaEnterGameFlowService : IService, IMobaGameStartPort
    {
        [WorldInject] private IMobaEnterGameStartupSnapshotTransaction _snapshots = null;
        [WorldInject] private IWorldContext _worldContext = null;
        [WorldInject] private global::Entitas.IContexts _contexts = null;
        [WorldInject] private ActorIdAllocator _actorIds = null;
        [WorldInject] private IMobaActorSpawnCoordinator _actorSpawns = null;
        [WorldInject] private IMobaPlayerActorBindingTransaction _playerActorBindings = null;
        [WorldInject(required: false)] private ActorEntityInitPipeline _generator = null;
        [WorldInject(required: false)] private IWorldResolver _services = null;
        [WorldInject(required: false)] private IMobaBattleRunGateCommitter _runGate = null;
        [WorldInject(required: false)] private IMobaGameplayStartTransaction _gameplayStart = null;
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

            if (MobaRuntimeLog.IsEnabled(
                    MobaRuntimeLogLevel.Info,
                    MobaRuntimeLogPurpose.Lifecycle))
            {
                MobaRuntimeLog.Info(
                    MobaRuntimeLogModule.Bootstrap,
                    MobaRuntimeLogPurpose.Lifecycle,
                    nameof(MobaEnterGameFlowService),
                    $"TryStartGame begin. players={(effectiveReq.Players != null ? effectiveReq.Players.Length : 0)}, playerId={effectiveReq.PlayerId.Value}");
            }

            var spawnEntries = new List<MobaActorSpawnSnapshotEntry>(effectiveReq.Players != null ? effectiveReq.Players.Length : 4);
            var buildResult = BuildEnterGameActors(in effectiveReq, spawnEntries, out var built);
            if (!buildResult.Succeeded)
            {
                return buildResult;
            }

            var bindResult = BindPlayerActors(built.PlayerActors);
            if (!bindResult.Succeeded)
            {
                RollbackPreparedStart(in built, cancelGameplay: false);
                return bindResult;
            }

            var gameplayPreparation = PrepareGameplay(effectiveReq.GameplayId);
            if (!gameplayPreparation.Succeeded)
            {
                RollbackPreparedStart(in built, cancelGameplay: false);
                return gameplayPreparation;
            }

            var snapshotPreparation = PrepareEnterGameSnapshots(
                in effectiveReq,
                in built,
                spawnEntries,
                out var snapshotBatch);
            if (!snapshotPreparation.Succeeded)
            {
                RollbackPreparedStart(in built, cancelGameplay: true);
                return snapshotPreparation;
            }

            return CommitPreparedStart(in built, in snapshotBatch);
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
            in EnterMobaGameReq effectiveReq,
            List<MobaActorSpawnSnapshotEntry> spawnEntries,
            out BuildActorsResult built)
        {
            built = default;

            MobaActorSpawnResult[] spawnResults = null;
            try
            {
                var loadouts = effectiveReq.Players;
                var requests = new MobaActorSpawnRequest[loadouts.Length];
                for (var i = 0; i < loadouts.Length; i++)
                {
                    var loadout = loadouts[i];
                    var spec = MobaConverter.ToActorBuildSpec(_actorIds.Next(), in loadout);
                    var request = MobaActorSpawnRequest.FromSpec(in spec);
                    request.Initializer = (entity, _) =>
                    {
                        if (!_generator.TryInitializeFromLoadout(entity, in loadout, out var error))
                        {
                            throw new InvalidOperationException(
                                error ?? $"actor loadout initialization failed. playerId={loadout.PlayerId.Value} heroId={loadout.HeroId}");
                        }
                    };
                    requests[i] = request;
                }

                if (!_actorSpawns.TryPrepareBatch(requests, out var batch) || !batch.Success)
                {
                    throw new InvalidOperationException(batch.Error ?? "actor spawn batch failed");
                }

                spawnResults = batch.Actors;
                var players = new MobaPlayerEntry[loadouts.Length];
                var playerActors = new MobaPlayerActorEntry[loadouts.Length];
                var localActorId = 0;
                var localTransform = Transform3.Identity;
                for (var i = 0; i < loadouts.Length; i++)
                {
                    var loadout = loadouts[i];
                    var actor = spawnResults[i];
                    var actorId = actor.ActorId;
                    if (actorId <= 0)
                    {
                        throw new InvalidOperationException($"actor id is invalid after build. playerId={loadout.PlayerId.Value}, heroId={loadout.HeroId}");
                    }

                    players[i] = new MobaPlayerEntry(loadout.PlayerId, loadout.TeamId, loadout.HeroId, loadout.SpawnIndex);
                    playerActors[i] = new MobaPlayerActorEntry(loadout.PlayerId, actorId);
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

                    if (localActorId == 0 && loadout.PlayerId.Equals(effectiveReq.PlayerId))
                    {
                        localActorId = actorId;
                        localTransform = actor.Spec.Info.Transform;
                    }
                }

                if (localActorId <= 0)
                {
                    throw new InvalidOperationException($"localPlayerId not found in loadouts. playerId={effectiveReq.PlayerId.Value}");
                }

                built = new BuildActorsResult(
                    localActorId,
                    players,
                    playerActors,
                    in localTransform,
                    spawnResults);
            }
            catch (Exception ex)
            {
                RollbackSpawnResults(spawnResults);
                ReportStartupException(ex, MobaBattleExceptionDomain.Bootstrap, nameof(BuildEnterGameActors), MobaBattleExceptionSeverity.Critical, $"players={effectiveReq.Players.Length}");
                return Fail(MobaGameStartFailureCode.ActorBuildFailed, ex.Message);
            }

            var buildValidation = ValidateBuildResult(in built, effectiveReq.Players.Length);
            if (!buildValidation.Succeeded)
            {
                RollbackBuiltActors(in built);
                return buildValidation;
            }

            if (MobaRuntimeLog.IsEnabled(
                    MobaRuntimeLogLevel.Info,
                    MobaRuntimeLogPurpose.Lifecycle))
            {
                MobaRuntimeLog.Info(
                    MobaRuntimeLogModule.Bootstrap,
                    MobaRuntimeLogPurpose.Lifecycle,
                    nameof(MobaEnterGameFlowService),
                    $"BuildEnterGameActors completed. localActorId={built.LocalActorId}");
            }

            return MobaGameStartResult.Success;
        }

        private MobaGameStartResult PrepareEnterGameSnapshots(
            in EnterMobaGameReq effectiveReq,
            in BuildActorsResult built,
            IReadOnlyList<MobaActorSpawnSnapshotEntry> spawnEntries,
            out MobaEnterGameStartupSnapshotBatch batch)
        {
            batch = default;
            var response = CreateEnterGameRes(in effectiveReq, in built);
            try
            {
                if (_snapshots.TryPrepare(
                        in response,
                        spawnEntries,
                        out batch,
                        out var failureCode,
                        out var error))
                {
                    return MobaGameStartResult.Success;
                }

                return Fail(
                    failureCode == MobaGameStartFailureCode.None
                        ? MobaGameStartFailureCode.PublishEnterGameSnapshotFailed
                        : failureCode,
                    error ?? "enter-game snapshot preparation failed");
            }
            catch (Exception ex)
            {
                ReportStartupException(
                    ex,
                    MobaBattleExceptionDomain.Snapshot,
                    nameof(PrepareEnterGameSnapshots),
                    MobaBattleExceptionSeverity.Critical,
                    $"playerId={effectiveReq.PlayerId.Value} localActorId={built.LocalActorId}");
                return Fail(
                    MobaGameStartFailureCode.PublishEnterGameSnapshotFailed,
                    ex.Message);
            }
        }

        private MobaGameStartResult CommitPreparedStart(
            in BuildActorsResult built,
            in MobaEnterGameStartupSnapshotBatch snapshotBatch)
        {
            try
            {
                if (!_gameplayStart.CommitPreparedStart())
                {
                    throw new InvalidOperationException(
                        _gameplayStart.LastStartFailureReason ??
                        "prepared gameplay commit failed");
                }

                _actorSpawns.PublishBatch(built.SpawnResults);
                _snapshots.Commit(in snapshotBatch);
                MarkGameStarted();
                return MobaGameStartResult.Success;
            }
            catch (Exception ex)
            {
                ReportStartupException(
                    ex,
                    MobaBattleExceptionDomain.Bootstrap,
                    nameof(CommitPreparedStart),
                    MobaBattleExceptionSeverity.Critical,
                    $"localActorId={built.LocalActorId}");
                TryRollbackSnapshots();
                RollbackPreparedStart(in built, cancelGameplay: true);
                return Fail(MobaGameStartFailureCode.GameStartCommitFailed, ex.Message);
            }
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
            _runGate?.SetInGame("game start applied");
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
            if (_gameplayStart == null)
            {
                return Fail(MobaGameStartFailureCode.MissingGameplayService, "IMobaGameplayStartTransaction is required to start battle gameplay.");
            }

            if (gameplayId <= 0)
            {
                return Fail(MobaGameStartFailureCode.InvalidGameplayId, $"gameplay id must be positive for formal battle start. gameplayId={gameplayId}");
            }

            if (!_gameplayStart.TryPrepareStart(gameplayId, out var error))
            {
                var detail = string.IsNullOrEmpty(error)
                    ? string.Empty
                    : $", detail={error}";
                return Fail(MobaGameStartFailureCode.GameplayStartFailed, $"gameplay preparation failed. gameplayId={gameplayId}, phase={_gameplayStart.Phase}, currentGameplayId={_gameplayStart.CurrentGameplayId}{detail}");
            }

            return MobaGameStartResult.Success;
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

                _playerActorBindings.Bind(entry.PlayerId, entry.ActorId);
            }

            return MobaGameStartResult.Success;
        }

        private void RollbackPreparedStart(
            in BuildActorsResult built,
            bool cancelGameplay)
        {
            if (cancelGameplay)
            {
                try
                {
                    if (_gameplayStart != null && _gameplayStart.IsRunning)
                    {
                        _gameplayStart.Reset();
                    }
                    else
                    {
                        _gameplayStart?.CancelPreparedStart();
                    }
                }
                catch (Exception ex)
                {
                    ReportRollbackException(ex, "gameplay");
                }
            }

            RollbackBuiltActors(in built);
        }

        private void RollbackBuiltActors(in BuildActorsResult built)
        {
            var playerActors = built.PlayerActors;
            if (playerActors != null)
            {
                for (var i = playerActors.Length - 1; i >= 0; i--)
                {
                    var entry = playerActors[i];
                    if (entry.ActorId <= 0) continue;
                    try
                    {
                        _playerActorBindings?.Unbind(entry.PlayerId, entry.ActorId);
                    }
                    catch (Exception ex)
                    {
                        ReportRollbackException(
                            ex,
                            $"player-map playerId={entry.PlayerId.Value} actorId={entry.ActorId}");
                    }
                }
            }

            RollbackSpawnResults(built.SpawnResults);
        }

        private void RollbackSpawnResults(MobaActorSpawnResult[] spawnResults)
        {
            try
            {
                _actorSpawns?.RollbackBatch(spawnResults);
            }
            catch (Exception ex)
            {
                ReportRollbackException(ex, "actor-spawn-batch");
            }
        }

        private void TryRollbackSnapshots()
        {
            try
            {
                _snapshots?.Rollback();
            }
            catch (Exception ex)
            {
                ReportRollbackException(ex, "startup-snapshots");
            }
        }

        private void ReportRollbackException(Exception exception, string step)
        {
            ReportStartupException(
                exception,
                MobaBattleExceptionDomain.Bootstrap,
                "RollbackPreparedStart",
                MobaBattleExceptionSeverity.Critical,
                $"step={step}");
        }

        public void Dispose()
        {
        }
    }
}
