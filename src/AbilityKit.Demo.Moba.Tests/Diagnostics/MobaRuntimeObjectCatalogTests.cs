using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Observability;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Diagnostics;

public sealed class MobaRuntimeObjectCatalogTests
{
    private static readonly BattleDiagnosticSessionScope Scope =
        new("runtime-objects", "world", 1);

    [Fact]
    public void ReusedRuntimeId_GetsANewGenerationAndResolvesByFrame()
    {
        var catalog = CreateCatalog();

        Observe(catalog, MobaRuntimeObjectLifecycleStage.Created, 42, 10);
        Observe(catalog, MobaRuntimeObjectLifecycleStage.Destroyed, 42, 20);
        Observe(catalog, MobaRuntimeObjectLifecycleStage.Created, 42, 30);

        var snapshot = catalog.CaptureObjectCatalogSnapshot();
        Assert.Equal(2, snapshot.Items.Count);
        Assert.Equal(1, snapshot.Items[0].Generation);
        Assert.Equal(2, snapshot.Items[1].Generation);
        Assert.True(catalog.TryResolve(MobaRuntimeObjectKind.Projectile, 42, 15, out var first));
        Assert.Equal(1, first.Generation);
        Assert.False(catalog.TryResolve(MobaRuntimeObjectKind.Projectile, 42, 25, out _));
        Assert.True(catalog.TryResolve(MobaRuntimeObjectKind.Projectile, 42, 35, out var second));
        Assert.Equal(2, second.Generation);
        Assert.True(catalog.TryResolve(MobaRuntimeObjectKind.Projectile, 42, -1, out var latest));
        Assert.Equal(second, latest);
    }

    [Fact]
    public void DuplicateCreated_MergesMetadataWithoutCreatingAnotherGeneration()
    {
        var catalog = CreateCatalog();

        Observe(catalog, MobaRuntimeObjectLifecycleStage.Created, 7, 10);
        Observe(
            catalog,
            MobaRuntimeObjectLifecycleStage.Created,
            7,
            11,
            definitionId: 301,
            sourceActorId: 9,
            displayName: "Arc Bolt");

        var item = Assert.Single(catalog.CaptureObjectCatalogSnapshot().Items);
        Assert.Equal(1, item.Generation);
        Assert.Equal(10, item.CreatedFrame);
        Assert.Equal(301, item.DefinitionId);
        Assert.Equal(9, item.SourceActorId);
        Assert.Equal("Arc Bolt", item.DisplayName);
        Assert.Equal(
            BattleDiagnosticRuntimeObjectDiscoveryKind.LifecycleCreated,
            item.DiscoveryKind);
        Assert.Equal(-1, item.BackfilledFrame);
    }

    [Fact]
    public void DestroyedWithoutCreated_ProducesAnEndedOrphanRecord()
    {
        var catalog = CreateCatalog();

        Observe(catalog, MobaRuntimeObjectLifecycleStage.Destroyed, 8, 25, endReason: 4);

        var item = Assert.Single(catalog.CaptureObjectCatalogSnapshot().Items);
        Assert.Equal(-1, item.CreatedFrame);
        Assert.Equal(25, item.DestroyedFrame);
        Assert.Equal(BattleDiagnosticRuntimeObjectState.Ended, item.State);
        Assert.Equal(4, item.EndReason);
        Assert.Equal(
            BattleDiagnosticRuntimeObjectDiscoveryKind.LifecycleEndedOnly,
            item.DiscoveryKind);
        Assert.Equal(BattleDiagnosticDataCompleteness.Unreliable, item.Completeness);
    }

    [Fact]
    public void Capacity_PrefersEndedRecordsAndNeverReusesAnEvictedKey()
    {
        var catalog = CreateCatalog(capacity: 2);

        Observe(catalog, MobaRuntimeObjectLifecycleStage.Created, 1, 1);
        Observe(catalog, MobaRuntimeObjectLifecycleStage.Destroyed, 1, 2);
        Observe(catalog, MobaRuntimeObjectLifecycleStage.Created, 2, 3);
        Observe(catalog, MobaRuntimeObjectLifecycleStage.Created, 3, 4);

        var truncated = catalog.CaptureObjectCatalogSnapshot();
        Assert.True(truncated.Truncated);
        Assert.Equal(new long[] { 2, 3 }, truncated.Items.Select(item => item.RuntimeId));

        Observe(catalog, MobaRuntimeObjectLifecycleStage.Created, 1, 5);

        var recreated = Assert.Single(
            catalog.CaptureObjectCatalogSnapshot().Items,
            item => item.RuntimeId == 1);
        Assert.Equal(4, recreated.Generation);
    }

