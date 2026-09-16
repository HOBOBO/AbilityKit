using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityManager;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Rollback
{
    // Restore before component and service providers: predicted spawns must not remain indexed.
    public sealed class MobaEntitasEntityRollbackProvider : IRollbackStateProvider, IRollbackStatePreflightProvider
    {
        public const int DefaultKey = 10000;
        private readonly global::ActorContext _context;
        private readonly ActorIdAllocator _ids;
        private readonly MobaActorRegistry _actors;
        private readonly MobaEntityManager _entities;
        private readonly MobaSummonService _summons;

        public MobaEntitasEntityRollbackProvider(global::ActorContext context, ActorIdAllocator ids,
            MobaActorRegistry actors, MobaEntityManager entities, MobaSummonService summons = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _ids = ids ?? throw new ArgumentNullException(nameof(ids));
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _entities = entities;
            _summons = summons;
        }

        public int Key => DefaultKey;

        public byte[] Export(FrameIndex frame)
        {
            var actorIds = new List<int>();
            var castIds = new List<long>();
            foreach (var entity in _context.GetEntities())
            {
                if (entity.hasActorId) actorIds.Add(entity.actorId.Value);
                if (entity.hasSkillCastInstanceId) castIds.Add(entity.skillCastInstanceId.Value);
            }
            actorIds.Sort();
            castIds.Sort();
            return MemoryPackSerializer.Serialize(new MobaEntitasEntityRollbackPayload(1, _ids.NextId,
                actorIds.ToArray(), castIds.ToArray()));
        }

        public void ValidateImport(FrameIndex frame, byte[] payload)
        {
            var snapshot = Read(payload);
            var actors = new HashSet<int>();
            var casts = new HashSet<long>();
            foreach (var entity in _context.GetEntities())
            {
                if (entity.hasActorId) actors.Add(entity.actorId.Value);
                if (entity.hasSkillCastInstanceId) casts.Add(entity.skillCastInstanceId.Value);
            }
            foreach (var id in snapshot.ActorIds ?? Array.Empty<int>())
                if (!actors.Contains(id)) throw new InvalidOperationException($"Confirmed actor {id} was destroyed before rollback.");
            foreach (var id in snapshot.CastIds ?? Array.Empty<long>())
                if (!casts.Contains(id)) throw new InvalidOperationException($"Confirmed skill cast {id} was destroyed before rollback.");
        }

        public void Import(FrameIndex frame, byte[] payload)
        {
            var snapshot = Read(payload);
            ValidateImport(frame, payload);
            var actorIds = new HashSet<int>(snapshot.ActorIds ?? Array.Empty<int>());
            var castIds = new HashSet<long>(snapshot.CastIds ?? Array.Empty<long>());
            var remove = new List<global::ActorEntity>();
            foreach (var entity in _context.GetEntities())
            {
                if (entity.hasActorId && !actorIds.Contains(entity.actorId.Value) ||
                    entity.hasSkillCastInstanceId && !castIds.Contains(entity.skillCastInstanceId.Value))
                    remove.Add(entity);
            }
            foreach (var entity in remove)
            {
                if (entity.hasActorId)
                {
                    var id = entity.actorId.Value;
                    if (_entities != null && _entities.TryGetActorEntity(id, out var indexed) && ReferenceEquals(indexed, entity))
                        _entities.UnregisterSilently(id, out _);
                    if (_actors.TryGetRegistered(id, out var registered) && ReferenceEquals(registered, entity))
                        _actors.Unregister(id);
                }
                if (entity.isEnabled) entity.Destroy();
            }
            _summons?.PruneRollbackActors(actorIds);
            _ids.Reset(snapshot.NextActorId);
        }

        private static MobaEntitasEntityRollbackPayload Read(byte[] payload)
        {
            if (payload == null || payload.Length == 0) throw new InvalidOperationException("Missing Entitas entity rollback payload.");
            var snapshot = MemoryPackSerializer.Deserialize<MobaEntitasEntityRollbackPayload>(payload);
            if (snapshot.Version != 1 || snapshot.NextActorId < 1)
                throw new InvalidOperationException("Unsupported Entitas entity rollback payload.");
            return snapshot;
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaEntitasEntityRollbackPayload
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly int NextActorId;
        [MemoryPackOrder(2)] public readonly int[] ActorIds;
        [MemoryPackOrder(3)] public readonly long[] CastIds;

        [MemoryPackConstructor]
        public MobaEntitasEntityRollbackPayload(int version, int nextActorId, int[] actorIds, long[] castIds)
        {
            Version = version;
            NextActorId = nextActorId;
            ActorIds = actorIds;
            CastIds = castIds;
        }
    }
}
