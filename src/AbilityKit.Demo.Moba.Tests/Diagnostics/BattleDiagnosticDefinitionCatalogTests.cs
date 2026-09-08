using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Triggering.Runtime.Config.Plans;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Diagnostics;

public sealed class BattleDiagnosticDefinitionCatalogTests
{
    private readonly BattleDiagnosticSessionScope _scope =
        new("definition-session", "definition-world", 1);

    [Fact]
    public void Catalog_SeparatesSameNumericIdByKindAndKeepsUnresolvedEntries()
    {
        var snapshot = CreateSnapshot();

        Assert.Equal(3, snapshot.Definitions.Items.Count);
        Assert.Equal(1, snapshot.Definitions.UnresolvedCount);
        Assert.False(snapshot.Definitions.IsComplete);

        using var session = new BattleDiagnosticOfflineSession(snapshot);
        var skill = session.QueryDefinition(
            1,
            new BattleDiagnosticDefinitionReference(
                BattleDiagnosticDefinitionKind.Skill,
                101));
        var trigger = session.QueryDefinition(
            2,
            new BattleDiagnosticDefinitionReference(
                BattleDiagnosticDefinitionKind.Trigger,
                101));

        Assert.Equal("Fire Strike", Assert.Single(skill.Items).DisplayName);
        Assert.Equal("On Fire Strike", Assert.Single(trigger.Items).DisplayName);
    }

    [Fact]
    public void JsonByteSerializer_RoundTripsTypedDefinitionMetadata()
    {
        IBattleDiagnosticArtifactSerializer serializer =
            new MobaBattleDiagnosticJsonArtifactSerializer();

        var bytes = serializer.Serialize(CreateSnapshot());
        var restored = serializer.Deserialize(bytes);

        Assert.NotEmpty(bytes);
        Assert.Equal(MobaBattleDiagnosticJsonArtifactSerializer.JsonFormatId,
            serializer.FormatId);
        Assert.Equal(23, restored.Definitions.Revision);
        var definition = restored.Definitions.Items[0];
        Assert.Equal("skill-hash", definition.ContentHash);
        Assert.Equal(BattleDiagnosticDefinitionMetadataValueKind.Integer,
            definition.Metadata[0].ValueKind);
        Assert.Equal(750, definition.Metadata[0].IntegerValue);
    }

    [Fact]
    public void JsonCodec_LegacyArtifactWithoutDefinitionTrackUsesEmptyCatalog()
    {
        var artifact = MobaBattleDiagnosticArtifactCodec.Attach(
            new AbilityKit.Diagnostics.Analysis.AbilityKitAnalysisArtifact(),
            CreateSnapshot());
        artifact.BattleDiagnostics.Definitions = null;
        artifact.BattleDiagnostics.Session.Capabilities &=
            ~(long)BattleDiagnosticCapabilities.Definitions;

        var restored = MobaBattleDiagnosticArtifactCodec.ImportSnapshot(
            MobaBattleDiagnosticArtifactCodec.ExportToString(artifact));

        Assert.Empty(restored.Definitions.Items);
        Assert.False(restored.SessionInfo.Supports(BattleDiagnosticCapabilities.Definitions));
    }

    [Fact]
    public void MemoryPackSerializer_RoundTripsCatalogAsBinaryWireData()
    {
        IBattleDiagnosticDefinitionCatalogSerializer serializer =
            new MobaBattleDiagnosticDefinitionMemoryPackSerializer();

        var bytes = serializer.Serialize(CreateSnapshot().Definitions);
        var restored = serializer.Deserialize(bytes);

        Assert.NotEmpty(bytes);
        Assert.NotEqual((byte)'{', bytes[0]);
        Assert.Equal(
            MobaBattleDiagnosticDefinitionMemoryPackSerializer.MemoryPackFormatId,
            serializer.FormatId);
        Assert.Equal(_scope, restored.Scope);
        Assert.Equal(23, restored.Revision);
        Assert.Equal(3, restored.Items.Count);
        Assert.Equal("skill-hash", restored.Items[0].ContentHash);
        Assert.Equal(750, restored.Items[0].Metadata[0].IntegerValue);
        Assert.Equal(BattleDiagnosticDefinitionResolution.Unresolved,
            restored.Items[2].Resolution);
    }