    [Fact]
    public void DisabledCatalog_DoesNotCreateStorageOrAdvanceRevision()
    {
        var catalog = CreateCatalog(enabled: false);

        Observe(catalog, MobaRuntimeObjectLifecycleStage.Created, 1, 1);

        Assert.False(catalog.IsEnabled);
        var snapshot = catalog.CaptureObjectCatalogSnapshot();
        Assert.Empty(snapshot.Items);
        Assert.Equal(0, snapshot.Revision);
        Assert.False(snapshot.Truncated);
    }

    [Fact]
    public void MissingCapturePolicy_DefaultsToDisabled()
    {
        var catalog = new MobaRuntimeObjectCatalogService(Scope, capacity: 4);

        Observe(catalog, MobaRuntimeObjectLifecycleStage.Created, 1, 1);

        Assert.False(catalog.IsEnabled);
        Assert.Empty(catalog.CaptureObjectCatalogSnapshot().Items);
    }

    [Fact]
    public void MetricsMode_DoesNotCaptureAndEventsModeEnablesCatalog()
    {
        var collector = new MobaBattleDiagnosticEventCollector(
            Scope,
            BattleDiagnosticCaptureOptions.RecommendedDefault);
        var catalog = new MobaRuntimeObjectCatalogService(collector);

        Observe(catalog, MobaRuntimeObjectLifecycleStage.Created, 1, 1);
        Assert.False(catalog.IsEnabled);
        Assert.Empty(catalog.CaptureObjectCatalogSnapshot().Items);

        collector.CaptureMode = BattleDiagnosticCaptureMode.Events;
        Observe(catalog, MobaRuntimeObjectLifecycleStage.Created, 1, 2);

        Assert.True(catalog.IsEnabled);
        Assert.Single(catalog.CaptureObjectCatalogSnapshot().Items);
    }

    [Fact]
    public void MetricsMode_RegistersContributorWithoutCapturingUntilEventsAreEnabled()
    {
        var currentFrame = 12;
        var collector = new MobaBattleDiagnosticEventCollector(
            Scope,
            BattleDiagnosticCaptureOptions.RecommendedDefault,
            frameProvider: () => currentFrame);
        var catalog = new MobaRuntimeObjectCatalogService(collector);
        var contributor = new TestBootstrapContributor(runtimeId: 41);

        Assert.True(((IMobaRuntimeObjectBootstrapRegistry)catalog).Register(contributor));
        Assert.True(((IMobaRuntimeObjectBootstrapRegistry)catalog).Register(contributor));
        Assert.Equal(0, contributor.CaptureCount);
        Assert.Empty(catalog.CaptureObjectCatalogSnapshot().Items);

        currentFrame = 24;
        collector.CaptureMode = BattleDiagnosticCaptureMode.Events;

        var item = Assert.Single(catalog.CaptureObjectCatalogSnapshot().Items);
        Assert.Equal(1, contributor.CaptureCount);
        Assert.Equal(24, item.CreatedFrame);
        Assert.Equal(41, item.RuntimeId);
        Assert.Equal(
            BattleDiagnosticRuntimeObjectDiscoveryKind.ActiveBackfill,
            item.DiscoveryKind);
        Assert.Equal(24, item.BackfilledFrame);
        Assert.Equal(BattleDiagnosticDataCompleteness.Partial, item.Completeness);
        var health = catalog.CaptureObjectCatalogSnapshot();
        Assert.Equal(1, health.BackfillAttemptCount);
        Assert.Equal(0, health.BackfillFailureCount);
        Assert.Equal(24, health.LastBackfillFrame);

        Observe(
            catalog,
            MobaRuntimeObjectLifecycleStage.Created,
            41,
            25,
            definitionId: 301,
            displayName: "Arc Bolt");
        var enriched = Assert.Single(catalog.CaptureObjectCatalogSnapshot().Items);
        Assert.Equal(BattleDiagnosticRuntimeObjectDiscoveryKind.ActiveBackfill, enriched.DiscoveryKind);
        Assert.Equal(24, enriched.BackfilledFrame);
        Assert.Equal("Arc Bolt", enriched.DisplayName);
    }

    [Fact]
    public void BackfilledObject_ResolvesEventGenerationImmediately()
    {
        var collector = new MobaBattleDiagnosticEventCollector(
            Scope,
            BattleDiagnosticCaptureOptions.RecommendedDefault,
            frameProvider: () => 30,
            timestampProvider: () => 40L);
        var catalog = new MobaRuntimeObjectCatalogService(collector);
        var contributor = new TestBootstrapContributor(runtimeId: 51);
        ((IMobaRuntimeObjectBootstrapRegistry)catalog).Register(contributor);

        collector.EnabledChannels = BattleDiagnosticEventChannel.TemporaryEntity;
        collector.CaptureMode = BattleDiagnosticCaptureMode.Events;
        var draft = new MobaBattleDiagnosticEventDraft(
            BattleDiagnosticEventKind.ProjectileHit,
            BattleDiagnosticEventChannel.TemporaryEntity,
            subjectObject: BattleDiagnosticRuntimeObjectReference.Create(
                BattleDiagnosticRuntimeObjectKind.Projectile,
                51));

        Assert.True(collector.TryCollect(in draft));
        Assert.True(Assert.Single(collector.Store.CaptureEventSnapshot().Events)
            .SubjectObject.IsResolved);
    }

