using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Combat.Collision;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Services.Projectile;
using MemoryPack;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

/// <summary>
/// Cross-provider semantic checks must fail while the world is still untouched. These tests use
/// the same detached payload parsers as the coordinator, with a tiny synthetic target frame.
/// </summary>
public sealed class MobaRollbackSnapshotSemanticPreflightTests
{
    [Theory]
    [InlineData("missing-projectile")]
    [InlineData("missing-actor")]
    [InlineData("missing-flying-projectile")]
    [InlineData("missing-source-context")]
    [InlineData("mismatched-retain")]
    public void Projectile_relation_semantic_errors_abort_before_any_import(string defect)
    {
        using var fixture = ProjectileFixture.Create(defect);
        var relation = new MobaProjectileLinkRollbackProvider(fixture.Links, fixture.Skills);
        var providers = fixture.CreateProviders(relation, relation.Export(fixture.Frame));
        var coordinator = new RollbackCoordinator(providers.Registry, new RollbackSnapshotRingBuffer(1));

        Assert.False(coordinator.TryRestore(fixture.Snapshot, out var result));
        Assert.Equal(RollbackOperationStatus.ProviderFailed, result.Status);
        Assert.Equal(MobaProjectileLinkRollbackProvider.DefaultKey, result.ProviderKey);
        Assert.All(providers.All, provider => Assert.Equal(0, provider.ImportCount));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Projectile_launcher_relation_requires_target_component(bool hasLauncher)
    {
        using var fixture = ProjectileFixture.CreateLauncher(hasLauncher);
        var relation = new MobaProjectileLinkRollbackProvider(fixture.Links, fixture.Skills);
        var providers = fixture.CreateProviders(relation, relation.Export(fixture.Frame));
        var coordinator = new RollbackCoordinator(providers.Registry, new RollbackSnapshotRingBuffer(1));

        var restored = coordinator.TryRestore(fixture.Snapshot, out var result);
        Assert.True(restored == hasLauncher, result.ToString());
        if (!hasLauncher)
        {
            Assert.Equal(RollbackOperationStatus.ProviderFailed, result.Status);
            Assert.Equal(MobaProjectileLinkRollbackProvider.DefaultKey, result.ProviderKey);
            Assert.All(providers.All, provider => Assert.Equal(0, provider.ImportCount));
        }
    }

    [Fact]
    public void Projectile_relation_coordinator_import_restores_target_relation_state()
    {
        using var fixture = ProjectileFixture.Create("valid");
        var relation = fixture.CreateActualRelationProvider();
        var providers = fixture.CreateActualProviders(relation, relation.Export(fixture.Frame));

        fixture.Links.UnlinkByProjectileId(fixture.PhysicalProjectileId);
        var futureProjectile = new ProjectileId(fixture.PhysicalProjectileId.Value + 1);
        fixture.Links.Link(futureProjectile, fixture.RelationActorId + 1);

        var coordinator = new RollbackCoordinator(providers.Registry, new RollbackSnapshotRingBuffer(1));
        Assert.True(coordinator.TryRestore(providers.Snapshot, out var result), result.ToString());
        Assert.True(fixture.Links.TryGetActorId(fixture.PhysicalProjectileId, out var restoredActor));
        Assert.Equal(fixture.RelationActorId, restoredActor);
        Assert.False(fixture.Links.TryGetActorId(futureProjectile, out _));
    }

    [Fact]
    public void Summon_relation_without_summon_meta_aborts_before_any_import()
    {
        using var fixture = SummonFixture.Create(hasSummonMeta: false);
        var relation = new MobaSummonRollbackProvider(fixture.Summons, fixture.Actors, fixture.Skills);
        var providers = fixture.CreateProviders(relation, relation.Export(fixture.Frame));
        var coordinator = new RollbackCoordinator(providers.Registry, new RollbackSnapshotRingBuffer(1));

        Assert.False(coordinator.TryRestore(fixture.Snapshot, out var result));
        Assert.Equal(RollbackOperationStatus.ProviderFailed, result.Status);
        Assert.Equal(MobaSummonRollbackProvider.DefaultKey, result.ProviderKey);
        Assert.All(providers.All, provider => Assert.Equal(0, provider.ImportCount));
    }

    [Fact]
    public void Summon_relation_coordinator_import_restores_owner_and_source_indexes()
    {
        using var fixture = SummonFixture.Create(hasSummonMeta: true);
        var relation = fixture.CreateActualRelationProvider();
        var providers = fixture.CreateActualProviders(relation, relation.Export(fixture.Frame));

        fixture.SetPerturbedRelationState();

        var coordinator = new RollbackCoordinator(providers.Registry, new RollbackSnapshotRingBuffer(1));
        Assert.True(coordinator.TryRestore(providers.Snapshot, out var result), result.ToString());
        Assert.True(fixture.TryGetRestoredRelation(out var ownerIds, out var source));
        Assert.Equal(new[] { fixture.SummonActorId }, ownerIds);
        Assert.Equal(fixture.SourceContextId, source.SourceContextId);
    }

    private sealed class CountingProvider : IRollbackStateProvider, IRollbackStatePreflightProvider,
        IRollbackSnapshotPreflightProvider
    {
        private readonly byte[] _payload;
        private readonly Action<RollbackSnapshotPreflightContext> _snapshotValidator;

        public CountingProvider(int key, byte[] payload,
            Action<RollbackSnapshotPreflightContext> snapshotValidator = null)
        {
            Key = key;
            _payload = payload ?? Array.Empty<byte>();
            _snapshotValidator = snapshotValidator;
        }

        public int Key { get; }
        public int ImportCount { get; private set; }
        public byte[] Export(FrameIndex frame) => (byte[])_payload.Clone();
        public void ValidateImport(FrameIndex frame, byte[] payload) { }
        public void ValidateSnapshot(RollbackSnapshotPreflightContext context) => _snapshotValidator?.Invoke(context);
        public void Import(FrameIndex frame, byte[] payload) => ImportCount++;
    }

    private sealed class ProviderSet
    {
        public ProviderSet(RollbackRegistry registry, CountingProvider[] all)
        {
            Registry = registry;
            All = all;
        }

        public RollbackRegistry Registry { get; }
        public CountingProvider[] All { get; }
    }

    private sealed class ActualProviderSet
    {
        public ActualProviderSet(RollbackRegistry registry, WorldRollbackSnapshot snapshot)
        {
            Registry = registry;
            Snapshot = snapshot;
        }

        public RollbackRegistry Registry { get; }
        public WorldRollbackSnapshot Snapshot { get; }
    }

    private abstract class FixtureBase : IDisposable
    {
        protected FixtureBase(int frame, int actorId, byte[] entityPayload, byte[] componentPayload,
            byte[] lifecyclePayload)
        {
            Frame = new FrameIndex(frame);
            ActorId = actorId;
            EntityPayload = entityPayload;
            ComponentPayload = componentPayload;
            LifecyclePayload = lifecyclePayload;
        }

        public FrameIndex Frame { get; }
        public int ActorId { get; }
        public byte[] EntityPayload { get; protected set; }
        public byte[] ComponentPayload { get; protected set; }
        public byte[] LifecyclePayload { get; protected set; }
        public WorldRollbackSnapshot Snapshot { get; protected set; }

        public abstract ProviderSet CreateProviders(IRollbackStateProvider relation, byte[] relationPayload);
        public virtual void Dispose() { }

        protected ProviderSet BuildProviders(int relationKey, IRollbackStateProvider relation,
            byte[] relationPayload, byte[] projectilePayload = null)
        {
            var all = new List<CountingProvider>
            {
                new CountingProvider(MobaEntitasEntityRollbackProvider.DefaultKey, EntityPayload),
                new CountingProvider(MobaEntitasComponentRollbackProvider.DefaultKey, ComponentPayload),
                new CountingProvider(MobaSkillLifecycleRollbackProvider.DefaultKey, LifecyclePayload),
            };
            if (projectilePayload != null)
                all.Add(new CountingProvider(ProjectileRollbackProvider.DefaultKey, projectilePayload));

            var relationCounting = new CountingProvider(
                relationKey,
                relationPayload,
                context => ((IRollbackSnapshotPreflightProvider)relation).ValidateSnapshot(context));
            all.Add(relationCounting);

            var registry = new RollbackRegistry();
            foreach (var provider in all) registry.Register(provider);
            Snapshot = new WorldRollbackSnapshot(
                WorldRollbackSnapshotCodec.CurrentVersion,
                Frame,
                all.ConvertAll(provider => new WorldRollbackSnapshotEntry(provider.Key, provider.Export(Frame))).ToArray());
            return new ProviderSet(registry, all.ToArray());
        }

        protected ActualProviderSet BuildActualProviders(IRollbackStateProvider relation,
            byte[] relationPayload, byte[] projectilePayload = null)
        {
            var providers = new List<IRollbackStateProvider>
            {
                new CountingProvider(MobaEntitasEntityRollbackProvider.DefaultKey, EntityPayload),
                new CountingProvider(MobaEntitasComponentRollbackProvider.DefaultKey, ComponentPayload),
                new CountingProvider(MobaSkillLifecycleRollbackProvider.DefaultKey, LifecyclePayload),
            };
            if (projectilePayload != null)
                providers.Add(new CountingProvider(ProjectileRollbackProvider.DefaultKey, projectilePayload));
            providers.Add(relation);

            var registry = new RollbackRegistry();
            foreach (var provider in providers) registry.Register(provider);
            var entries = new WorldRollbackSnapshotEntry[providers.Count];
            for (var i = 0; i < providers.Count; i++)
                entries[i] = new WorldRollbackSnapshotEntry(providers[i].Key, i == providers.Count - 1
                    ? relationPayload
                    : providers[i].Export(Frame));
            var snapshot = new WorldRollbackSnapshot(
                WorldRollbackSnapshotCodec.CurrentVersion, Frame, entries);
            return new ActualProviderSet(registry, snapshot);
        }
    }

    private sealed class ProjectileFixture : FixtureBase
    {
        private const int SourceActorId = 11;
        private const int RelationActorIdValue = 101;
        private const int ProjectileConfigId = 301;
        private const long SourceContextId = MobaExecutionContextRegistry.FirstExecutionContextId;

        private readonly ActorContext _context;
        private readonly CollisionService _collisions;
        private readonly ProjectileService _projectiles;
        private readonly MobaActorRegistry _actors;
        private readonly byte[] _projectilePayload;
        private readonly MobaExecutionContextRegistry _executionContexts;

        private ProjectileFixture(string defect, bool hasLauncher)
            : base(
                frame: 0,
                actorId: RelationActorIdValue,
                entityPayload: null,
                componentPayload: null,
                lifecyclePayload: null)
        {
            _context = new ActorContext();
            var actor = _context.CreateEntity();
            actor.AddActorId(RelationActorIdValue);
            if (defect != "missing-flying-projectile" && !hasLauncher) actor.isFlyingProjectileTag = true;
            if (hasLauncher) actor.AddProjectileLauncher(1, ProjectileConfigId, RelationActorIdValue, 1000, 1, 0, 0, 1);

            _actors = new MobaActorRegistry();
            _actors.Register(RelationActorIdValue, actor);
            EntityPayload = MemoryPackSerializer.Serialize(new MobaEntitasEntityRollbackPayload(
                1, RelationActorIdValue + 1, new[] { RelationActorIdValue }, Array.Empty<long>()));
            ComponentPayload = new MobaEntitasComponentRollbackProvider(_context).Export(Frame);

            _collisions = new CollisionService();
            _projectiles = new ProjectileService(_collisions);
            var position = Vec3.Zero;
            var projectileParams = new ProjectileSpawnParams(
                SourceActorId, ProjectileConfigId, 0, SourceActorId, Frame.Value,
                in position, Vec3.Right, 1f, 0, 0f, 0f, 100, 100f, -1, default);
            var physicalId = _projectiles.Spawn(in projectileParams);
            PhysicalProjectileId = physicalId;
            _projectilePayload = new ProjectileRollbackProvider(_projectiles).Export(Frame);

            var links = new MobaProjectileLinkService();
            Links = links;
            Skills = new MobaSkillCastRuntimeService();
            _executionContexts = new MobaExecutionContextRegistry();
            SetPrivateField(Skills, "_executionContexts", _executionContexts);
            _executionContexts.Create(new MobaExecutionContextCreateRequest(
                MobaExecutionKind.ProjectileLaunch, ProjectileConfigId, SourceActorId, 12, frame: Frame.Value));
            var relationActor = defect == "missing-actor" ? RelationActorIdValue + 1 : RelationActorIdValue;
            var sourceContext = defect == "missing-source-context" ? SourceContextId + 99 : SourceContextId;
            var source = new ProjectileSourceContext(
                SourceActorId, 12, ProjectileConfigId, sourceContext, SourceContextId, SourceContextId,
                default, default);
            if (defect == "launcher")
            {
                links.BindLauncherSource(relationActor, in source);
            }
            else
            {
                var relationProjectile = new ProjectileId(defect == "missing-projectile" ? physicalId.Value + 1 : physicalId.Value);
                links.Link(relationProjectile, relationActor);
                links.BindSource(relationProjectile, in source);
            }
            if (defect == "mismatched-retain")
            {
                var runtime = new MobaSkillCastRuntimeHandle(1, 1, SourceContextId);
                var child = new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Projectile,
                    physicalId.Value, SourceContextId, ProjectileConfigId);
                var retain = new MobaSkillRuntimeRetainHandle(1, in runtime, in child);
                var retainMap = (Dictionary<ProjectileId, MobaSkillRuntimeRetainHandle>)typeof(MobaProjectileLinkService)
                    .GetField("_retainByProjectile", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(links)!;
                retainMap[new ProjectileId(defect == "missing-projectile" ? physicalId.Value + 1 : physicalId.Value)] = retain;
            }

            LifecyclePayload = BuildLifecyclePayload(Frame.Value, SourceContextId,
                MobaExecutionKind.ProjectileLaunch, SourceActorId, ProjectileConfigId);
            Snapshot = new WorldRollbackSnapshot(WorldRollbackSnapshotCodec.CurrentVersion, Frame, Array.Empty<WorldRollbackSnapshotEntry>());
        }

        public MobaProjectileLinkService Links { get; }
        public MobaSkillCastRuntimeService Skills { get; }
        public ProjectileId PhysicalProjectileId { get; }
        public int RelationActorId => 101;

        public MobaProjectileLinkRollbackProvider CreateActualRelationProvider() =>
            new MobaProjectileLinkRollbackProvider(Links, Skills, _projectiles, _actors);

        public ActualProviderSet CreateActualProviders(MobaProjectileLinkRollbackProvider relation,
            byte[] relationPayload) => BuildActualProviders(relation, relationPayload, _projectilePayload);

        public static ProjectileFixture Create(string defect) => new ProjectileFixture(defect, hasLauncher: false);
        public static ProjectileFixture CreateLauncher(bool hasLauncher) => new ProjectileFixture("launcher", hasLauncher);

        public override ProviderSet CreateProviders(IRollbackStateProvider relation, byte[] relationPayload) =>
            BuildProviders(MobaProjectileLinkRollbackProvider.DefaultKey, relation, relationPayload, _projectilePayload);

        public override void Dispose()
        {
            Skills.Dispose();
            _executionContexts.Dispose();
            _projectiles.Dispose();
            _collisions.Dispose();
            _context.DestroyAllEntities();
        }
    }

    private sealed class SummonFixture : FixtureBase
    {
        private const int SourceActorId = 11;
        private const int SummonActorIdValue = 201;
        private const int SummonConfigId = 801;
        private const long SourceContextIdValue = MobaExecutionContextRegistry.FirstExecutionContextId;
        private readonly ActorContext _context;
        private readonly MobaExecutionContextRegistry _executionContexts;

        private SummonFixture(bool hasSummonMeta)
            : base(0, SummonActorIdValue, null, null, null)
        {
            _context = new ActorContext();
            var summon = _context.CreateEntity();
            summon.AddActorId(SummonActorIdValue);
            summon.AddOwnerLink(SourceActorId, SourceActorId);
            if (hasSummonMeta) summon.AddSummonMeta(SummonConfigId, true);
            Actors = new MobaActorRegistry();
            Actors.Register(SummonActorIdValue, summon);
            EntityPayload = MemoryPackSerializer.Serialize(new MobaEntitasEntityRollbackPayload(
                1, SummonActorIdValue + 1, new[] { SummonActorIdValue }, Array.Empty<long>()));
            ComponentPayload = new MobaEntitasComponentRollbackProvider(_context).Export(Frame);
            Skills = new MobaSkillCastRuntimeService();
            _executionContexts = new MobaExecutionContextRegistry();
            SetPrivateField(Skills, "_executionContexts", _executionContexts);
            _executionContexts.Create(new MobaExecutionContextCreateRequest(
                MobaExecutionKind.SummonSpawn, SummonConfigId, SourceActorId, SummonActorIdValue, frame: Frame.Value));
            Summons = new MobaSummonService();
            var owners = (Dictionary<int, List<int>>)typeof(MobaSummonService)
                .GetField("_summonsByRootOwner", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(Summons)!;
            owners[SourceActorId] = new List<int> { SummonActorIdValue };
            var source = new SummonSourceContext(SourceActorId, SummonActorIdValue, SummonConfigId,
                SourceContextIdValue, SourceContextIdValue, SourceContextIdValue, default, default);
            var sources = (Dictionary<int, SummonSourceContext>)typeof(MobaSummonService)
                .GetField("_sourceBySummonActorId", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(Summons)!;
            sources[SummonActorIdValue] = source;
            typeof(MobaSummonService).GetField("_registry", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(Summons, Actors);
            LifecyclePayload = BuildLifecyclePayload(Frame.Value, SourceContextIdValue,
                MobaExecutionKind.SummonSpawn, SourceActorId, SummonConfigId);
            Snapshot = new WorldRollbackSnapshot(WorldRollbackSnapshotCodec.CurrentVersion, Frame, Array.Empty<WorldRollbackSnapshotEntry>());
        }

        public MobaSummonService Summons { get; }
        public MobaActorRegistry Actors { get; }
        public MobaSkillCastRuntimeService Skills { get; }
        public int SummonActorId => 201;
        public long SourceContextId => MobaExecutionContextRegistry.FirstExecutionContextId;

        public MobaSummonRollbackProvider CreateActualRelationProvider() =>
            new MobaSummonRollbackProvider(Summons, Actors, Skills);

        public ActualProviderSet CreateActualProviders(MobaSummonRollbackProvider relation,
            byte[] relationPayload) => BuildActualProviders(relation, relationPayload);

        public void SetPerturbedRelationState()
        {
            var owners = (Dictionary<int, List<int>>)typeof(MobaSummonService)
                .GetField("_summonsByRootOwner", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(Summons)!;
            owners[SourceActorId].Clear();
            owners[9] = new List<int> { 999 };
            var sources = (Dictionary<int, SummonSourceContext>)typeof(MobaSummonService)
                .GetField("_sourceBySummonActorId", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(Summons)!;
            sources.Clear();
            Assert.True(Actors.TryGet(SummonActorId, out var summon));
            summon.ReplaceOwnerLink(9, 9);
        }

        public bool TryGetRestoredRelation(out int[] ownerIds, out SummonSourceContext source)
        {
            var owners = (Dictionary<int, List<int>>)typeof(MobaSummonService)
                .GetField("_summonsByRootOwner", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(Summons)!;
            ownerIds = owners.TryGetValue(SourceActorId, out var ids) ? ids.ToArray() : Array.Empty<int>();
            var sources = (Dictionary<int, SummonSourceContext>)typeof(MobaSummonService)
                .GetField("_sourceBySummonActorId", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(Summons)!;
            return sources.TryGetValue(SummonActorId, out source);
        }

        public static SummonFixture Create(bool hasSummonMeta) => new SummonFixture(hasSummonMeta);

        public override ProviderSet CreateProviders(IRollbackStateProvider relation, byte[] relationPayload) =>
            BuildProviders(MobaSummonRollbackProvider.DefaultKey, relation, relationPayload);

        public override void Dispose()
        {
            Skills.Dispose();
            _executionContexts.Dispose();
            _context.DestroyAllEntities();
        }
    }

    private static byte[] BuildLifecyclePayload(int frame, long contextId, MobaExecutionKind kind,
        int sourceActorId, int configId)
    {
        var node = new MobaSkillExecutionContextRollbackEntry(
            contextId, 0, contextId, contextId, (int)kind, configId, sourceActorId, 0,
            frame, 0, 0, false, 0, 0, 0, 0);
        var execution = new MobaExecutionRegistryCheckpoint
        {
            Version = 1,
            NextContextId = contextId + 1,
            Nodes = MemoryPackSerializer.Serialize(new[] { node }),
        };
        var runtimeState = MemoryPackSerializer.Serialize(new MobaSkillRuntimeRollbackPayload(
            3, 1, 1, 1, Array.Empty<MobaSkillRuntimeRollbackEntry>(),
            Array.Empty<MobaSkillRuntimeRetainRollbackEntry>()));
        var local = new MobaSkillRuntimeLocalRollbackPayload(
            1, runtimeState, new[] { node }, contextId + 1);
        var checkpoint = new MobaSkillLifecycleCheckpoint
        {
            Format = MobaSkillLifecycleCheckpoint.FormatName,
            Version = MobaSkillLifecycleCheckpoint.CurrentVersion,
            Frame = frame,
            ActorIds = Array.Empty<int>(),
            Sections = new[]
            {
                new MobaSkillLifecycleSection { Name = "context-entities", Payload = new byte[] { 1 } },
                new MobaSkillLifecycleSection { Name = "execution-contexts", Payload = MobaSkillLifecycleCheckpointCodec.Encode(execution) },
                new MobaSkillLifecycleSection { Name = "skill-runtime", Payload = MemoryPackSerializer.Serialize(local) },
                new MobaSkillLifecycleSection { Name = "areas", Payload = new byte[] { 1 } },
                new MobaSkillLifecycleSection { Name = "buffs", Payload = new byte[] { 1 } },
                new MobaSkillLifecycleSection { Name = "buff-local", Payload = new byte[] { 1 } },
                new MobaSkillLifecycleSection { Name = "continuous", Payload = new byte[] { 1 } },
                new MobaSkillLifecycleSection { Name = "trigger-modifiers", Payload = new byte[] { 1 } },
                new MobaSkillLifecycleSection { Name = "pipelines", Payload = new byte[] { 1 } },
                new MobaSkillLifecycleSection { Name = "owner-blackboards", Payload = new byte[] { 1 } },
                new MobaSkillLifecycleSection { Name = "continuous-local", Payload = new byte[] { 1 } },
            },
        };
        return MobaSkillLifecycleCheckpointCodec.Encode(checkpoint);
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(target, value);
    }
}
