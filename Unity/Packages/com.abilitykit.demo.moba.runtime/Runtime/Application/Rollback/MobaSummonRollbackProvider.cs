using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Demo.Moba.Rollback
{
    [MobaRollbackProvider(DefaultKey)]
    public sealed class MobaSummonRollbackProvider : IRollbackStateProvider, IRollbackStatePreflightProvider, IRollbackSnapshotPreflightProvider
    {
        public const int DefaultKey = 12003;
        private const int Version = 1;
        private readonly MobaSummonService _summons;
        private readonly MobaActorRegistry _actors;
        private readonly MobaSkillCastRuntimeService _skills;

        public MobaSummonRollbackProvider(MobaSummonService summons, MobaActorRegistry actors, MobaSkillCastRuntimeService skills)
        { _summons = summons ?? throw new ArgumentNullException(nameof(summons)); _actors = actors ?? throw new ArgumentNullException(nameof(actors)); _skills = skills ?? throw new ArgumentNullException(nameof(skills)); }

        public int Key => DefaultKey;

        public byte[] Export(FrameIndex frame)
        {
            var state = _summons.CaptureRollbackState();
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Version); writer.Write(state.Entries.Length);
                foreach (var entry in state.Entries)
                {
                    var source = entry.Source;
                    var retain = entry.Retain;
                    writer.Write(entry.RootOwnerActorId); writer.Write(entry.OwnerActorId); writer.Write(entry.ActorId);
                    WriteSource(writer, in source); WriteRetain(writer, in retain);
                }
                return stream.ToArray();
            }
        }

        public void ValidateImport(FrameIndex frame, byte[] payload) => Read(payload);

        public void ValidateSnapshot(RollbackSnapshotPreflightContext context)
        {
            context.RequirePayload(Key);
            var entries = Read(context.RequirePayload(Key).ToArray());
            var actorIds = MobaEntitasEntityRollbackProvider.ReadSnapshotActorIds(
                context.RequirePayload(MobaEntitasEntityRollbackProvider.DefaultKey).ToArray());
            var components = MobaEntitasComponentRollbackProvider.ReadSnapshotSummaries(
                context.RequirePayload(MobaEntitasComponentRollbackProvider.DefaultKey).ToArray());
            var lifecycle = MobaSkillLifecycleSnapshotIndex.Read(context.Frame,
                context.RequirePayload(MobaSkillLifecycleRollbackProvider.DefaultKey).ToArray());

            foreach (var entry in entries)
            {
                Require(actorIds.Contains(entry.ActorId), "Summon relation actor is missing from the target actor roster.");
                Require(HasSummonMeta(components, entry.ActorId, entry.Source.IsValid ? entry.Source.SummonConfigId : 0),
                    "Summon relation actor is missing matching SummonMeta in the target frame.");
                Require(entry.Source.IsValid, "Summon relation is missing its target-frame source context.");
                lifecycle.RequireSource(MobaExecutionKind.SummonSpawn, entry.Source.SourceActorId,
                    entry.Source.SummonConfigId, entry.Source.SourceContextId, entry.Source.RootContextId);
                var retain = entry.Retain;
                lifecycle.RequireRetain(in retain);
            }
        }

        private static bool HasSummonMeta(MobaEntitasComponentSnapshotSummary[] summaries, int actorId, int summonId)
        {
            foreach (var summary in summaries)
                if (summary.ActorId == actorId)
                    return !summary.HasMetadata || summary.HasSummonMeta && (summonId == 0 || summary.SummonId == summonId);
            return false;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        public void Import(FrameIndex frame, byte[] payload)
        {
            var entries = Read(payload);
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (!_actors.TryGetRegistered(entry.ActorId, out var entity) || entity == null ||
                    !entity.hasSummonMeta || entity.summonMeta == null || entity.summonMeta.SummonId != entry.Source.SummonConfigId && entry.Source.IsValid ||
                    entry.OwnerActorId <= 0 || entry.RootOwnerActorId <= 0)
                    throw new InvalidOperationException("Summon relation does not match the restored actor roster.");
                if (entry.Source.IsValid && _skills.ExecutionContextRegistry != null &&
                    (!_skills.ExecutionContextRegistry.TryGet(entry.Source.SourceContextId, out _) ||
                     entry.Source.RootContextId != 0 && !_skills.ExecutionContextRegistry.TryGet(entry.Source.RootContextId, out _)))
                    throw new InvalidOperationException("Summon source context is missing from the restored execution-context registry.");
                var retain = ResolveRetain(entry);
                var source = entry.Source;
                entries[i] = new MobaSummonRollbackEntry(entry.RootOwnerActorId, entry.OwnerActorId,
                    entry.ActorId, in source, in retain);
            }
            _summons.RestoreRollbackState(new MobaSummonRollbackState(entries));
        }

        private MobaSkillRuntimeRetainHandle ResolveRetain(MobaSummonRollbackEntry entry)
        {
            var expected = entry.Retain;
            if (!expected.IsValid) return default;
            if (!entry.Source.IsValid || expected.Child.Kind != MobaSkillRuntimeChildKind.Summon ||
                expected.Child.ChildId != entry.ActorId || expected.Child.ContextId != entry.Source.SourceContextId ||
                expected.Child.ConfigId != entry.Source.SummonConfigId ||
                !_skills.TryFindRetain(expected.Runtime, expected.Child, out var actual) || !actual.Equals(expected))
                throw new InvalidOperationException("Summon relation retain does not match the restored SkillRuntime child.");
            return actual;
        }

        private static MobaSummonRollbackEntry[] Read(byte[] payload)
        {
            if (payload == null || payload.Length == 0) throw new InvalidOperationException("Missing Summon rollback payload.");
            using (var reader = new BinaryReader(new MemoryStream(payload, false)))
            {
                if (reader.ReadInt32() != Version) throw new InvalidOperationException("Unsupported Summon rollback payload.");
                var count = reader.ReadInt32();
                if (count < 0 || count > 100000) throw new InvalidOperationException("Invalid Summon rollback count.");
                var entries = new MobaSummonRollbackEntry[count]; var actors = new HashSet<int>();
                for (var i = 0; i < count; i++)
                {
                    var rootOwner = reader.ReadInt32(); var owner = reader.ReadInt32(); var actor = reader.ReadInt32();
                    if (rootOwner <= 0 || owner <= 0 || actor <= 0 || !actors.Add(actor))
                        throw new InvalidOperationException("Duplicate or invalid Summon relation identity.");
                    var source = ReadSource(reader); var retain = ReadRetain(reader);
                    if (source.IsValid && (source.SummonActorId != actor || source.SummonConfigId <= 0))
                        throw new InvalidOperationException("Summon source does not match its actor relation.");
                    if (retain.IsValid && (!source.IsValid || retain.Child.Kind != MobaSkillRuntimeChildKind.Summon ||
                        retain.Child.ChildId != actor || retain.Child.ContextId != source.SourceContextId ||
                        retain.Child.ConfigId != source.SummonConfigId))
                        throw new InvalidOperationException("Summon retain does not match its target relation.");
                    entries[i] = new MobaSummonRollbackEntry(rootOwner, owner, actor, in source, in retain);
                }
                if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidOperationException("Unexpected Summon rollback payload data.");
                return entries;
            }
        }

        private static void WriteSource(BinaryWriter w, in SummonSourceContext s)
        {
            w.Write(s.SourceActorId); w.Write(s.SummonActorId); w.Write(s.SummonConfigId);
            w.Write(s.SourceContextId); w.Write(s.RootContextId); w.Write(s.OwnerContextId);
            WriteRuntime(w, s.SkillRuntimeHandle); WriteOrigin(w, s.Origin);
        }
        private static SummonSourceContext ReadSource(BinaryReader r)
        {
            var source = r.ReadInt32(); var actor = r.ReadInt32(); var config = r.ReadInt32();
            var context = r.ReadInt64(); var root = r.ReadInt64(); var owner = r.ReadInt64();
            var runtime = ReadRuntime(r); var origin = ReadOrigin(r);
            return new SummonSourceContext(source, actor, config, context, root, owner, in runtime, in origin);
        }
        private static void WriteOrigin(BinaryWriter w, MobaGameplayOrigin o)
        {
            w.Write(o.SourceActorId); w.Write(o.TargetActorId); w.Write((byte)o.ImmediateKind); w.Write(o.ImmediateConfigId);
            w.Write(o.ImmediateContextId); w.Write(o.ParentContextId); w.Write(o.RootContextId); w.Write(o.OwnerContextId);
            WriteRuntime(w, o.SkillRuntimeHandle);
        }
        private static MobaGameplayOrigin ReadOrigin(BinaryReader r)
        {
            var source = r.ReadInt32(); var target = r.ReadInt32(); var kind = (MobaExecutionKind)r.ReadByte(); var config = r.ReadInt32();
            var context = r.ReadInt64(); var parent = r.ReadInt64(); var root = r.ReadInt64(); var owner = r.ReadInt64();
            var runtime = ReadRuntime(r);
            return new MobaGameplayOrigin(source, target, kind, config, context, parent, root, owner, runtime);
        }
        private static void WriteRuntime(BinaryWriter w, MobaSkillCastRuntimeHandle h)
        { w.Write(h.RuntimeId); w.Write(h.Generation); w.Write(h.RootContextId); }
        private static MobaSkillCastRuntimeHandle ReadRuntime(BinaryReader r) => new MobaSkillCastRuntimeHandle(r.ReadInt64(), r.ReadInt32(), r.ReadInt64());
        private static void WriteRetain(BinaryWriter w, in MobaSkillRuntimeRetainHandle h)
        {
            w.Write(h.RetainId); WriteRuntime(w, h.Runtime); w.Write((int)h.Child.Kind); w.Write(h.Child.ChildId);
            w.Write(h.Child.ContextId); w.Write(h.Child.ConfigId);
        }
        private static MobaSkillRuntimeRetainHandle ReadRetain(BinaryReader r)
        {
            var id = r.ReadInt64(); var runtime = ReadRuntime(r);
            var child = new MobaSkillRuntimeChildRef((MobaSkillRuntimeChildKind)r.ReadInt32(), r.ReadInt64(), r.ReadInt64(), r.ReadInt32());
            return new MobaSkillRuntimeRetainHandle(id, in runtime, in child);
        }
    }
}