    [Fact]
    public void ReenablingEvents_DoesNotCreateAnotherGenerationForActiveObject()
    {
        var collector = new MobaBattleDiagnosticEventCollector(
            Scope,
            BattleDiagnosticCaptureOptions.RecommendedDefault,
            frameProvider: () => 10);
        var catalog = new MobaRuntimeObjectCatalogService(collector);
        var contributor = new TestBootstrapContributor(runtimeId: 61);
        ((IMobaRuntimeObjectBootstrapRegistry)catalog).Register(contributor);

        collector.CaptureMode = BattleDiagnosticCaptureMode.Events;
        var first = Assert.Single(catalog.CaptureObjectCatalogSnapshot().Items);
        collector.CaptureMode = BattleDiagnosticCaptureMode.Metrics;
        collector.CaptureMode = BattleDiagnosticCaptureMode.Full;
        var second = Assert.Single(catalog.CaptureObjectCatalogSnapshot().Items);

        Assert.Equal(2, contributor.CaptureCount);
        Assert.Equal(first.Generation, second.Generation);
        Assert.Equal(first.CreatedFrame, second.CreatedFrame);
        Assert.Equal(2, catalog.CaptureObjectCatalogSnapshot().BackfillAttemptCount);
    }

    [Fact]
    public void ContributorRegisteredAfterEventsAreEnabled_IsCapturedImmediately()
    {
        var collector = new MobaBattleDiagnosticEventCollector(
            Scope,
            capacity: 8,
            frameProvider: () => 70);
        var catalog = new MobaRuntimeObjectCatalogService(collector);
        var contributor = new TestBootstrapContributor(runtimeId: 71);

        Assert.True(((IMobaRuntimeObjectBootstrapRegistry)catalog).Register(contributor));

        Assert.Equal(1, contributor.CaptureCount);
        Assert.Equal(70, Assert.Single(catalog.CaptureObjectCatalogSnapshot().Items).CreatedFrame);
        Assert.Equal(
            BattleDiagnosticDataCompleteness.Partial,
            catalog.CaptureObjectCatalogSnapshot().Completeness);
    }

    [Fact]
    public void FailingContributor_DoesNotPreventOtherContributorsFromBackfilling()
    {
        var collector = new MobaBattleDiagnosticEventCollector(
            Scope,
            BattleDiagnosticCaptureOptions.RecommendedDefault,
            frameProvider: () => 80);
        var catalog = new MobaRuntimeObjectCatalogService(collector);
        var failing = new TestBootstrapContributor(runtimeId: 0, throws: true);
        var succeeding = new TestBootstrapContributor(runtimeId: 81);
        var registry = (IMobaRuntimeObjectBootstrapRegistry)catalog;
        registry.Register(failing);
        registry.Register(succeeding);

        collector.CaptureMode = BattleDiagnosticCaptureMode.Events;

        Assert.Equal(1, failing.CaptureCount);
        Assert.Equal(1, succeeding.CaptureCount);
        var snapshot = catalog.CaptureObjectCatalogSnapshot();
        Assert.Equal(81, Assert.Single(snapshot.Items).RuntimeId);
        Assert.Equal(2, snapshot.BackfillAttemptCount);
        Assert.Equal(1, snapshot.BackfillFailureCount);
        Assert.Equal(80, snapshot.LastBackfillFrame);
        Assert.Equal(BattleDiagnosticDataCompleteness.Unreliable, snapshot.Completeness);
    }

    [Fact]
    public void UnregisteredAndEmptyContributors_DoNotCreateBackfillRecords()
    {
        var collector = new MobaBattleDiagnosticEventCollector(
            Scope,
            BattleDiagnosticCaptureOptions.RecommendedDefault,
            frameProvider: () => 90);
        var catalog = new MobaRuntimeObjectCatalogService(collector);
        var removed = new TestBootstrapContributor(runtimeId: 91);
        var empty = new TestBootstrapContributor(runtimeId: 0);
        var registry = (IMobaRuntimeObjectBootstrapRegistry)catalog;
        registry.Register(removed);
        registry.Unregister(removed);
        registry.Register(empty);

        collector.CaptureMode = BattleDiagnosticCaptureMode.Events;

        Assert.Equal(0, removed.CaptureCount);
        Assert.Equal(1, empty.CaptureCount);
        Assert.Empty(catalog.CaptureObjectCatalogSnapshot().Items);
    }

