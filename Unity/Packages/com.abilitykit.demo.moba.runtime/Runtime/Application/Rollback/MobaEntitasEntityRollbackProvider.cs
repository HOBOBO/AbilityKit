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
    [MobaRollbackProvider(DefaultKey)]
    public sealed class MobaEntitasEntityRollbackProvider : IRollbackStructureRestoreProvider, IRollbackStatePreflightProvider
    {
        public const int DefaultKey = 10000;
        private readonly global::ActorContext _context;
        private readonly ActorIdAllocator _ids;
        private readonly MobaActorRegistry _actors;
        private readonly MobaEntityManager _entities;
        private readonly MobaSummonService _summons;
        private readonly MobaBrainService _brains;
        private readonly MobaActorRollbackCommandRuntime _commands;
        private readonly bool _allowMissingActorsRestoredByCommands;

        public MobaEntitasEntityRollbackProvider(global::ActorContext context, ActorIdAllocator ids,
            MobaActorRegistry actors, MobaEntityManager entities, MobaSummonService summons = null,
            bool allowMissingActorsRestoredByCommands = false, MobaBrainService brains = null)
            : this(context, ids, actors, entities, summons, allowMissingActorsRestoredByCommands, brains, null) { }

        internal MobaEntitasEntityRollbackProvider(global::ActorContext context, ActorIdAllocator ids,
            MobaActorRegistry actors, MobaEntityManager entities, MobaSummonService summons,
            bool allowMissingActorsRestoredByCommands, MobaBrainService brains, MobaActorRollbackCommandRuntime commands)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _ids = ids ?? throw new ArgumentNullException(nameof(ids));
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _entities = entities;
            _summons = summons;
            _brains = brains;
            _commands = commands;
            _allowMissingActorsRestoredByCommands = allowMissingActorsRestoredByCommands;
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
            var actorEntityVersions = new int[actorIds.Count];
            for (var i = 0; i < actorIds.Count; i++) actorEntityVersions[i] = _actors.GetEntityVersion(actorIds[i]);
            return MemoryPackSerializer.Serialize(new MobaEntitasEntityRollbackPayload(2, _ids.NextId,
                actorIds.ToArray(), castIds.ToArray(), actorEntityVersions));
        }

        public void ValidateImport(FrameIndex frame, byte[] payload)
        {
            var snapshot = Read(payload);
            ValidateRoster(snapshot);
            ValidateSummonDiscards(frame, new HashSet<int>(snapshot.ActorIds ?? Array.Empty<int>()));
        }

        private void ValidateRoster(MobaEntitasEntityRollbackPayload snapshot)
        {
            var actors = new HashSet<int>();
            var casts = new HashSet<long>();
            foreach (var entity in _context.GetEntities())
            {
                if (entity.hasActorId) actors.Add(entity.actorId.Value);
                if (entity.hasSkillCastInstanceId) casts.Add(entity.skillCastInstanceId.Value);
            }
            foreach (var id in snapshot.ActorIds ?? Array.Empty<int>())
                if (!_allowMissingActorsRestoredByCommands && !actors.Contains(id))
                    throw new InvalidOperationException($"Confirmed actor {id} was destroyed before rollback.");
            foreach (var id in snapshot.CastIds ?? Array.Empty<long>())
                if (!casts.Contains(id)) throw new InvalidOperationException($"Confirmed skill cast {id} was destroyed before rollback.");
        }

        private HashSet<int> ValidateSummonDiscards(FrameIndex frame, HashSet<int> actorIds)
        {
            if (_commands == null) return null;
            /*
             * 单份名单没有 checkpoint；至少重新验证本地 journal、出生帧和归属，
             * 不将类型或启用预测等同于静默销毁许可。完整快照另校验实际 checkpoint。
             */
            var candidates = _commands.FindLiveSummonDiscards(actorIds.Contains);
            _commands.ValidateLiveSummonBirths(frame, candidates, _summons);
            return candidates;
        }

        internal static HashSet<int> ReadSnapshotActorIds(byte[] payload)
        {
            var snapshot = Read(payload);
            var ids = new HashSet<int>();
            foreach (var id in snapshot.ActorIds ?? Array.Empty<int>())
                if (id <= 0 || !ids.Add(id))
                    throw new InvalidOperationException("Entitas entity rollback payload contains a duplicate or invalid actor identity.");
            return ids;
        }

        internal static Dictionary<int, int> ReadSnapshotActorEntityVersions(byte[] payload)
        {
            var snapshot = Read(payload);
            var versions = new Dictionary<int, int>();
            var actorIds = snapshot.ActorIds ?? Array.Empty<int>();
            var actorVersions = snapshot.ActorEntityVersions;
            if (actorVersions == null || actorVersions.Length != actorIds.Length)
                throw new InvalidOperationException("Entitas entity rollback payload omits actor entity versions.");
            for (var i = 0; i < actorIds.Length; i++)
            {
                if (actorVersions[i] <= 0 || !versions.TryAdd(actorIds[i], actorVersions[i]))
                    throw new InvalidOperationException("Entitas entity rollback payload contains invalid actor entity versions.");
            }
            return versions;
        }

        public void Import(FrameIndex frame, byte[] payload)
        {
            var snapshot = Read(payload);
            ValidateRoster(snapshot);
            var actorIds = new HashSet<int>(snapshot.ActorIds ?? Array.Empty<int>());
            var summonDiscards = ValidateSummonDiscards(frame, actorIds);
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
                    /*
                     * 已验证的预测出生不走正常停用，避免回滚触发 Brain 结束回调。
                     */
                    if (summonDiscards != null && summonDiscards.Contains(id))
                        _brains?.DiscardCreatedActorBrain(entity);
                    else
                        _brains?.DeactivateBrain(entity);
                    if (_entities != null && _entities.TryGetActorEntity(id, out var indexed) && ReferenceEquals(indexed, entity))
                        _entities.UnregisterSilently(id, out _, publishObjectLifecycle: false);
                    if (_actors.TryGetRegistered(id, out var registered) && ReferenceEquals(registered, entity))
                        _actors.Unregister(id);
                    _actors.ForgetEntityIdentity(id);
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
            if ((snapshot.Version != 1 && snapshot.Version != 2) || snapshot.NextActorId < 1)
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
        [MemoryPackOrder(4)] public readonly int[] ActorEntityVersions;

        [MemoryPackConstructor]
        public MobaEntitasEntityRollbackPayload(int version, int nextActorId, int[] actorIds, long[] castIds,
            int[] actorEntityVersions)
        {
            Version = version;
            NextActorId = nextActorId;
            ActorIds = actorIds;
            CastIds = castIds;
            ActorEntityVersions = actorEntityVersions;
        }

        public MobaEntitasEntityRollbackPayload(int version, int nextActorId, int[] actorIds, long[] castIds)
            : this(version, nextActorId, actorIds, castIds, null) { }
    }
}
