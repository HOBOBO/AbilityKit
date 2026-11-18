using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Demo.Moba.Services.Projectile.Launch;

namespace AbilityKit.Demo.Moba.Rollback
{
    [MobaRollbackProvider(DefaultKey)]
    public sealed class MobaProjectileLinkRollbackProvider : IRollbackStateProvider, IRollbackStatePreflightProvider, IRollbackSnapshotPreflightProvider
    {
        public const int DefaultKey = 12002;
        private const int Version = 1;
        private readonly MobaProjectileLinkService _links;
        private readonly MobaSkillCastRuntimeService _skills;
        private readonly IProjectileService _projectiles;
        private readonly MobaActorRegistry _actors;

        public MobaProjectileLinkRollbackProvider(MobaProjectileLinkService links, MobaSkillCastRuntimeService skills,
            IProjectileService projectiles = null, MobaActorRegistry actors = null)
        { _links = links ?? throw new ArgumentNullException(nameof(links)); _skills = skills ?? throw new ArgumentNullException(nameof(skills)); _projectiles = projectiles; _actors = actors; }

        public int Key => DefaultKey;

        public byte[] Export(FrameIndex frame)
        {
            var state = _links.CaptureRollbackState();
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                writer.Write(state.Projectiles.Length);
                foreach (var entry in state.Projectiles)
                {
                    var source = entry.Source;
                    var retain = entry.Retain;
                    writer.Write(entry.ProjectileId); writer.Write(entry.ActorId);
                    WriteSource(writer, in source); WriteRetain(writer, in retain);
                }
                writer.Write(state.Retired.Length);
                foreach (var entry in state.Retired) { writer.Write(entry.ProjectileId); writer.Write(entry.ActorId); }
                writer.Write(state.Launchers.Length);
                foreach (var entry in state.Launchers)
                {
                    var source = entry.Source;
                    var retain = entry.Retain;
                    writer.Write(entry.ActorId); WriteSource(writer, in source); WriteRetain(writer, in retain);
                }
                return stream.ToArray();
            }
        }

        public void ValidateImport(FrameIndex frame, byte[] payload) => Read(payload);