    [Fact]
    public void FreezeStopsWritesAndClearPreservesTrackRevision()
    {
        var catalog = CreateCatalog();
        Observe(catalog, MobaRuntimeObjectLifecycleStage.Created, 1, 1);

        catalog.SetFrozen(true);
        Observe(catalog, MobaRuntimeObjectLifecycleStage.Destroyed, 1, 2);
        Assert.Equal(BattleDiagnosticRuntimeObjectState.Active,
            Assert.Single(catalog.CaptureObjectCatalogSnapshot().Items).State);

        catalog.SetFrozen(false);
        Observe(catalog, MobaRuntimeObjectLifecycleStage.Destroyed, 1, 3);
        catalog.Clear();

        var cleared = catalog.CaptureObjectCatalogSnapshot();
        Assert.Empty(cleared.Items);
        Assert.Equal(3, cleared.Revision);
    }

    [Fact]
    public void ArtifactRoundTrip_PreservesObjectsAndLegacyV1DefaultsToEmptyTrack()
    {
        var item = new BattleDiagnosticRuntimeObject(
            BattleDiagnosticRuntimeObjectKind.Area,
            runtimeId: 77,
            generation: 3,
            BattleDiagnosticDefinitionKind.Area,
            definitionId: 701,
            relatedActorId: 0,
            ownerActorId: 5,
            sourceActorId: 5,
            targetActorId: 9,
            createdFrame: 10,
            destroyedFrame: 20,
            rootContextId: 800,
            contextId: 801,
            BattleDiagnosticRuntimeObjectState.Ended,
            endReason: 2,
            displayName: "Fire Field",
            BattleDiagnosticRuntimeObjectDiscoveryKind.ActiveBackfill,
            backfilledFrame: 10);
        var snapshot = CreateSnapshot(
            new BattleDiagnosticObjectCatalogSnapshot(
                Scope,
                12,
                true,
                new[] { item },
                backfillAttemptCount: 3,
                backfillFailureCount: 1,
                lastBackfillFrame: 10));

        var json = MobaBattleDiagnosticArtifactCodec.ExportSnapshotToString(snapshot);
        var restored = MobaBattleDiagnosticArtifactCodec.ImportSnapshot(json);

        Assert.Contains("\"objects\"", json);
        Assert.Contains("\"completeness\"", json);
        Assert.Contains("\"summary\"", json);
        Assert.Equal(12, restored.Objects.Revision);
        Assert.True(restored.Objects.Truncated);
        var restoredItem = Assert.Single(restored.Objects.Items);
        Assert.Equal(item.RuntimeId, restoredItem.RuntimeId);
        Assert.Equal(item.Generation, restoredItem.Generation);
        Assert.Equal(item.DefinitionId, restoredItem.DefinitionId);
        Assert.Equal(item.DisplayName, restoredItem.DisplayName);
        Assert.Equal(item.DiscoveryKind, restoredItem.DiscoveryKind);
        Assert.Equal(10, restoredItem.BackfilledFrame);
        Assert.Equal(3, restored.Objects.BackfillAttemptCount);
        Assert.Equal(1, restored.Objects.BackfillFailureCount);
        Assert.Equal(10, restored.Objects.LastBackfillFrame);
        Assert.Equal(1, restored.Objects.Summary.TotalCount);
        Assert.Equal(1, restored.Objects.Summary.PartialCount);
        Assert.Equal(1, restored.Objects.Summary.EndedCount);

        var root = JObject.Parse(json);
        var objectTrack = (JObject)root["battleDiagnostics"]!["objects"]!;
        objectTrack.Remove("summary");
        objectTrack.Remove("backfillAttemptCount");
        objectTrack.Remove("backfillFailureCount");
        objectTrack.Remove("lastBackfillFrame");
        var objectItem = (JObject)objectTrack["items"]![0]!;
        objectItem.Remove("discoveryKind");
        objectItem.Remove("backfilledFrame");
        var legacyFields = MobaBattleDiagnosticArtifactCodec.ImportSnapshot(root.ToString());
        Assert.Equal(0, legacyFields.Objects.BackfillAttemptCount);
        Assert.Equal(0, legacyFields.Objects.BackfillFailureCount);
        Assert.Equal(-1, legacyFields.Objects.LastBackfillFrame);
        Assert.Equal(1, legacyFields.Objects.Summary.TotalCount);
        Assert.Equal(1, legacyFields.Objects.Summary.UnreliableCount);
        var legacyItem = Assert.Single(legacyFields.Objects.Items);
        Assert.Equal(BattleDiagnosticRuntimeObjectDiscoveryKind.Unknown, legacyItem.DiscoveryKind);
        Assert.Equal(-1, legacyItem.BackfilledFrame);

        Assert.True(((JObject)root["battleDiagnostics"]!).Remove("objects"));
        var legacy = MobaBattleDiagnosticArtifactCodec.ImportSnapshot(root.ToString());
        Assert.Empty(legacy.Objects.Items);
        Assert.Equal(0, legacy.Objects.Revision);
    }

