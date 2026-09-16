using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Demo.Moba.Components;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Rollback
{
    public sealed class MobaEntitasComponentRollbackProvider : IRollbackStateProvider, IRollbackStatePreflightProvider
    {
        public const int DefaultKey = 10018;
        private readonly global::ActorContext _context;

        public MobaEntitasComponentRollbackProvider(global::ActorContext context) =>
            _context = context ?? throw new ArgumentNullException(nameof(context));

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
                    entry.ActorId > 0 && !byActor.Contains(entry.ActorId) ||
                    entry.CastId > 0 && !byCast.Contains(entry.CastId))
                    throw new InvalidOperationException($"Missing or invalid Entitas component snapshot for actor {entry.ActorId}, cast {entry.CastId}.");
            }
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
                using (var stream = new MemoryStream(entry.Data, false))
                using (var reader = new BinaryReader(stream))
                    Restore(target, reader);
            }
        }

        private static void Write(global::ActorEntity e, BinaryWriter w)
        {
            w.Write(e.hasLifetime);
            if (e.hasLifetime) w.Write(e.lifetime.EndTimeMs);
            w.Write(e.hasProjectileLauncher);
            if (e.hasProjectileLauncher)
            {
                var p = e.projectileLauncher;
                w.Write(p.LauncherId); w.Write(p.ProjectileId); w.Write(p.RootActorId);
                w.Write(p.EndTimeMs); w.Write(p.ActiveBullets); w.Write(p.ScheduleId);
                w.Write(p.IntervalFrames); w.Write(p.TotalCount);
            }
            w.Write(e.hasSummonMeta);
            if (e.hasSummonMeta) { w.Write(e.summonMeta.SummonId); w.Write(e.summonMeta.DespawnOnOwnerDie); }
            w.Write(e.hasProjectileEffectSnapshot);
            if (e.hasProjectileEffectSnapshot)
            {
                var p = e.projectileEffectSnapshot;
                w.Write(p.DamageMul); w.Write(p.SpeedMul); w.Write(p.Pierce);
            }
            w.Write(e.hasSkillLoadout);
            if (e.hasSkillLoadout)
            {
                var active = e.skillLoadout.ActiveSkills;
                w.Write(active == null ? -1 : active.Length);
                if (active != null) foreach (var skill in active)
                {
                    w.Write(skill != null);
                    if (skill == null) continue;
                    w.Write(skill.SkillId); w.Write(skill.Level); w.Write(skill.CooldownDurationMs);
                    w.Write(skill.CooldownEndTimeMs); w.Write(skill.MaxCharges); w.Write(skill.CurrentCharges);
                    w.Write(skill.ChargeRecoveryMs); w.Write(skill.NextChargeRecoveryTimeMs);
                    w.Write(skill.CooldownGroupId); w.Write(skill.ChargesConfigured); w.Write(skill.IgnoreGlobalCooldown);
                }
                var passive = e.skillLoadout.PassiveSkills;
                w.Write(passive == null ? -1 : passive.Length);
                if (passive != null) foreach (var skill in passive)
                {
                    w.Write(skill != null);
                    if (skill == null) continue;
                    w.Write(skill.PassiveSkillId); w.Write(skill.Level);
                    w.Write(skill.CooldownDurationMs); w.Write(skill.CooldownEndTimeMs);
                }
            }
            w.Write(e.hasSkillCastTimelineRuntime);
            if (e.hasSkillCastTimelineRuntime)
            { w.Write(e.skillCastTimelineRuntime.ElapsedMs); w.Write(e.skillCastTimelineRuntime.NextEventIndex); }
            w.Write(e.isSkillCastRunningTag);
            w.Write(e.hasSkillCastCancelRequest);
            if (e.hasSkillCastCancelRequest)
            { w.Write(e.skillCastCancelRequest.Frame); w.Write((int)e.skillCastCancelRequest.Reason); }
            w.Write(e.hasSkillCastDestroyRequest);
            if (e.hasSkillCastDestroyRequest)
            {
                var p = e.skillCastDestroyRequest;
                w.Write(p.RequestFrame); w.Write(p.MinConfirmedFrame); w.Write((int)p.Reason);
            }
            w.Write(e.hasActorDespawnRequest);
            if (e.hasActorDespawnRequest)
            {
                var p = e.actorDespawnRequest;
                w.Write(p.RequestFrame); w.Write(p.MinConfirmedFrame); w.Write((int)p.Reason);
                w.Write(p.SourceActorId); w.Write(p.SourceContextId);
            }
        }

        private static void Restore(global::ActorEntity e, BinaryReader r)
        {
            if (r.ReadBoolean()) e.ReplaceLifetime(r.ReadInt64());
            else if (e.hasLifetime) e.RemoveLifetime();
            if (r.ReadBoolean()) e.ReplaceProjectileLauncher(r.ReadInt32(), r.ReadInt32(), r.ReadInt32(),
                r.ReadInt64(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32());
            else if (e.hasProjectileLauncher) e.RemoveProjectileLauncher();
            if (r.ReadBoolean()) e.ReplaceSummonMeta(r.ReadInt32(), r.ReadBoolean());
            else if (e.hasSummonMeta) e.RemoveSummonMeta();
            if (r.ReadBoolean()) e.ReplaceProjectileEffectSnapshot(r.ReadSingle(), r.ReadSingle(), r.ReadInt32());
            else if (e.hasProjectileEffectSnapshot) e.RemoveProjectileEffectSnapshot();
            if (r.ReadBoolean())
            {
                var activeCount = r.ReadInt32();
                var active = activeCount < 0 ? null : new ActiveSkillRuntime[activeCount];
                for (var i = 0; i < activeCount; i++)
                {
                    if (!r.ReadBoolean()) continue;
                    active[i] = new ActiveSkillRuntime
                    {
                        SkillId = r.ReadInt32(), Level = r.ReadInt32(), CooldownDurationMs = r.ReadInt32(),
                        CooldownEndTimeMs = r.ReadInt64(), MaxCharges = r.ReadInt32(), CurrentCharges = r.ReadInt32(),
                        ChargeRecoveryMs = r.ReadInt32(), NextChargeRecoveryTimeMs = r.ReadInt64(),
                        CooldownGroupId = r.ReadInt32(), ChargesConfigured = r.ReadBoolean(), IgnoreGlobalCooldown = r.ReadBoolean()
                    };
                }
                var passiveCount = r.ReadInt32();
                var passive = passiveCount < 0 ? null : new PassiveSkillRuntime[passiveCount];
                for (var i = 0; i < passiveCount; i++)
                {
                    if (!r.ReadBoolean()) continue;
                    passive[i] = new PassiveSkillRuntime
                    {
                        PassiveSkillId = r.ReadInt32(), Level = r.ReadInt32(),
                        CooldownDurationMs = r.ReadInt32(), CooldownEndTimeMs = r.ReadInt64()
                    };
                }
                e.ReplaceSkillLoadout(active, passive);
            }
            else if (e.hasSkillLoadout) e.RemoveSkillLoadout();
            if (r.ReadBoolean()) e.ReplaceSkillCastTimelineRuntime(r.ReadInt32(), r.ReadInt32());
            else if (e.hasSkillCastTimelineRuntime) e.RemoveSkillCastTimelineRuntime();
            if (r.ReadBoolean()) e.isSkillCastRunningTag = true;
            else e.isSkillCastRunningTag = false;
            if (r.ReadBoolean()) e.ReplaceSkillCastCancelRequest(r.ReadInt32(), (SkillCancelReason)r.ReadInt32());
            else if (e.hasSkillCastCancelRequest) e.RemoveSkillCastCancelRequest();
            if (r.ReadBoolean()) e.ReplaceSkillCastDestroyRequest(r.ReadInt32(), r.ReadInt32(), (SkillDestroyReason)r.ReadInt32());
            else if (e.hasSkillCastDestroyRequest) e.RemoveSkillCastDestroyRequest();
            if (r.ReadBoolean()) e.ReplaceActorDespawnRequest(r.ReadInt32(), r.ReadInt32(),
                (ActorDespawnReason)r.ReadInt32(), r.ReadInt32(), r.ReadInt64());
            else if (e.hasActorDespawnRequest) e.RemoveActorDespawnRequest();
            if (r.BaseStream.Position != r.BaseStream.Length)
                throw new InvalidOperationException("Unexpected Entitas component snapshot trailing data.");
        }

        private static MobaEntitasComponentRollbackPayload Read(byte[] payload)
        {
            if (payload == null || payload.Length == 0) throw new InvalidOperationException("Missing Entitas component snapshot.");
            var snapshot = MemoryPackSerializer.Deserialize<MobaEntitasComponentRollbackPayload>(payload);
            if (snapshot.Version != 1) throw new InvalidOperationException("Unsupported Entitas component snapshot version.");
            return snapshot;
        }
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
