using System.Reflection;
using MemoryPack;
using Newtonsoft.Json.Linq;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Demo.Moba.Services.Projectile.Launch;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

public sealed partial class MobaProjectileAreaMotionCompositionTests
{
    [Theory]
    [InlineData("missing-tombstone")]
    [InlineData("wrong-kind")]
    [InlineData("entity-version")]
    [InlineData("payload-version")]
    [InlineData("command-frame")]
    [InlineData("trimmed-journal")]
    [InlineData("old-command-key")]
    [InlineData("old-lifecycle-version")]
    [InlineData("unknown-extra")]
    [InlineData("launcher-component")]
    [InlineData("active-bullets")]
    [InlineData("missing-link")]
    [InlineData("link-source")]
    [InlineData("missing-retain")]
    [InlineData("parent-runtime")]
    [InlineData("parent-context")]
    [InlineData("missing-continuous")]
    [InlineData("continuous-identity")]
    [InlineData("pipeline-identity")]
    [InlineData("result-schedule")]
    [InlineData("result-end-time")]
    [InlineData("non-finite-request")]
    [InlineData("missing-request-field")]
    [InlineData("sequence-type")]
    [InlineData("missing-request-payload")]
    public void Cleaned_launcher_rejects_incomplete_recovery_evidence_without_any_import(string defect)
    {
        using var b = new Battle(1001, "launcher-invalid-" + defect);
        b.SpawnTestRepeatLauncher(durationMs: 0, endPipeline: true);
        b.VerifyInvalidLauncherEvidence(defect);
    }

    [Fact]
    public void Launcher_cleanup_without_rollback_does_not_create_a_command_journal()
    {
        using var b = new Battle(1001, "launcher-without-rollback", enableRollback: false);
        b.SpawnTestRepeatLauncher(durationMs: 0, endPipeline: true);
        b.VerifyCleanupWithoutRollback();
    }

    private sealed partial class Battle
    {
        private string[] ReadActorJournal()
        {
            Assert.True(Services.Resolve<MobaEntityManager>().TryGetActorRollbackCommands(out var commands));
            return Enumerable.Range(0, commands.Log.Count).Select(i =>
            {
                var r = commands.Log.GetRecord(i);
                return $"{r.Frame.Value}:{r.Order}:{r.CommandType}:{r.PayloadVersion}:{Convert.ToHexString(r.Payload.Span)}";
            }).ToArray();
        }

        public void VerifyCleanupWithoutRollback()
        {
            var entities = Services.Resolve<MobaEntityManager>();
            Assert.False(entities.TryGetActorRollbackCommands(out _));
            var launcher = Assert.Single(Services.Resolve<MobaProjectileLinkService>().CaptureRollbackState().Launchers);
            Until(() => !HasLauncher());
            Setup.Tick(1);
            Assert.False(entities.TryGetActorRollbackCommands(out _));
            Assert.Empty(Services.Resolve<MobaProjectileLinkService>().CaptureRollbackState().Projectiles);
            Assert.False(Services.Resolve<MobaSkillCastRuntimeService>().TryGet(launcher.Source.SkillRuntimeHandle, out _));
            Assert.DoesNotContain(Services.Resolve<MobaContinuousManager>().GetAllContinuous(),
                c => c is MobaProjectileLaunchContinuous);
            Assert.Equal(0, PendingNotifications);
        }