    [Fact]
    public void AllLocalCapabilities_AdvertiseRuntimeObjectCatalog()
    {
        Assert.True((BattleDiagnosticCapabilities.AllLocal &
                     BattleDiagnosticCapabilities.RuntimeObjects) != 0);
    }

    [Fact]
    public void Collector_ResolvesActorAndSubjectKeysWhenWritingEvent()
    {
        var collector = new MobaBattleDiagnosticEventCollector(
            Scope,
            capacity: 8,
            frameProvider: () => 10,
            timestampProvider: () => 20L);
        var catalog = new MobaRuntimeObjectCatalogService(collector);
        Observe(catalog, MobaRuntimeObjectLifecycleStage.Created, 101, 1,
            kind: MobaRuntimeObjectKind.Actor);
        Observe(catalog, MobaRuntimeObjectLifecycleStage.Created, 202, 1,
            kind: MobaRuntimeObjectKind.Actor);
        Observe(catalog, MobaRuntimeObjectLifecycleStage.Created, 303, 2);
        var draft = new MobaBattleDiagnosticEventDraft(
            BattleDiagnosticEventKind.ProjectileHit,
            BattleDiagnosticEventChannel.TemporaryEntity,
            BattleDiagnosticEventOutcome.Succeeded,
            sourceActorId: 101,
            targetActorId: 202,
            subjectObject: BattleDiagnosticRuntimeObjectReference.Create(
                BattleDiagnosticRuntimeObjectKind.Projectile,
                303));

        Assert.True(collector.TryCollect(in draft));

        var diagnosticEvent = Assert.Single(collector.Store.CaptureEventSnapshot().Events);
        Assert.Equal(1, diagnosticEvent.SourceActor.Generation);
        Assert.Equal(2, diagnosticEvent.TargetActor.Generation);
        Assert.Equal(3, diagnosticEvent.SubjectObject.Generation);
        Assert.True(diagnosticEvent.SourceActor.IsResolved);
        Assert.True(diagnosticEvent.SubjectObject.IsResolved);
    }

    [Fact]
    public void OfflineSession_ResolvesExportedEventReferenceAndLegacyReferenceByFrame()
    {
        var runtimeObject = new BattleDiagnosticRuntimeObject(
            BattleDiagnosticRuntimeObjectKind.Projectile,
            runtimeId: 303,
            generation: 4,
            BattleDiagnosticDefinitionKind.Projectile,
            definitionId: 9001,
            relatedActorId: 30,
            ownerActorId: 10,
            sourceActorId: 10,
            targetActorId: 20,
            createdFrame: 5,
            destroyedFrame: 15,
            rootContextId: 80,
            contextId: 81,
            BattleDiagnosticRuntimeObjectState.Ended,
            endReason: 2,
            displayName: "Arc Bolt");
        var objects = new BattleDiagnosticObjectCatalogSnapshot(
            Scope,
            revision: 7,
            truncated: false,
            new[] { runtimeObject });
        var exact = runtimeObject.Reference;
        var legacy = BattleDiagnosticRuntimeObjectReference.Create(
            BattleDiagnosticRuntimeObjectKind.Projectile,
            303);
        using var session = new BattleDiagnosticOfflineSession(CreateSnapshot(objects));

        var exactResult = session.QueryRuntimeObject(1, in exact, frame: 10);
        var legacyResult = session.QueryRuntimeObject(2, in legacy, frame: 10);
        var outsideLifetime = session.QueryRuntimeObject(3, in legacy, frame: 20);

        Assert.Equal("Arc Bolt", Assert.Single(exactResult.Items).DisplayName);
        Assert.Equal(4, Assert.Single(legacyResult.Items).Generation);
        Assert.Equal(BattleDiagnosticDataAvailability.NotCaptured,
            outsideLifetime.Status.Availability);
    }