        public void ValidateSnapshot(RollbackSnapshotPreflightContext context)
        {
            context.RequirePayload(Key);
            var state = Read(context.RequirePayload(Key).ToArray());
            var projectileIds = ProjectileRollbackProvider.ReadProjectileIds(
                context.RequirePayload(ProjectileRollbackProvider.DefaultKey).ToArray());
            var actorIds = MobaEntitasEntityRollbackProvider.ReadSnapshotActorIds(
                context.RequirePayload(MobaEntitasEntityRollbackProvider.DefaultKey).ToArray());
            var components = MobaEntitasComponentRollbackProvider.ReadSnapshotSummaries(
                context.RequirePayload(MobaEntitasComponentRollbackProvider.DefaultKey).ToArray(), context.Frame.Value);
            var schedules = ProjectileRollbackProvider.ReadScheduleSnapshot(
                context.RequirePayload(ProjectileRollbackProvider.DefaultKey).ToArray(), context.Frame);
            var scheduledLaunchers = new HashSet<int>();
            foreach (var schedule in schedules)
            {
                Require(scheduledLaunchers.Add(schedule.LauncherActorId), "Multiple active schedules reference one MOBA launcher.");
                var matched = false;
                foreach (var component in components)
                    if (component.ActorId == schedule.LauncherActorId && component.HasProjectileLauncher)
                    {
                        Require(component.ScheduleId == schedule.Id && component.LauncherProjectileId == schedule.TemplateId &&
                            component.LauncherRootActorId == schedule.RootActorId &&
                            Math.Max(0, component.LauncherIntervalFrames) == schedule.IntervalFrames,
                            "Active schedule does not match target-frame Launcher fields.");
                        MobaProjectileSchedulePatternResolver.ValidateLauncher(in schedule.Pattern, component.LauncherId);
                        matched = true;
                    }
                Require(matched && actorIds.Contains(schedule.LauncherActorId), "Active schedule has no target-frame Launcher actor.");
                matched = false;
                foreach (var launcher in state.Launchers)
                    if (launcher.ActorId == schedule.LauncherActorId)
                    {
                        Require(launcher.Source.SourceActorId == schedule.RootActorId &&
                            launcher.Source.ProjectileConfigId == schedule.TemplateId,
                            "Active schedule does not match its source relationship.");
                        matched = true;
                    }
                Require(matched, "Active schedule has no target-frame Launcher source relationship.");
            }
            var lifecycle = MobaSkillLifecycleSnapshotIndex.Read(context.Frame,
                context.RequirePayload(MobaSkillLifecycleRollbackProvider.DefaultKey).ToArray());

            foreach (var entry in state.Projectiles)
            {
                Require(actorIds.Contains(entry.ActorId), "Projectile relation actor is missing from the target actor roster.");
                Require(projectileIds.Contains(entry.ProjectileId), "Projectile relation references a missing target-frame physical projectile.");
                Require(HasFlyingProjectile(components, entry.ActorId), "Projectile relation actor is missing the FlyingProjectile component in the target frame.");
                lifecycle.RequireSource(MobaExecutionKind.ProjectileLaunch, entry.Source.SourceActorId,
                    entry.Source.ProjectileConfigId, entry.Source.SourceContextId, entry.Source.RootContextId);
                var retain = entry.Retain;
                lifecycle.RequireRetain(in retain);
            }
            foreach (var entry in state.Launchers)
            {
                Require(actorIds.Contains(entry.ActorId), "Projectile launcher actor is missing from the target actor roster.");
                var launcherId = ReadLauncherId(components, entry.ActorId, entry.Source.ProjectileConfigId);
                Require(launcherId > 0, "Projectile launcher actor is missing or has a mismatched ProjectileLauncher component in the target frame.");
                lifecycle.RequireSource(MobaExecutionKind.ProjectileLaunch, entry.Source.SourceActorId,
                    entry.Source.ProjectileConfigId, entry.Source.SourceContextId, entry.Source.RootContextId);
                var retain = entry.Retain;
                Require(!retain.IsValid || retain.Child.ConfigId == launcherId,
                    "Projectile launcher retain references a different launcher template.");
                lifecycle.RequireRetain(in retain);
            }
        }

        private static bool HasFlyingProjectile(MobaEntitasComponentSnapshotSummary[] summaries, int actorId)
        {
            foreach (var summary in summaries)
                if (summary.ActorId == actorId) return summary.HasFlyingProjectile;
            return false;
        }

