using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Util.Converter;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Services
{
    public enum MobaHeroReplacementFailureStage
    {
        None = 0,
        Validation = 1,
        Spawn = 2,
        SnapshotPrecommit = 3,
        Commit = 4,
    }

    public readonly struct MobaHeroReplacementRequest
    {
        public readonly PlayerId Player;
        public readonly FrameIndex Frame;
        public readonly int PreviousActorId;
        public readonly global::ActorEntity PreviousActor;
        public readonly MobaPlayerLoadout Loadout;

        public MobaHeroReplacementRequest(
            PlayerId player,
            FrameIndex frame,
            int previousActorId,
            global::ActorEntity previousActor,
            in MobaPlayerLoadout loadout)
        {
            Player = player;
            Frame = frame;
            PreviousActorId = previousActorId;
            PreviousActor = previousActor;
            Loadout = loadout;
        }
    }

    public readonly struct MobaHeroReplacementResult
    {
        public readonly bool Success;
        public readonly int PreviousActorId;
        public readonly int ActorId;
        public readonly MobaHeroReplacementFailureStage FailureStage;
        public readonly string Error;

        private MobaHeroReplacementResult(
            bool success,
            int previousActorId,
            int actorId,
            MobaHeroReplacementFailureStage failureStage,
            string error)
        {
            Success = success;
            PreviousActorId = previousActorId;
            ActorId = actorId;
            FailureStage = failureStage;
            Error = error;
        }

        public static MobaHeroReplacementResult Accepted(int previousActorId, int actorId)
        {
            return new MobaHeroReplacementResult(true, previousActorId, actorId, MobaHeroReplacementFailureStage.None, null);
        }

        public static MobaHeroReplacementResult Failed(
            int previousActorId,
            int actorId,
            MobaHeroReplacementFailureStage failureStage,
            string error)
        {
            return new MobaHeroReplacementResult(false, previousActorId, actorId, failureStage, error);
        }
    }

    public readonly struct MobaHeroReplacementSnapshotBatch
    {
        public readonly MobaActorSpawnSnapshotEntry SpawnEntry;
        public readonly MobaPlayerHeroChangedSnapshotEntry ChangedEntry;
        public readonly byte[] SpawnPayload;
        public readonly byte[] ChangedPayload;

        public MobaHeroReplacementSnapshotBatch(
            in MobaActorSpawnSnapshotEntry spawnEntry,
            in MobaPlayerHeroChangedSnapshotEntry changedEntry,
            byte[] spawnPayload,
            byte[] changedPayload)
        {
            SpawnEntry = spawnEntry;
            ChangedEntry = changedEntry;
            SpawnPayload = spawnPayload;
            ChangedPayload = changedPayload;
        }
    }

    public interface IMobaHeroReplacementSnapshotPrecommit : IService
    {
        bool TryPrepare(
            in MobaActorSpawnSnapshotEntry spawnEntry,
            in MobaPlayerHeroChangedSnapshotEntry changedEntry,
            out MobaHeroReplacementSnapshotBatch batch,
            out string error);

        void Commit(in MobaHeroReplacementSnapshotBatch batch);
    }

    [WorldService(typeof(MobaHeroReplacementSnapshotPrecommitService))]
    [WorldService(typeof(IMobaHeroReplacementSnapshotPrecommit))]
    public sealed class MobaHeroReplacementSnapshotPrecommitService : IMobaHeroReplacementSnapshotPrecommit
    {
        private readonly MobaActorSpawnSnapshotService _spawnSnapshots;
        private readonly MobaPlayerHeroChangedSnapshotService _changedSnapshots;

        public MobaHeroReplacementSnapshotPrecommitService(
            MobaActorSpawnSnapshotService spawnSnapshots,
            MobaPlayerHeroChangedSnapshotService changedSnapshots)
        {
            _spawnSnapshots = spawnSnapshots ?? throw new ArgumentNullException(nameof(spawnSnapshots));
            _changedSnapshots = changedSnapshots ?? throw new ArgumentNullException(nameof(changedSnapshots));
        }

        public bool TryPrepare(
            in MobaActorSpawnSnapshotEntry spawnEntry,
            in MobaPlayerHeroChangedSnapshotEntry changedEntry,
            out MobaHeroReplacementSnapshotBatch batch,
            out string error)
        {
            batch = default;
            error = null;
            if (spawnEntry.NetId <= 0)
            {
                error = "replacement spawn snapshot actor id must be positive";
                return false;
            }

            if (changedEntry.ActorId != spawnEntry.NetId ||
                changedEntry.PreviousActorId <= 0 ||
                string.IsNullOrEmpty(changedEntry.PlayerId))
            {
                error = "replacement hero changed snapshot is inconsistent";
                return false;
            }

            try
            {
                var spawnPayload = MobaActorSpawnSnapshotCodec.Serialize(new[] { spawnEntry });
                var changedPayload = MobaPlayerHeroChangedSnapshotCodec.Serialize(new[] { changedEntry });
                if (spawnPayload == null || spawnPayload.Length == 0 ||
                    changedPayload == null || changedPayload.Length == 0)
                {
                    error = "replacement snapshot serialization returned an empty payload";
                    return false;
                }

                batch = new MobaHeroReplacementSnapshotBatch(
                    in spawnEntry,
                    in changedEntry,
                    spawnPayload,
                    changedPayload);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        public void Commit(in MobaHeroReplacementSnapshotBatch batch)
        {
            _spawnSnapshots.Enqueue(batch.SpawnEntry);
            _changedSnapshots.Enqueue(batch.ChangedEntry);
        }

        public void Dispose()
        {
        }
    }

    public interface IMobaHeroReplacementTransactionService : IService
    {
        bool TryReplace(
            in MobaHeroReplacementRequest request,
            out MobaHeroReplacementResult result);
    }

    [WorldService(typeof(MobaHeroReplacementTransactionService))]
    [WorldService(typeof(IMobaHeroReplacementTransactionService))]
    public sealed class MobaHeroReplacementTransactionService : IMobaHeroReplacementTransactionService
    {
        private readonly MobaPlayerActorMapService _playerActorMap;
        private readonly MobaEntityManager _entities;
        private readonly MobaActorRegistry _registry;
        private readonly IMobaActorSpawnService _actorSpawn;
        private readonly ActorEntityInitPipeline _initializer;
        private readonly IMobaHeroReplacementSnapshotPrecommit _snapshots;

        public MobaHeroReplacementTransactionService(
            MobaPlayerActorMapService playerActorMap,
            MobaEntityManager entities,
            MobaActorRegistry registry,
            IMobaActorSpawnService actorSpawn,
            ActorEntityInitPipeline initializer,
            IMobaHeroReplacementSnapshotPrecommit snapshots)
        {
            _playerActorMap = playerActorMap ?? throw new ArgumentNullException(nameof(playerActorMap));
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _actorSpawn = actorSpawn ?? throw new ArgumentNullException(nameof(actorSpawn));
            _initializer = initializer ?? throw new ArgumentNullException(nameof(initializer));
            _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        }

        public bool TryReplace(
            in MobaHeroReplacementRequest request,
            out MobaHeroReplacementResult result)
        {
            if (!TryValidate(in request, out var validationError))
            {
                result = MobaHeroReplacementResult.Failed(
                    request.PreviousActorId,
                    0,
                    MobaHeroReplacementFailureStage.Validation,
                    validationError);
                return false;
            }

            if (!IsCurrentPlayerActor(in request))
            {
                result = MobaHeroReplacementResult.Failed(
                    request.PreviousActorId,
                    0,
                    MobaHeroReplacementFailureStage.Validation,
                    "player actor mapping changed before replacement started");
                return false;
            }

            var loadout = request.Loadout;
            var spec = MobaConverter.ToActorBuildSpec(actorId: 0, in loadout);
            var spawnRequest = MobaActorSpawnRequest.FromSpec(in spec);
            spawnRequest.AllocateActorIdIfMissing = true;
            spawnRequest.Initializer = (entity, _) => InitializeLoadoutOrThrow(entity, in loadout);
            if (!_actorSpawn.TrySpawn(in spawnRequest, out var spawnResult) || !spawnResult.Success)
            {
                result = MobaHeroReplacementResult.Failed(
                    request.PreviousActorId,
                    0,
                    MobaHeroReplacementFailureStage.Spawn,
                    string.IsNullOrEmpty(spawnResult.Error) ? "replacement hero spawn failed" : spawnResult.Error);
                return false;
            }

            var position = request.PreviousActor.transform.Value.Position;
            var spawnEntry = new MobaActorSpawnSnapshotEntry(
                spawnResult.ActorId,
                (int)SpawnEntityKind.Character,
                request.Loadout.HeroId,
                spawnResult.ActorId,
                position.X,
                position.Y,
                position.Z);
            var changedEntry = new MobaPlayerHeroChangedSnapshotEntry(
                request.Player.Value,
                request.PreviousActorId,
                spawnResult.ActorId,
                request.Loadout.TeamId,
                request.Loadout.HeroId,
                request.Loadout.AttributeTemplateId,
                request.Loadout.Level,
                request.Loadout.BasicAttackSkillId,
                request.Loadout.SkillIds);

            if (!_snapshots.TryPrepare(in spawnEntry, in changedEntry, out var snapshotBatch, out var snapshotError))
            {
                CompensateSpawn(in spawnResult);
                result = MobaHeroReplacementResult.Failed(
                    request.PreviousActorId,
                    spawnResult.ActorId,
                    MobaHeroReplacementFailureStage.SnapshotPrecommit,
                    snapshotError ?? "replacement snapshot precommit failed");
                return false;
            }

            if (!IsCurrentPlayerActor(in request))
            {
                CompensateSpawn(in spawnResult);
                result = MobaHeroReplacementResult.Failed(
                    request.PreviousActorId,
                    spawnResult.ActorId,
                    MobaHeroReplacementFailureStage.Commit,
                    "player actor mapping changed during replacement preparation");
                return false;
            }

            _playerActorMap.Bind(request.Player, spawnResult.ActorId);
            _snapshots.Commit(in snapshotBatch);
            ActorLifecycleRequests.RequestDespawn(
                request.PreviousActor,
                request.Frame.Value,
                ActorDespawnReason.HeroReplaced,
                spawnResult.ActorId);

            result = MobaHeroReplacementResult.Accepted(request.PreviousActorId, spawnResult.ActorId);
            return true;
        }

        private static bool TryValidate(in MobaHeroReplacementRequest request, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(request.Player.Value))
            {
                error = "player id is required";
                return false;
            }

            if (request.PreviousActorId <= 0 || request.PreviousActor == null ||
                !request.PreviousActor.isEnabled || !request.PreviousActor.hasTransform)
            {
                error = "previous actor is unavailable";
                return false;
            }

            if (request.Loadout.HeroId <= 0)
            {
                error = "replacement hero id must be positive";
                return false;
            }

            return true;
        }

        private bool IsCurrentPlayerActor(in MobaHeroReplacementRequest request)
        {
            return _playerActorMap.TryGetActorId(request.Player, out var actorId) &&
                   actorId == request.PreviousActorId;
        }

        private void InitializeLoadoutOrThrow(
            global::ActorEntity entity,
            in MobaPlayerLoadout loadout)
        {
            if (!_initializer.TryInitializeFromLoadout(entity, in loadout, out var error))
            {
                throw new InvalidOperationException(error ?? "replacement hero loadout initialization failed");
            }
        }

        private void CompensateSpawn(in MobaActorSpawnResult spawnResult)
        {
            new MobaActorSpawnRegistrar(_registry, _entities).Unregister(
                spawnResult.ActorId,
                out _,
                publishDespawn: false);
            ActorSpawnPipeline.DestroyBuiltEntity(spawnResult.Entity);
        }

        public void Dispose()
        {
        }
    }
}