    [Fact]
    public void LocalSession_AdvertisesAndQueriesRuntimeObjectStore()
    {
        var collector = new MobaBattleDiagnosticEventCollector(
            Scope,
            capacity: 8,
            frameProvider: () => 10);
        var catalog = new MobaRuntimeObjectCatalogService(collector);
        Observe(catalog, MobaRuntimeObjectLifecycleStage.Created, 303, 2,
            definitionId: 9001,
            displayName: "Arc Bolt");
        var session = new MobaBattleDiagnosticLocalSession(
            collector.Store,
            collector.StateStore,
            traceStore: null,
            attributeStore: null,
            buffStore: null,
            tagStore: null,
            effectStore: null,
            runtimeObjectStore: catalog);
        var reference = BattleDiagnosticRuntimeObjectReference.Create(
            BattleDiagnosticRuntimeObjectKind.Projectile,
            303);

        var result = ((IBattleDiagnosticRuntimeObjectSession)session)
            .QueryRuntimeObject(1, in reference, frame: 10);

        Assert.True(session.SessionInfo.Supports(BattleDiagnosticCapabilities.RuntimeObjects));
        Assert.Equal("Arc Bolt", Assert.Single(result.Items).DisplayName);
        Assert.Equal(catalog.Revision,
            ((IBattleDiagnosticRuntimeObjectSession)session).RuntimeObjectStoreRevision);

        var catalogSession = (IBattleDiagnosticRuntimeObjectCatalogSession)session;
        var summary = Assert.Single(catalogSession.QueryRuntimeObjectSummary(2).Items);
        Assert.Equal(1, summary.TotalCount);
        Assert.Equal(1, summary.CompleteCount);
        var filtered = catalogSession.QueryRuntimeObjects(
            new BattleDiagnosticRuntimeObjectQuery(
                3,
                new BattleDiagnosticRuntimeObjectFilter(
                    completeness: BattleDiagnosticDataCompleteness.Complete),
                new BattleDiagnosticPageRequest(catalog.Revision, 0, 10)));
        Assert.Equal(303, Assert.Single(filtered.Items).RuntimeId);
    }

    [Fact]
    public void OfflineCatalogQuery_FiltersCompletenessAndProvidesStableSummary()
    {
        var complete = CreateRuntimeObject(
            1,
            BattleDiagnosticRuntimeObjectDiscoveryKind.LifecycleCreated,
            BattleDiagnosticRuntimeObjectState.Active);
        var partial = CreateRuntimeObject(
            2,
            BattleDiagnosticRuntimeObjectDiscoveryKind.ActiveBackfill,
            BattleDiagnosticRuntimeObjectState.Active,
            backfilledFrame: 10);
        var unreliable = CreateRuntimeObject(
            3,
            BattleDiagnosticRuntimeObjectDiscoveryKind.LifecycleEndedOnly,
            BattleDiagnosticRuntimeObjectState.Ended,
            createdFrame: -1,
            destroyedFrame: 20);
        var objects = new BattleDiagnosticObjectCatalogSnapshot(
            Scope,
            9,
            false,
            new[] { complete, partial, unreliable },
            backfillAttemptCount: 1,
            backfillFailureCount: 0,
            lastBackfillFrame: 10);
        using var session = new BattleDiagnosticOfflineSession(CreateSnapshot(objects));
        var catalogSession = (IBattleDiagnosticRuntimeObjectCatalogSession)session;

        var filtered = catalogSession.QueryRuntimeObjects(
            new BattleDiagnosticRuntimeObjectQuery(
                1,
                new BattleDiagnosticRuntimeObjectFilter(
                    state: BattleDiagnosticRuntimeObjectState.Active,
                    completeness: BattleDiagnosticDataCompleteness.Partial),
                new BattleDiagnosticPageRequest(9, 0, 10)));
        var summary = Assert.Single(catalogSession.QueryRuntimeObjectSummary(2).Items);

        Assert.Equal(2, Assert.Single(filtered.Items).RuntimeId);
        Assert.Equal(3, summary.TotalCount);
        Assert.Equal(1, summary.CompleteCount);
        Assert.Equal(1, summary.PartialCount);
        Assert.Equal(1, summary.UnreliableCount);
        Assert.Equal(2, summary.ActiveCount);
        Assert.Equal(1, summary.EndedCount);
        Assert.Equal(BattleDiagnosticDataCompleteness.Unreliable, summary.Completeness);

        var firstPage = catalogSession.QueryRuntimeObjects(
            new BattleDiagnosticRuntimeObjectQuery(
                3,
                default,
                new BattleDiagnosticPageRequest(9, 0, 1)));
        Assert.Single(firstPage.Items);
        Assert.True(firstPage.Status.HasMore);

        var stale = catalogSession.QueryRuntimeObjects(
            new BattleDiagnosticRuntimeObjectQuery(
                4,
                default,
                new BattleDiagnosticPageRequest(8, 0, 10)));
        Assert.Equal(BattleDiagnosticDataAvailability.Evicted, stale.Status.Availability);
    }