        private static int ReadLauncherId(MobaEntitasComponentSnapshotSummary[] summaries, int actorId, int projectileConfigId)
        {
            foreach (var summary in summaries)
                if (summary.ActorId == actorId && summary.HasProjectileLauncher &&
                    summary.LauncherProjectileId == projectileConfigId) return summary.LauncherId;
            return 0;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        public void Import(FrameIndex frame, byte[] payload)
        {
            var state = Read(payload);
            var projectiles = new MobaProjectileLinkRollbackEntry[state.Projectiles.Length];
            for (var i = 0; i < projectiles.Length; i++)
            {
                var entry = state.Projectiles[i];
                var source = entry.Source;
                if (_projectiles != null && !_projectiles.TryGetRuntimeState(new ProjectileId(entry.ProjectileId), out _))
                    throw new InvalidOperationException("Projectile relation references a missing restored physical projectile.");
                if (_actors != null)
                {
                    if (!_actors.TryGetRegistered(entry.ActorId, out var actor) || actor == null)
                        throw new InvalidOperationException($"Projectile relation references a missing restored projectile actor {entry.ActorId}.");
                    if (!actor.isFlyingProjectileTag)
                        throw new InvalidOperationException($"Projectile relation actor {entry.ActorId} is missing its restored FlyingProjectile marker.");
                }
                ValidateSourceContext(in source);
                var retain = ResolveRetain(entry.Retain, MobaSkillRuntimeChildKind.Projectile, entry.ProjectileId,
                    source.SourceContextId, source.ProjectileConfigId);
                projectiles[i] = new MobaProjectileLinkRollbackEntry(entry.ProjectileId, entry.ActorId, in source, in retain);
            }
            var launchers = new MobaProjectileLauncherLinkRollbackEntry[state.Launchers.Length];
            for (var i = 0; i < launchers.Length; i++)
            {
                var entry = state.Launchers[i];
                var source = entry.Source;
                var launcherConfigId = entry.Retain.Child.ConfigId;
                if (_actors != null && (!_actors.TryGetRegistered(entry.ActorId, out var actor) || actor == null || !actor.hasProjectileLauncher))
                    throw new InvalidOperationException("Projectile launcher relation references a missing restored launcher actor.");
                if (_actors != null && _actors.TryGetRegistered(entry.ActorId, out var launcherActor))
                {
                    if (launcherActor.projectileLauncher.ProjectileId != source.ProjectileConfigId)
                        throw new InvalidOperationException("Projectile launcher source references a different projectile template.");
                    launcherConfigId = launcherActor.projectileLauncher.LauncherId;
                }
                ValidateSourceContext(in source);
                var retain = ResolveRetain(entry.Retain, MobaSkillRuntimeChildKind.ProjectileLauncher, entry.ActorId,
                    source.SourceContextId, launcherConfigId);
                launchers[i] = new MobaProjectileLauncherLinkRollbackEntry(entry.ActorId, in source, in retain);
            }
            _links.RestoreRollbackState(new MobaProjectileLinkRollbackState(projectiles, state.Retired, launchers));
        }

        private void ValidateSourceContext(in ProjectileSourceContext source)
        {
            var contexts = _skills.ExecutionContextRegistry;
            if (contexts != null && (!contexts.TryGet(source.SourceContextId, out _) ||
                source.RootContextId != 0 && !contexts.TryGet(source.RootContextId, out _)))
                throw new InvalidOperationException("Projectile source context is missing from the restored execution-context registry.");
        }

        private MobaSkillRuntimeRetainHandle ResolveRetain(
            MobaSkillRuntimeRetainHandle expected, MobaSkillRuntimeChildKind kind, long childId,
            long contextId, int configId)
        {
            if (!expected.IsValid) return default;
            if (expected.Child.Kind != kind || expected.Child.ChildId != childId ||
                expected.Child.ContextId != contextId || expected.Child.ConfigId != configId ||
                !_skills.TryFindRetain(expected.Runtime, expected.Child, out var actual) || !actual.Equals(expected))
                throw new InvalidOperationException("Projectile relation retain does not match the restored SkillRuntime child.");
            return actual;
        }

        private static State Read(byte[] payload)
        {
            if (payload == null || payload.Length == 0) throw new InvalidOperationException("Missing Projectile link rollback payload.");
            using (var reader = new BinaryReader(new MemoryStream(payload, false)))
            {
                if (reader.ReadInt32() != Version) throw new InvalidOperationException("Unsupported Projectile link rollback payload.");
                var projectiles = new List<MobaProjectileLinkRollbackEntry>();
                var projectileIds = new HashSet<int>(); var actorIds = new HashSet<int>();
                var retainIds = new HashSet<long>();
                var count = ReadCount(reader);
                for (var i = 0; i < count; i++)
                {
                    var id = reader.ReadInt32(); var actorId = reader.ReadInt32();
                    if (id <= 0 || actorId <= 0 || !projectileIds.Add(id) || !actorIds.Add(actorId))
                        throw new InvalidOperationException("Duplicate or invalid Projectile link identity.");
                    var source = ReadSource(reader); var retain = ReadRetain(reader);
                    if (!source.IsValid) throw new InvalidOperationException("Projectile link has no target-frame source context.");
                    ValidateRetainIdentity(retain, MobaSkillRuntimeChildKind.Projectile, id,
                        source.SourceContextId, source.ProjectileConfigId, retainIds);
                    projectiles.Add(new MobaProjectileLinkRollbackEntry(id, actorId, in source, in retain));
                }
                var retired = new List<MobaProjectileRetiredLinkRollbackEntry>();
                var retiredIds = new HashSet<int>();
                count = ReadCount(reader);
                for (var i = 0; i < count; i++)
                {
                    var id = reader.ReadInt32(); var actorId = reader.ReadInt32();
                    if (id <= 0 || actorId <= 0 || !retiredIds.Add(id) || projectileIds.Contains(id))
                        throw new InvalidOperationException("Duplicate or invalid retired Projectile link identity.");
                    retired.Add(new MobaProjectileRetiredLinkRollbackEntry(id, actorId));
                }
                var launchers = new List<MobaProjectileLauncherLinkRollbackEntry>();
                var launcherIds = new HashSet<int>();
                count = ReadCount(reader);
                for (var i = 0; i < count; i++)
                {
                    var actorId = reader.ReadInt32();
                    if (actorId <= 0 || !launcherIds.Add(actorId)) throw new InvalidOperationException("Duplicate or invalid launcher link identity.");
                    var source = ReadSource(reader); var retain = ReadRetain(reader);
                    if (!source.IsValid) throw new InvalidOperationException("Launcher link has no target-frame source context.");
                    ValidateRetainIdentity(retain, MobaSkillRuntimeChildKind.ProjectileLauncher, actorId,
                        source.SourceContextId, retain.Child.ConfigId, retainIds);
                    launchers.Add(new MobaProjectileLauncherLinkRollbackEntry(actorId, in source, in retain));
                }
                if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidOperationException("Unexpected Projectile link payload data.");
                return new State(projectiles.ToArray(), retired.ToArray(), launchers.ToArray());
            }
        }

        private static int ReadCount(BinaryReader reader)
        { var count = reader.ReadInt32(); if (count < 0 || count > 100000) throw new InvalidOperationException("Invalid Projectile link count."); return count; }

        private static void ValidateRetainIdentity(MobaSkillRuntimeRetainHandle retain,
            MobaSkillRuntimeChildKind kind, long childId, long contextId, int configId, ISet<long> retainIds)
        {
            if (!retain.IsValid) return;
            if (retain.RetainId <= 0 || configId <= 0 || !retainIds.Add(retain.RetainId) || retain.Child.Kind != kind ||
                retain.Child.ChildId != childId || retain.Child.ContextId != contextId || retain.Child.ConfigId != configId)
                throw new InvalidOperationException("Duplicate or mismatched Projectile relation retain identity.");
        }

        private static void WriteSource(BinaryWriter w, in ProjectileSourceContext s)
        {
            w.Write(s.SourceActorId); w.Write(s.InitialTargetActorId); w.Write(s.ProjectileConfigId);
            w.Write(s.SourceContextId); w.Write(s.RootContextId); w.Write(s.OwnerContextId);
            WriteRuntime(w, s.SkillRuntimeHandle); WriteOrigin(w, s.Origin);
        }
        private static ProjectileSourceContext ReadSource(BinaryReader r)
        {
            var source = r.ReadInt32(); var target = r.ReadInt32(); var config = r.ReadInt32();
            var context = r.ReadInt64(); var root = r.ReadInt64(); var owner = r.ReadInt64();
            var runtime = ReadRuntime(r); var origin = ReadOrigin(r);
            return new ProjectileSourceContext(source, target, config, context, root, owner, in runtime, in origin);
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

        private sealed class State
        {
            public State(MobaProjectileLinkRollbackEntry[] projectiles, MobaProjectileRetiredLinkRollbackEntry[] retired,
                MobaProjectileLauncherLinkRollbackEntry[] launchers)
            { Projectiles = projectiles; Retired = retired; Launchers = launchers; }
            public MobaProjectileLinkRollbackEntry[] Projectiles { get; }
            public MobaProjectileRetiredLinkRollbackEntry[] Retired { get; }
            public MobaProjectileLauncherLinkRollbackEntry[] Launchers { get; }
        }
    }
}