    [Fact]
    public void RuntimeSource_ResolvesConfigAndTriggerPlanDefinitionsWithoutPersistingRuntimeObjects()
    {
        var configs = new MobaConfigDatabase();
        var reload = configs.ReloadFromDtoArrays(
            new Dictionary<Type, Array>
            {
                [typeof(SkillDTO)] = new[]
                {
                    new SkillDTO
                    {
                        Id = 101,
                        Name = "Fire Strike",
                        CooldownMs = 750,
                        Range = 7,
                        Category = 2,
                        SkillType = 1,
                        Tags = Array.Empty<int>(),
                        PreCastFlowId = 201,
                        CastFlowId = 202
                    }
                }
            },
            strict: false);
        Assert.True(reload.Succeeded, reload.Error);

        var triggerPlans = new TriggerPlanJsonDatabase();
        var plan = new TriggerPlan<object>(
            phase: 0,
            priority: 3,
            triggerId: 701);
        triggerPlans.AddRecord(new TriggerPlanJsonDatabase.Record(
            701,
            "skill.fire",
            81,
            TriggerPlanScope.OwnerBound,
            in plan));

        var source = new MobaBattleDiagnosticDefinitionCatalogSource(
            configs,
            triggerPlans);
        var references = new[]
        {
            new BattleDiagnosticDefinitionReference(
                BattleDiagnosticDefinitionKind.Skill,
                101),
            new BattleDiagnosticDefinitionReference(
                BattleDiagnosticDefinitionKind.Trigger,
                701),
            new BattleDiagnosticDefinitionReference(
                BattleDiagnosticDefinitionKind.Effect,
                701),
            new BattleDiagnosticDefinitionReference(
                BattleDiagnosticDefinitionKind.Buff,
                999)
        };

        var catalog = source.CaptureDefinitionCatalogSnapshot(_scope, references);

        Assert.Equal(configs.Version, catalog.Revision);
        Assert.Equal(4, catalog.Items.Count);
        Assert.Equal(1, catalog.UnresolvedCount);
        Assert.All(catalog.Items, item =>
        {
            Assert.IsType<BattleDiagnosticDefinition>(item);
            Assert.All(item.Metadata, metadata =>
                Assert.IsType<BattleDiagnosticDefinitionMetadataEntry>(metadata));
        });

        Assert.True(catalog.TryResolve(references[0], out var skill));
        Assert.Equal("Fire Strike", skill.DisplayName);
        Assert.Equal("moba.skills", skill.SourcePath);
        Assert.NotEmpty(skill.ContentHash);
        Assert.Contains(skill.Metadata, item =>
            item.Key == "cooldownMs" && item.IntegerValue == 750);

        Assert.True(catalog.TryResolve(references[1], out var trigger));
        Assert.True(catalog.TryResolve(references[2], out var effect));
        Assert.Equal("skill.fire", trigger.DisplayName);
        Assert.Equal("skill.fire", effect.DisplayName);
        Assert.Equal(BattleDiagnosticDefinitionKind.Trigger, trigger.Kind);
        Assert.Equal(BattleDiagnosticDefinitionKind.Effect, effect.Kind);
        Assert.NotEqual(trigger.ContentHash, effect.ContentHash);

        Assert.True(catalog.TryResolve(references[3], out var missing));
        Assert.Equal(BattleDiagnosticDefinitionResolution.Unresolved, missing.Resolution);
        Assert.Empty(missing.Metadata);
    }

    private BattleDiagnosticSessionSnapshot CreateSnapshot()
    {
        var skillReference = new BattleDiagnosticDefinitionReference(
            BattleDiagnosticDefinitionKind.Skill,
            101);
        var triggerReference = new BattleDiagnosticDefinitionReference(
            BattleDiagnosticDefinitionKind.Trigger,
            101);
        var unresolvedReference = new BattleDiagnosticDefinitionReference(
            BattleDiagnosticDefinitionKind.Effect,
            999);
        var definitions = new BattleDiagnosticDefinitionCatalogSnapshot(
            _scope,
            23,
            new[]
            {
                new BattleDiagnosticDefinition(
                    in skillReference,
                    "Fire Strike",
                    "config-7",
                    "skill-hash",
                    "moba.skills",
                    BattleDiagnosticDefinitionResolution.Resolved,
                    new[]
                    {
                        BattleDiagnosticDefinitionMetadataEntry.Integer(
                            "cooldownMs",
                            750)
                    }),
                new BattleDiagnosticDefinition(
                    in triggerReference,
                    "On Fire Strike",
                    "trigger-3",
                    "trigger-hash",
                    "trigger-plans",
                    BattleDiagnosticDefinitionResolution.Resolved),
                BattleDiagnosticDefinition.Unresolved(in unresolvedReference)
            });
        var info = new BattleDiagnosticSessionInfo(
            _scope,
            "Definition Test",
            "build-1",
            1,
            TimeSpan.TicksPerSecond,
            BattleDiagnosticCapabilities.Definitions |
            BattleDiagnosticCapabilities.Export,
            BattleDiagnosticConnectionState.Connected,
            BattleDiagnosticCaptureState.Capturing);
        var eventMetrics = new BattleDiagnosticStoreMetrics(
            16,
            0,
            1,
            0,
            0,
            0,
            true);

        return new BattleDiagnosticSessionSnapshot(
            in info,
            100,
            new BattleDiagnosticEventTrackSnapshot(
                1,
                in eventMetrics,
                Array.Empty<BattleDiagnosticEvent>()),
            new BattleDiagnosticStateTrackSnapshot(
                0,
                BattleDiagnosticFrames.Invalid,
                null,
                Array.Empty<BattleDiagnosticActorSummary>()),
            new BattleDiagnosticTraceTrackSnapshot(
                0,
                Array.Empty<BattleDiagnosticTraceNodeSummary>(),
                false),
            new BattleDiagnosticAttributeTrackSnapshot(
                0,
                BattleDiagnosticFrames.Invalid,
                Array.Empty<BattleDiagnosticActorAttribute>(),
                Array.Empty<BattleDiagnosticActorAttributeModifier>()),
            new BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorBuff>(
                0,
                BattleDiagnosticFrames.Invalid,
                Array.Empty<BattleDiagnosticActorBuff>()),
            new BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorTag>(
                0,
                BattleDiagnosticFrames.Invalid,
                Array.Empty<BattleDiagnosticActorTag>()),
            new BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorEffect>(
                0,
                BattleDiagnosticFrames.Invalid,
                Array.Empty<BattleDiagnosticActorEffect>()),
            definitions: definitions);
    }
}