    [Fact]
    public void SessionSnapshot_AggregatesRuntimeObjectEventReferenceCoverage()
    {
        var complete = CreateRuntimeObject(
            1,
            BattleDiagnosticRuntimeObjectDiscoveryKind.LifecycleCreated,
            BattleDiagnosticRuntimeObjectState.Active);
        var partial = CreateRuntimeObject(
            2,
            BattleDiagnosticRuntimeObjectDiscoveryKind.ActiveBackfill,
            BattleDiagnosticRuntimeObjectState.Active,
            backfilledFrame: 10,
            createdFrame: 10);
        var unreliable = CreateRuntimeObject(
            3,
            BattleDiagnosticRuntimeObjectDiscoveryKind.LifecycleEndedOnly,
            BattleDiagnosticRuntimeObjectState.Ended,
            createdFrame: -1,
            destroyedFrame: 20);
        var objects = new BattleDiagnosticObjectCatalogSnapshot(
            Scope,
            9,
            false,
            new[] { complete, partial, unreliable });
        var events = new[]
        {
            CreateSubjectEvent(1, 1, generation: 1),
            CreateSubjectEvent(2, 2, generation: 2),
            CreateSubjectEvent(3, 3, generation: 3),
            CreateSubjectEvent(4, 99),
            new BattleDiagnosticEvent(
                Scope,
                frame: 10,
                sequence: 5,
                monotonicTimestamp: 5,
                BattleDiagnosticEventKind.SkillRuntimeStarted,
                BattleDiagnosticEventChannel.Skill,
                BattleDiagnosticEventOutcome.Succeeded)
        };

        var snapshot = CreateSnapshot(objects, events);
        var coverage = snapshot.RuntimeObjectEventCoverage;

        Assert.Equal(5, coverage.EventCount);
        Assert.Equal(4, coverage.ReferencedEventCount);
        Assert.Equal(1, coverage.CompleteEventCount);
        Assert.Equal(1, coverage.PartialEventCount);
        Assert.Equal(2, coverage.UnreliableEventCount);
        Assert.Equal(4, coverage.TotalReferenceCount);
        Assert.Equal(3, coverage.ResolvedReferenceCount);
        Assert.Equal(1, coverage.UnresolvedReferenceCount);
        Assert.Equal(0.75f, coverage.ResolvedReferenceRatio);

        var json = MobaBattleDiagnosticArtifactCodec.ExportSnapshotToString(snapshot);
        Assert.Contains("\"eventCoverage\"", json);
        Assert.Contains("\"unresolvedReferenceCount\": 1", json);
    }

    [Fact]
    public void ArtifactRoundTrip_PreservesEventKeysAndOldV1KeepsUnresolvedActorIds()
    {
        var sourceEvent = new BattleDiagnosticEvent(
            Scope,
            frame: 10,
            sequence: 1,
            monotonicTimestamp: 20,
            BattleDiagnosticEventKind.AreaSpawned,
            BattleDiagnosticEventChannel.TemporaryEntity,
            BattleDiagnosticEventOutcome.Succeeded,
            sourceActorId: 11,
            targetActorId: 12,
            sourceActorGeneration: 2,
            targetActorGeneration: 3,
            subjectObjectKind: BattleDiagnosticRuntimeObjectKind.Area,
            subjectRuntimeId: 77,
            subjectGeneration: 4);
        var json = MobaBattleDiagnosticArtifactCodec.ExportSnapshotToString(
            CreateSnapshot(
                BattleDiagnosticObjectCatalogSnapshot.Empty(Scope),
                new[] { sourceEvent }));

        var restored = Assert.Single(
            MobaBattleDiagnosticArtifactCodec.ImportSnapshot(json).Events.Events);
        Assert.Equal(2, restored.SourceActor.Generation);
        Assert.Equal(3, restored.TargetActor.Generation);
        Assert.Equal(4, restored.SubjectObject.Generation);
        Assert.Equal(BattleDiagnosticRuntimeObjectKind.Area, restored.SubjectObject.Kind);

        var root = JObject.Parse(json);
        var eventJson = (JObject)root["battleDiagnostics"]!["events"]!["items"]![0]!;
        eventJson.Remove("sourceActorGeneration");
        eventJson.Remove("targetActorGeneration");
        eventJson.Remove("subjectObjectKind");
        eventJson.Remove("subjectRuntimeId");
        eventJson.Remove("subjectGeneration");
        var legacy = Assert.Single(
            MobaBattleDiagnosticArtifactCodec.ImportSnapshot(root.ToString()).Events.Events);

        Assert.Equal(11, legacy.SourceActorId);
        Assert.Equal(12, legacy.TargetActorId);
        Assert.False(legacy.SourceActor.IsResolved);
        Assert.False(legacy.TargetActor.IsResolved);
        Assert.False(legacy.SubjectObject.HasRuntimeId);
    }

    private static MobaRuntimeObjectCatalogService CreateCatalog(
        int capacity = MobaRuntimeObjectCatalogService.DefaultCapacity,
        bool enabled = true)
    {
        return new MobaRuntimeObjectCatalogService(Scope, capacity, () => enabled);
    }

