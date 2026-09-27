using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Services.EntityManager;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Rollback
{
    /// <summary>
    /// Owns the byte-command journal used to reverse actor create/destroy operations.
    /// Field providers remain responsible for restoring actor state after the skeleton exists.
    /// </summary>
    public sealed class MobaActorRollbackCommandRuntime
    {
        public const int DestroyCreatedActorCommand = 0x4D4F0101;
        public const int RestoreDestroyedActorCommand = 0x4D4F0102;
        public const int PayloadVersion = 2;

        private readonly global::ActorContext _context;
        private readonly MobaActorRegistry _actors;
        private readonly MobaEntityManager _entities;
        private readonly IFrameTime _frameTime;
        private readonly Dictionary<int, MobaEntityKind> _actorKinds =
            new Dictionary<int, MobaEntityKind>();

        public MobaActorRollbackCommandRuntime(
            global::ActorContext context,
            MobaActorRegistry actors,
            MobaEntityManager entities,
            IFrameTime frameTime)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
            _frameTime = frameTime ?? throw new ArgumentNullException(nameof(frameTime));

            Log = new CommandRollbackLog();
            var handlers = new RollbackCommandHandlerRegistry();
            handlers.Register(new DestroyCreatedActorHandler(this));
            handlers.Register(new RestoreDestroyedActorHandler(this));
            handlers.Seal();
            StateProvider = new CommandRollbackStateProvider(Log, handlers);

            foreach (var entry in actors.Entries)
            {
                if (entry.Key > 0 && entry.Value != null)
                    _actorKinds[entry.Key] = ResolveKind(entry.Value);
            }
        }

        public CommandRollbackLog Log { get; }

        public CommandRollbackStateProvider StateProvider { get; }

        internal bool Matches(
            global::ActorContext context,
            MobaActorRegistry actors,
            IFrameTime frameTime)
        {
            return ReferenceEquals(_context, context) &&
                   ReferenceEquals(_actors, actors) &&
                   ReferenceEquals(_frameTime, frameTime);
        }

        internal void RecordCreated(global::ActorEntity entity, MobaEntityKind kind)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (!entity.hasActorId || entity.actorId.Value <= 0)
                throw new InvalidOperationException("A rollback-tracked actor must have a valid ActorId.");
            if (kind == MobaEntityKind.Unknown)
                throw new InvalidOperationException("A rollback-tracked actor must have a concrete entity kind.");

            var actorId = entity.actorId.Value;
            var payload = MemoryPackSerializer.Serialize(new MobaActorRollbackIdPayload(actorId));
            Log.Record(_frameTime.Frame, DestroyCreatedActorCommand, PayloadVersion, payload);
            _actorKinds[actorId] = kind;
        }

        internal byte[] CaptureDestroyed(global::ActorEntity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (!entity.hasActorId || entity.actorId.Value <= 0 ||
                !entity.hasTransform || !entity.hasTeam || !entity.hasEntityMainType ||
                !entity.hasUnitSubType || !entity.hasOwnerPlayerId)
            {
                throw new InvalidOperationException(
                    "A rollback-tracked actor is missing identity or transform components.");
            }

            var actorId = entity.actorId.Value;
            if (!_actorKinds.TryGetValue(actorId, out var kind)) kind = ResolveKind(entity);
            if (kind == MobaEntityKind.Unknown)
                throw new InvalidOperationException($"Cannot resolve rollback entity kind for actor {actorId}.");

            var tombstone = new MobaActorRollbackTombstone(
                actorId,
                kind,
                entity.transform.Value,
                entity.team.Value,
                entity.entityMainType.Value,
                entity.unitSubType.Value,
                entity.ownerPlayerId.Value.Value,
                entity.hasModelId ? entity.modelId.Value : 0,
                entity.hasOwnerLink,
                entity.hasOwnerLink ? entity.ownerLink.OwnerActorId : 0,
                entity.hasOwnerLink ? entity.ownerLink.RootOwnerActorId : 0,
                _actors.GetEntityVersion(actorId));
            return MemoryPackSerializer.Serialize(tombstone);
        }

        internal void RecordDestroyed(int actorId, byte[] payload)
        {
            Log.Record(_frameTime.Frame, RestoreDestroyedActorCommand, PayloadVersion, payload);
            _actorKinds.Remove(actorId);
        }

        internal void Clear()
        {
            Log.Clear();
            _actorKinds.Clear();
        }

        private static MobaEntityKind ResolveKind(global::ActorEntity entity)
        {
            if (entity == null) return MobaEntityKind.Unknown;
            if (entity.hasProjectileLauncher) return MobaEntityKind.ProjectileLauncher;
            if (entity.isFlyingProjectileTag ||
                entity.hasEntityMainType && entity.entityMainType.Value == EntityMainType.Projectile)
                return MobaEntityKind.Projectile;
            if (entity.hasSummonMeta ||
                entity.hasEntityMainType && entity.entityMainType.Value == EntityMainType.Summon)
                return MobaEntityKind.Summon;
            if (entity.hasEntityMainType &&
                (entity.entityMainType.Value == EntityMainType.SceneObject ||
                 entity.entityMainType.Value == EntityMainType.Effect))
                return MobaEntityKind.Area;
            if (!entity.hasEntityMainType || !entity.hasUnitSubType) return MobaEntityKind.Unknown;
            return ActorArchetypeFactory.CreateKindFromType(
                entity.entityMainType.Value,
                entity.unitSubType.Value);
        }

        private static MobaActorRollbackIdPayload ReadId(int payloadVersion, ReadOnlyMemory<byte> payload)
        {
            EnsureVersion(payloadVersion);
            var value = MemoryPackSerializer.Deserialize<MobaActorRollbackIdPayload>(payload.ToArray());
            if (value.ActorId <= 0) throw new InvalidOperationException("Invalid actor rollback id payload.");
            return value;
        }

        private static MobaActorRollbackTombstone ReadTombstone(
            int payloadVersion,
            ReadOnlyMemory<byte> payload)
        {
            EnsureVersion(payloadVersion);
            var value = MemoryPackSerializer.Deserialize<MobaActorRollbackTombstone>(payload.ToArray());
            if (value.ActorId <= 0 || value.EntityVersion <= 0 || value.Kind == MobaEntityKind.Unknown ||
                !Enum.IsDefined(typeof(MobaEntityKind), value.Kind))
            {
                throw new InvalidOperationException("Invalid actor rollback tombstone payload.");
            }
            return value;
        }

        private static void EnsureVersion(int payloadVersion)
        {
            if (payloadVersion != PayloadVersion)
                throw new InvalidOperationException($"Unsupported MOBA actor rollback payload version: {payloadVersion}");
        }

        private void ValidateRegistrationConsistency(int actorId)
        {
            var actorRegistered = _actors.TryGetRegistered(actorId, out var actor);
            var entityRegistered = _entities.TryGetActorEntity(actorId, out var indexed);
            if (actorRegistered != entityRegistered ||
                actorRegistered && !ReferenceEquals(actor, indexed))
            {
                throw new InvalidOperationException(
                    $"Actor {actorId} rollback registration indexes are inconsistent.");
            }
        }

        private void DestroyCreatedActor(int actorId)
        {
            ValidateRegistrationConsistency(actorId);
            if (!_actors.TryGetRegistered(actorId, out var entity))
            {
                _actorKinds.Remove(actorId);
                return;
            }

            _entities.UnregisterSilently(actorId, out _, publishObjectLifecycle: false);
            _actors.Unregister(actorId);
            _actors.ForgetEntityIdentity(actorId);
            _actorKinds.Remove(actorId);
            if (entity.isEnabled) entity.Destroy();
        }

        private void RestoreDestroyedActor(in MobaActorRollbackTombstone tombstone)
        {
            ValidateRegistrationConsistency(tombstone.ActorId);
            if (_actors.TryGetRegistered(tombstone.ActorId, out _))
                throw new InvalidOperationException($"Actor {tombstone.ActorId} already exists during rollback restore.");

            var transform = tombstone.Transform;
            var info = new MobaEntityInfo(
                tombstone.ActorId,
                tombstone.Kind,
                in transform,
                tombstone.Team,
                tombstone.MainType,
                tombstone.UnitSubType,
                new PlayerId(tombstone.OwnerPlayerId),
                tombstone.ModelId);
            var entity = ActorArchetypeFactory.Create(_context, in info);
            var actorRegistered = false;
            var entityRegistered = false;
            try
            {
                if (tombstone.ModelId != 0) entity.AddModelId(tombstone.ModelId);
                if (tombstone.HasOwnerLink)
                    entity.AddOwnerLink(tombstone.OwnerActorId, tombstone.RootOwnerActorId);

                _actors.RegisterRestored(tombstone.ActorId, entity, tombstone.EntityVersion);
                actorRegistered = true;
                entityRegistered = _entities.RegisterSilently(
                    tombstone.ActorId,
                    entity,
                    tombstone.Team,
                    tombstone.MainType,
                    tombstone.UnitSubType,
                    new PlayerId(tombstone.OwnerPlayerId),
                    publishObjectLifecycle: false);
                if (!entityRegistered)
                    throw new InvalidOperationException($"Actor {tombstone.ActorId} was not newly indexed during rollback restore.");
                _actorKinds[tombstone.ActorId] = tombstone.Kind;
            }
            catch
            {
                if (entityRegistered)
                    _entities.UnregisterSilently(
                        tombstone.ActorId,
                        out _,
                        publishObjectLifecycle: false);
                if (actorRegistered) _actors.Unregister(tombstone.ActorId);
                if (entity.isEnabled) entity.Destroy();
                throw;
            }
        }

        private sealed class DestroyCreatedActorHandler : IRollbackCommandHandler
        {
            private readonly MobaActorRollbackCommandRuntime _runtime;

            public DestroyCreatedActorHandler(MobaActorRollbackCommandRuntime runtime) => _runtime = runtime;

            public int CommandType => DestroyCreatedActorCommand;

            public bool CanRollback(int payloadVersion) => payloadVersion == PayloadVersion;

            public void ValidateRollback(
                in RollbackCommandContext context,
                int payloadVersion,
                ReadOnlyMemory<byte> payload)
            {
                var value = ReadId(payloadVersion, payload);
                _runtime.ValidateRegistrationConsistency(value.ActorId);
            }

            public void Rollback(
                in RollbackCommandContext context,
                int payloadVersion,
                ReadOnlyMemory<byte> payload)
            {
                var value = ReadId(payloadVersion, payload);
                _runtime.DestroyCreatedActor(value.ActorId);
            }
        }

        private sealed class RestoreDestroyedActorHandler : IRollbackCommandHandler
        {
            private readonly MobaActorRollbackCommandRuntime _runtime;

            public RestoreDestroyedActorHandler(MobaActorRollbackCommandRuntime runtime) => _runtime = runtime;

            public int CommandType => RestoreDestroyedActorCommand;

            public bool CanRollback(int payloadVersion) => payloadVersion == PayloadVersion;

            public void ValidateRollback(
                in RollbackCommandContext context,
                int payloadVersion,
                ReadOnlyMemory<byte> payload)
            {
                var value = ReadTombstone(payloadVersion, payload);
                _runtime.ValidateRegistrationConsistency(value.ActorId);
            }

            public void Rollback(
                in RollbackCommandContext context,
                int payloadVersion,
                ReadOnlyMemory<byte> payload)
            {
                var value = ReadTombstone(payloadVersion, payload);
                _runtime.RestoreDestroyedActor(in value);
            }
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaActorRollbackIdPayload
    {
        [MemoryPackOrder(0)] public readonly int ActorId;

        public MobaActorRollbackIdPayload(int actorId) => ActorId = actorId;
    }

    [MemoryPackable]
    public readonly partial struct MobaActorRollbackTombstone
    {
        [MemoryPackOrder(0)] public readonly int ActorId;
        [MemoryPackOrder(1)] public readonly MobaEntityKind Kind;
        [MemoryPackOrder(2)] public readonly Transform3 Transform;
        [MemoryPackOrder(3)] public readonly Team Team;
        [MemoryPackOrder(4)] public readonly EntityMainType MainType;
        [MemoryPackOrder(5)] public readonly UnitSubType UnitSubType;
        [MemoryPackOrder(6)] public readonly string OwnerPlayerId;
        [MemoryPackOrder(7)] public readonly int ModelId;
        [MemoryPackOrder(8)] public readonly bool HasOwnerLink;
        [MemoryPackOrder(9)] public readonly int OwnerActorId;
        [MemoryPackOrder(10)] public readonly int RootOwnerActorId;
        [MemoryPackOrder(11)] public readonly int EntityVersion;

        [MemoryPackConstructor]
        public MobaActorRollbackTombstone(
            int actorId,
            MobaEntityKind kind,
            Transform3 transform,
            Team team,
            EntityMainType mainType,
            UnitSubType unitSubType,
            string ownerPlayerId,
            int modelId,
            bool hasOwnerLink,
            int ownerActorId,
            int rootOwnerActorId,
            int entityVersion)
        {
            ActorId = actorId;
            Kind = kind;
            Transform = transform;
            Team = team;
            MainType = mainType;
            UnitSubType = unitSubType;
            OwnerPlayerId = ownerPlayerId;
            ModelId = modelId;
            HasOwnerLink = hasOwnerLink;
            OwnerActorId = ownerActorId;
            RootOwnerActorId = rootOwnerActorId;
            EntityVersion = entityVersion;
        }
    }
}
