using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Demo.Moba.Components;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Rollback
{
    [MobaRollbackProvider(DefaultKey)]
    public sealed class MobaEntitasComponentRollbackProvider : IRollbackStateProvider, IRollbackStatePreflightProvider
    {
        public const int DefaultKey = 10018;
        private const int ComponentMetadataMagic = 0x4D434D31;
        private const byte FlyingProjectileFlag = 1;
        private const byte ProjectileLauncherFlag = 2;
        private const byte SummonMetaFlag = 4;
        private readonly global::ActorContext _context;
        private readonly bool _allowMissingActorsRestoredByCommands;

        public MobaEntitasComponentRollbackProvider(
            global::ActorContext context,
            bool allowMissingActorsRestoredByCommands = false)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _allowMissingActorsRestoredByCommands = allowMissingActorsRestoredByCommands;
        }

        public int Key => DefaultKey;

        public byte[] Export(FrameIndex frame)
        {
            var entries = new List<MobaEntitasComponentRollbackEntry>();
            foreach (var entity in _context.GetEntities())
            {
                if (!entity.hasActorId && !entity.hasSkillCastInstanceId) continue;
                using (var stream = new MemoryStream())
                using (var writer = new BinaryWriter(stream))
                {
                    Write(entity, writer);
                    entries.Add(new MobaEntitasComponentRollbackEntry(
                        entity.hasActorId ? entity.actorId.Value : 0,
                        entity.hasSkillCastInstanceId ? entity.skillCastInstanceId.Value : 0,
                        stream.ToArray()));
                }
            }
            entries.Sort((a, b) => a.ActorId != b.ActorId
                ? a.ActorId.CompareTo(b.ActorId) : a.CastId.CompareTo(b.CastId));
            return MemoryPackSerializer.Serialize(new MobaEntitasComponentRollbackPayload(1, entries.ToArray()));
        }

        public void ValidateImport(FrameIndex frame, byte[] payload)
        {
            var snapshot = Read(payload);
            var byActor = new HashSet<int>();
            var byCast = new HashSet<long>();
            foreach (var entity in _context.GetEntities())
            {
                if (entity.hasActorId) byActor.Add(entity.actorId.Value);
                if (entity.hasSkillCastInstanceId) byCast.Add(entity.skillCastInstanceId.Value);
            }
            foreach (var entry in snapshot.Entries ?? Array.Empty<MobaEntitasComponentRollbackEntry>())
            {
                if (entry.Data == null || entry.Data.Length == 0 ||
                    entry.ActorId > 0 && !_allowMissingActorsRestoredByCommands && !byActor.Contains(entry.ActorId) ||
                    entry.CastId > 0 && !byCast.Contains(entry.CastId))
                    throw new InvalidOperationException($"Missing or invalid Entitas component snapshot for actor {entry.ActorId}, cast {entry.CastId}.");
            }
        }

        internal static MobaEntitasComponentSnapshotSummary[] ReadSnapshotSummaries(byte[] payload)
        {
            var snapshot = Read(payload);
            var entries = snapshot.Entries ?? Array.Empty<MobaEntitasComponentRollbackEntry>();
            var summaries = new MobaEntitasComponentSnapshotSummary[entries.Length];
            var actorIds = new HashSet<int>();
            var castIds = new HashSet<long>();
            var temporaryContext = new global::ActorContext();
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.ActorId <= 0 && entry.CastId <= 0 || entry.Data == null || entry.Data.Length == 0 ||
                    entry.ActorId > 0 && !actorIds.Add(entry.ActorId) ||
                    entry.ActorId == 0 && !castIds.Add(entry.CastId))
                    throw new InvalidOperationException("Entitas component rollback payload contains a duplicate or invalid identity.");

                var temporary = temporaryContext.CreateEntity();
                try
                {
                    var metadata = Restore(temporary, entry.Data);
                    if (temporary.hasActorId || temporary.hasSkillCastInstanceId)
                        throw new InvalidOperationException("Entitas component data must not redefine rollback identity.");
                    summaries[i] = new MobaEntitasComponentSnapshotSummary(
                        entry.ActorId,
                        entry.CastId,
                        metadata.HasMetadata,
                        metadata.HasMetadata ? (metadata.Flags & FlyingProjectileFlag) != 0 : temporary.isFlyingProjectileTag,
                        metadata.HasMetadata ? (metadata.Flags & ProjectileLauncherFlag) != 0 : temporary.hasProjectileLauncher,
                        metadata.HasMetadata ? metadata.LauncherProjectileId : temporary.hasProjectileLauncher ? temporary.projectileLauncher.ProjectileId : 0,
                        metadata.HasMetadata ? (metadata.Flags & SummonMetaFlag) != 0 : temporary.hasSummonMeta,
                        metadata.HasMetadata ? metadata.SummonId : temporary.hasSummonMeta ? temporary.summonMeta.SummonId : 0);
                }
                finally
                {
                    temporary.Destroy();
                }
            }
            return summaries;
        }

        public void Import(FrameIndex frame, byte[] payload)
        {
            var snapshot = Read(payload);
            foreach (var entry in snapshot.Entries ?? Array.Empty<MobaEntitasComponentRollbackEntry>())
            {
                global::ActorEntity target = null;
                foreach (var entity in _context.GetEntities())
                {
                    if (entry.ActorId > 0 && entity.hasActorId && entity.actorId.Value == entry.ActorId ||
                        entry.ActorId == 0 && entity.hasSkillCastInstanceId && entity.skillCastInstanceId.Value == entry.CastId)
                    {
                        target = entity;
                        break;
                    }
                }
                if (target == null) throw new InvalidOperationException($"Entitas actor {entry.ActorId}, cast {entry.CastId} is missing.");
                Restore(target, entry.Data);
            }
        }

        private static void Write(global::ActorEntity e, BinaryWriter w)
        {
            // 逐字段序列化由生成器产出，覆盖 pin 配置中标记 serialize:true 的组件。
            global::ActorComponentSerialization.Write(e, w);
            var flags = (byte)0;
            if (e.isFlyingProjectileTag) flags |= FlyingProjectileFlag;
            var launcherProjectileId = e.hasProjectileLauncher ? e.projectileLauncher.ProjectileId : 0;
            if (e.hasProjectileLauncher) flags |= ProjectileLauncherFlag;
            var summonId = e.hasSummonMeta ? e.summonMeta.SummonId : 0;
            if (e.hasSummonMeta) flags |= SummonMetaFlag;
            w.Write(ComponentMetadataMagic);
            w.Write(flags);
            w.Write(launcherProjectileId);
            w.Write(summonId);
        }

        private static ComponentMetadata Restore(global::ActorEntity e, byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new InvalidOperationException("Missing Entitas component snapshot data.");
            using (var stream = new MemoryStream(data, false))
            using (var r = new BinaryReader(stream))
            {
                global::ActorComponentSerialization.Restore(e, r);
                var remaining = r.BaseStream.Length - r.BaseStream.Position;
                if (remaining == 0) return default;
                if (remaining != sizeof(int) + sizeof(byte) + sizeof(int) + sizeof(int))
                    throw new InvalidOperationException("Unexpected Entitas component snapshot trailing data.");
                if (r.ReadInt32() != ComponentMetadataMagic)
                    throw new InvalidOperationException("Unsupported Entitas component metadata.");
                var flags = r.ReadByte();
                if ((flags & ~(FlyingProjectileFlag | ProjectileLauncherFlag | SummonMetaFlag)) != 0)
                    throw new InvalidOperationException("Invalid Entitas component metadata flags.");
                return new ComponentMetadata(true, flags, r.ReadInt32(), r.ReadInt32());
            }
        }

        private readonly struct ComponentMetadata
        {
            public ComponentMetadata(bool hasMetadata, byte flags, int launcherProjectileId, int summonId)
            { HasMetadata = hasMetadata; Flags = flags; LauncherProjectileId = launcherProjectileId; SummonId = summonId; }
            public bool HasMetadata { get; }
            public byte Flags { get; }
            public int LauncherProjectileId { get; }
            public int SummonId { get; }
        }

        private static MobaEntitasComponentRollbackPayload Read(byte[] payload)
        {
            if (payload == null || payload.Length == 0) throw new InvalidOperationException("Missing Entitas component snapshot.");
            var snapshot = MemoryPackSerializer.Deserialize<MobaEntitasComponentRollbackPayload>(payload);
            if (snapshot.Version != 1) throw new InvalidOperationException("Unsupported Entitas component snapshot version.");
            return snapshot;
        }
    }

    internal readonly struct MobaEntitasComponentSnapshotSummary
    {
        internal MobaEntitasComponentSnapshotSummary(int actorId, long castId, bool hasMetadata, bool flyingProjectile,
            bool projectileLauncher, int launcherProjectileId, bool summonMeta, int summonId)
        {
            ActorId = actorId;
            CastId = castId;
            HasMetadata = hasMetadata;
            HasFlyingProjectile = flyingProjectile;
            HasProjectileLauncher = projectileLauncher;
            LauncherProjectileId = launcherProjectileId;
            HasSummonMeta = summonMeta;
            SummonId = summonId;
        }

        internal int ActorId { get; }
        internal long CastId { get; }
        internal bool HasMetadata { get; }
        internal bool HasFlyingProjectile { get; }
        internal bool HasProjectileLauncher { get; }
        internal int LauncherProjectileId { get; }
        internal bool HasSummonMeta { get; }
        internal int SummonId { get; }
    }

    [MemoryPackable]
    public readonly partial struct MobaEntitasComponentRollbackPayload
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly MobaEntitasComponentRollbackEntry[] Entries;
        [MemoryPackConstructor]
        public MobaEntitasComponentRollbackPayload(int version, MobaEntitasComponentRollbackEntry[] entries)
        { Version = version; Entries = entries; }
    }

    [MemoryPackable]
    public readonly partial struct MobaEntitasComponentRollbackEntry
    {
        [MemoryPackOrder(0)] public readonly int ActorId;
        [MemoryPackOrder(1)] public readonly long CastId;
        [MemoryPackOrder(2)] public readonly byte[] Data;
        [MemoryPackConstructor]
        public MobaEntitasComponentRollbackEntry(int actorId, long castId, byte[] data)
        { ActorId = actorId; CastId = castId; Data = data; }
    }
}