    private static void Observe(
        MobaRuntimeObjectCatalogService catalog,
        MobaRuntimeObjectLifecycleStage stage,
        long runtimeId,
        int frame,
        int definitionId = 0,
        long sourceActorId = 0,
        int endReason = 0,
        string displayName = "",
        MobaRuntimeObjectKind kind = MobaRuntimeObjectKind.Projectile)
    {
        var observation = new MobaRuntimeObjectLifecycleObservation(
            stage,
            kind,
            runtimeId,
            frame,
            kind == MobaRuntimeObjectKind.Actor
                ? MobaRuntimeObjectDefinitionKind.Actor
                : MobaRuntimeObjectDefinitionKind.Projectile,
            definitionId,
            sourceActorId: sourceActorId,
            endReason: endReason,
            displayName: displayName);
        catalog.OnObserved(in observation);
    }

    private static BattleDiagnosticSessionSnapshot CreateSnapshot(
        BattleDiagnosticObjectCatalogSnapshot objects,
        IList<BattleDiagnosticEvent>? events = null)
    {
        var info = new BattleDiagnosticSessionInfo(
            Scope,
            "Runtime object test",
            "build",
            schemaVersion: 1,
            TimeSpan.TicksPerSecond,
            BattleDiagnosticCapabilities.AllLocal,
            BattleDiagnosticConnectionState.Connected,
            BattleDiagnosticCaptureState.Capturing);
        events ??= Array.Empty<BattleDiagnosticEvent>();
        var metrics = new BattleDiagnosticStoreMetrics(
            Math.Max(1, events.Count),
            events.Count,
            0,
            events.Count,
            0,
            0,
            false);
        return new BattleDiagnosticSessionSnapshot(
            in info,
            capturedAtTimestamp: 1,
            new BattleDiagnosticEventTrackSnapshot(0, in metrics, events),
            new BattleDiagnosticStateTrackSnapshot(0, -1, null, null),
            new BattleDiagnosticTraceTrackSnapshot(0, null, false),
            new BattleDiagnosticAttributeTrackSnapshot(0, -1, null, null),
            new BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorBuff>(0, -1, null),
            new BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorTag>(0, -1, null),
            new BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorEffect>(0, -1, null),
            objects);
    }

    private static BattleDiagnosticRuntimeObject CreateRuntimeObject(
        long runtimeId,
        BattleDiagnosticRuntimeObjectDiscoveryKind discoveryKind,
        BattleDiagnosticRuntimeObjectState state,
        int backfilledFrame = -1,
        int createdFrame = 1,
        int destroyedFrame = -1)
    {
        return new BattleDiagnosticRuntimeObject(
            BattleDiagnosticRuntimeObjectKind.Projectile,
            runtimeId,
            generation: (int)runtimeId,
            BattleDiagnosticDefinitionKind.Projectile,
            definitionId: 100 + (int)runtimeId,
            relatedActorId: 0,
            ownerActorId: 0,
            sourceActorId: 0,
            targetActorId: 0,
            createdFrame,
            destroyedFrame,
            rootContextId: 0,
            contextId: 0,
            state,
            endReason: 0,
            displayName: string.Empty,
            discoveryKind,
            backfilledFrame);
    }

    private static BattleDiagnosticEvent CreateSubjectEvent(
        long sequence,
        long runtimeId,
        int generation = 0)
    {
        return new BattleDiagnosticEvent(
            Scope,
            frame: 10,
            sequence,
            monotonicTimestamp: sequence,
            BattleDiagnosticEventKind.ProjectileHit,
            BattleDiagnosticEventChannel.TemporaryEntity,
            BattleDiagnosticEventOutcome.Succeeded,
            subjectObjectKind: BattleDiagnosticRuntimeObjectKind.Projectile,
            subjectRuntimeId: runtimeId,
            subjectGeneration: generation);
    }

    private sealed class TestBootstrapContributor : IMobaRuntimeObjectBootstrapContributor
    {
        private readonly long _runtimeId;
        private readonly bool _throws;

        public TestBootstrapContributor(long runtimeId, bool throws = false)
        {
            _runtimeId = runtimeId;
            _throws = throws;
        }

        public int CaptureCount { get; private set; }

        public void CaptureActiveRuntimeObjects(
            IMobaRuntimeObjectLifecycleHook hook,
            int frame)
        {
            CaptureCount++;
            if (_throws) throw new InvalidOperationException("Backfill failed.");
            if (_runtimeId == 0L) return;

            var observation = new MobaRuntimeObjectLifecycleObservation(
                MobaRuntimeObjectLifecycleStage.Created,
                MobaRuntimeObjectKind.Projectile,
                _runtimeId,
                frame,
                MobaRuntimeObjectDefinitionKind.Projectile,
                definitionId: 101);
            hook.TryObserve(in observation);
        }
    }
}