        public void VerifyInvalidLauncherEvidence(string defect)
        {
            Setup.Tick(1);
            var links = Services.Resolve<MobaProjectileLinkService>();
            var linkState = links.CaptureRollbackState();
            var launcher = Assert.Single(linkState.Launchers);
            var checkpoint = rollback.Capture(new(Frame));
            var invalid = checkpoint;
            void Replace(int key, byte[] payload) => invalid = new(checkpoint.Version, checkpoint.Frame,
                invalid.Entries.Select(p => p.Key == key ? new WorldRollbackSnapshotEntry(key, payload) : p).ToArray());
            var lifecycle = MobaSkillLifecycleCheckpointCodec.Decode<MobaSkillLifecycleCheckpoint>(checkpoint.Entries
                .Single(p => p.Key == MobaSkillLifecycleRollbackProvider.DefaultKey).Payload);
            var localSection = lifecycle.Sections.Single(s => s.Name == "continuous-local");
            var order = MobaSkillLifecycleCheckpointCodec.Decode<MobaContinuousLocalCheckpoint[]>(localSection.Payload);
            var continuous = Assert.Single(order, c => c.Type == typeof(MobaProjectileLaunchContinuous).FullName);
            var launch = continuous.ProjectileLaunch;
            Assert.NotNull(launch);
            var skillsSection = lifecycle.Sections.Single(s => s.Name == "skill-runtime");
            var localSkills = MemoryPackSerializer.Deserialize<MobaSkillRuntimeLocalRollbackPayload>(skillsSection.Payload);
            var skills = MemoryPackSerializer.Deserialize<MobaSkillRuntimeRollbackPayload>(localSkills.RuntimeState);
            switch (defect)
            {
                case "old-lifecycle-version": lifecycle.Version = 4; break;
                case "launcher-component":
                case "active-bullets":
                    var actor = Actor(launcher.ActorId);
                    var fields = actor.projectileLauncher;
                    var end = fields.EndTimeMs;
                    var active = fields.ActiveBullets;
                    if (defect == "active-bullets") fields.ActiveBullets++;
                    else fields.EndTimeMs++;
                    Replace(MobaEntitasComponentRollbackProvider.DefaultKey,
                        rollback.Capture(new(Frame)).Entries.Single(p => p.Key == MobaEntitasComponentRollbackProvider.DefaultKey).Payload);
                    fields.EndTimeMs = end;
                    fields.ActiveBullets = active;
                    break;
                case "missing-link":
                case "link-source":
                    var isolated = new MobaProjectileLinkService();
                    var source = launcher.Source;
                    var wrongSource = new ProjectileSourceContext(source.SourceActorId, source.InitialTargetActorId,
                        source.ProjectileConfigId, source.SourceContextId, source.RootContextId, source.OwnerContextId + 1,
                        source.SkillRuntimeHandle, source.Origin);
                    isolated.RestoreRollbackState(new(linkState.Projectiles, linkState.Retired,
                        defect == "missing-link" ? Array.Empty<MobaProjectileLauncherLinkRollbackEntry>() :
                        new[] { new MobaProjectileLauncherLinkRollbackEntry(launcher.ActorId, wrongSource, launcher.Retain) }));
                    Replace(MobaProjectileLinkRollbackProvider.DefaultKey,
                        new MobaProjectileLinkRollbackProvider(isolated, Services.Resolve<MobaSkillCastRuntimeService>()).Export(checkpoint.Frame));
                    break;
                case "missing-retain":
                case "parent-runtime":
                    var altered = new MobaSkillRuntimeRollbackPayload(skills.Version, skills.NextRuntimeId,
                        skills.NextRetainId, skills.NextGeneration,
                        defect == "parent-runtime" ? skills.Runtimes.Where(r => r.RuntimeId != launch.SkillRuntimeId).ToArray() : skills.Runtimes,
                        defect == "missing-retain" ? skills.Retains.Where(r => r.ChildId != launcher.ActorId ||
                            r.ChildKind != (int)MobaSkillRuntimeChildKind.ProjectileLauncher).ToArray() : skills.Retains);
                    skillsSection.Payload = MemoryPackSerializer.Serialize(new MobaSkillRuntimeLocalRollbackPayload(
                        localSkills.Version, MemoryPackSerializer.Serialize(altered), localSkills.ExecutionContextNodes,
                        localSkills.ExecutionContextNextId));
                    break;
                case "parent-context": launch.OriginParentContextId = long.MaxValue; break;
                case "missing-continuous": order = order.Where(c => c != continuous).ToArray(); break;
                case "continuous-identity": continuous.Id += ":invalid"; break;
                case "pipeline-identity": continuous.Type = typeof(SkillPipelineContinuousRuntime).FullName!; break;
                case "result-schedule": launch.ScheduleId++; break;
                case "result-end-time": launch.EndTimeMs++; break;
                case "non-finite-request": launch.DirectionX = float.NaN; break;
                case "sequence-type": launch.SequenceType = "unsupported-sequence"; break;
                case "missing-request-payload": continuous.ProjectileLaunch = null!; break;
            }
            localSection.Payload = MobaSkillLifecycleCheckpointCodec.Encode(order);
            if (defect == "missing-request-field")
            {
                var json = JArray.Parse(System.Text.Encoding.UTF8.GetString(localSection.Payload));
                ((JObject)Assert.Single(json.OfType<JObject>(), c => c["ProjectileLaunch"] is JObject)["ProjectileLaunch"]!)
                    .Property("TrackTarget")!.Remove();
                localSection.Payload = System.Text.Encoding.UTF8.GetBytes(json.ToString(Newtonsoft.Json.Formatting.None));
            }
            Replace(MobaSkillLifecycleRollbackProvider.DefaultKey, MobaSkillLifecycleCheckpointCodec.Encode(lifecycle));
            Until(() => !HasLauncher());
            Setup.Tick(1);
            Assert.DoesNotContain(Services.Resolve<MobaContinuousManager>().GetAllContinuous(), c => c is MobaProjectileLaunchContinuous);

            Assert.True(Services.Resolve<MobaEntityManager>().TryGetActorRollbackCommands(out var commands));
            var records = (List<RollbackCommandRecord>)typeof(CommandRollbackLog)
                .GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(commands.Log)!;
            var index = records.FindIndex(r => r.CommandType == MobaActorRollbackCommandRuntime.RestoreDestroyedActorCommand &&
                MemoryPackSerializer.Deserialize<MobaActorRollbackTombstone>(r.Payload.ToArray()).ActorId == launcher.ActorId);
            Assert.True(index >= 0);
            var record = records[index];
            if (defect == "missing-tombstone") records.RemoveAt(index);
            else if (defect is "wrong-kind" or "entity-version" or "payload-version" or "command-frame")
            {
                var original = MemoryPackSerializer.Deserialize<MobaActorRollbackTombstone>(record.Payload.ToArray());
                var tombstone = new MobaActorRollbackTombstone(original.ActorId,
                    defect == "wrong-kind" ? MobaEntityKind.Projectile : original.Kind, original.Transform,
                    original.Team, original.MainType, original.UnitSubType, original.OwnerPlayerId,
                    original.ModelId, original.HasOwnerLink, original.OwnerActorId, original.RootOwnerActorId,
                    original.EntityVersion + (defect == "entity-version" ? 1 : 0));
                records[index] = (RollbackCommandRecord)Activator.CreateInstance(typeof(RollbackCommandRecord),
                    BindingFlags.Instance | BindingFlags.NonPublic, null, new object[]
                    {
                        defect == "command-frame" ? checkpoint.Frame : record.Frame, record.Order, record.CommandType,
                        record.PayloadVersion + (defect == "payload-version" ? 1 : 0), MemoryPackSerializer.Serialize(tombstone),
                    }, null)!;
            }
            else if (defect == "trimmed-journal") commands.Log.TrimBefore(new(Frame + 1));
            else if (defect == "unknown-extra") Spawn("unknown", 2, 150, 150);
            else if (defect == "old-command-key")
                invalid = new(checkpoint.Version, checkpoint.Frame, invalid.Entries.Select(p => p.Key == commands.StateProvider.Key
                    ? new WorldRollbackSnapshotEntry(CommandRollbackStateProvider.DefaultKey, p.Payload) : p).ToArray());

            var notifications = 0;
            using var observer = ObserveProjectileSpawns(_ => notifications++, _ => notifications++, (_, _) => notifications++);
            var live = rollback.Capture(new(Frame));
            var journal = records.Select(r => (r.Frame.Value, r.Order, r.CommandType, r.PayloadVersion,
                Convert.ToHexString(r.Payload.Span))).ToArray();
            Assert.False(rollback.TryRestore(invalid, out var rejected));
            Assert.Equal(0, rejected.ProviderCount);
            Assert.Equal(0, notifications);
            EqualState(live, rollback.Capture(new(Frame)));
            Assert.Equal(journal, records.Select(r => (r.Frame.Value, r.Order, r.CommandType, r.PayloadVersion,
                Convert.ToHexString(r.Payload.Span))).ToArray());
        }
    }
}
