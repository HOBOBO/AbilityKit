using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using AbilityKit.Game.Flow;
using Xunit;

namespace AbilityKit.Game.View.Runtime.Tests;

public sealed class MobaBattleFlowProfileTests
{
    [Fact]
    public void WithoutBattleView_KeepsBattleLifecycleAndSnapshotSync()
    {
        var profile = MobaBattleFlowProfile.CreateWithoutBattleView();
        profile.ValidateOrThrow();

        Assert.Equal(new[] { "sync" }, profile.GetFeatures("Battle.InMatch"));
        Assert.DoesNotContain("view", profile.GetFeatures("Battle.InMatch"));
        Assert.DoesNotContain("hud", profile.GetFeatures("Battle.InMatch"));
        Assert.Contains("session", profile.GetFeatures("Battle.Prepare"));
    }

    [Fact]
    public void Profile_RejectsUnsupportedBattleTopology()
    {
        var profile = MobaBattleFlowProfile.CreateStandard();
        profile.TopologyId = "moba.custom";

        Assert.Throws<InvalidOperationException>(() => profile.ValidateOrThrow());
    }

    [Fact]
    public void Profile_ResolvesConfiguredTopologyAndBuildsBattleMachine()
    {
        var profile = MobaBattleFlowProfile.CreateStandard();
        profile.ProfileId = "moba.custom-staged";
        profile.TopologyId = "moba.custom-staged.v1";
        profile.Version = 5;
        profile.TopologyStates = new[]
        {
            new MobaBattleFlowTopologyState { StateId = "Battle.Prepare" },
            new MobaBattleFlowTopologyState { StateId = "Battle.Connect" },
            new MobaBattleFlowTopologyState { StateId = "Battle.CreateOrJoinWorld" },
            new MobaBattleFlowTopologyState { StateId = "Battle.InMatch" },
            new MobaBattleFlowTopologyState { StateId = "Battle.LoadAssets" },
            new MobaBattleFlowTopologyState { StateId = "Battle.End" }
        };
        profile.TopologyTransitions = new[]
        {
            new MobaBattleFlowTopologyTransition { EventId = "PrepareDone", FromStateId = "Battle.Prepare", ToStateId = "Battle.Connect" },
            new MobaBattleFlowTopologyTransition { EventId = "Connected", FromStateId = "Battle.Connect", ToStateId = "Battle.CreateOrJoinWorld" },
            new MobaBattleFlowTopologyTransition { EventId = "JoinedWorld", FromStateId = "Battle.CreateOrJoinWorld", ToStateId = "Battle.InMatch" },
            new MobaBattleFlowTopologyTransition { EventId = "AssetsLoadCompleted", FromStateId = "Battle.InMatch", ToStateId = "Battle.LoadAssets" },
            new MobaBattleFlowTopologyTransition { EventId = "Ended", FromStateId = "Battle.InMatch", ToStateId = "Battle.End" },
            new MobaBattleFlowTopologyTransition { EventId = "Ended", FromStateId = "Battle.LoadAssets", ToStateId = "Battle.End" }
        };

        var composition = profile.Resolve();
        var configuration = MobaFlowConfiguration.CreateResolved(composition);

        Assert.Equal("Battle.InMatch", composition.StateOrder[3]);
        Assert.Equal(MobaBattleState.InMatch, configuration.BattleMachine.States[3]);
        Assert.Equal(MobaBattleEvent.AssetsLoadCompleted, configuration.BattleMachine.Transitions[3].Trigger);
    }

    [Fact]
    public void Profile_RejectsTopologyReferencesOutsideDeclaredStates()
    {
        var profile = MobaBattleFlowProfile.CreateStandard();
        profile.ProfileId = "moba.invalid-topology";
        profile.TopologyId = "moba.invalid.v1";
        profile.Version = 5;
        profile.TopologyStates = new[]
        {
            new MobaBattleFlowTopologyState { StateId = "Battle.Prepare" }
        };
        profile.TopologyTransitions = new[]
        {
            new MobaBattleFlowTopologyTransition
            {
                EventId = "PrepareDone", FromStateId = "Battle.Prepare", ToStateId = "Battle.Connect"
            }
        };

        Assert.Throws<InvalidOperationException>(profile.ValidateOrThrow);
    }

    [Fact]
    public void ResourceDefaultProfile_MatchesStandardComposition()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "battle_flow_profiles.json")));
        var root = document.RootElement;
        var standard = MobaBattleFlowProfile.CreateStandard();
        var configured = JsonSerializer.Deserialize<MobaBattleFlowProfile>(
            root.GetProperty("Profiles")[0].GetRawText(),
            new JsonSerializerOptions { IncludeFields = true });

        Assert.NotNull(configured);
        Assert.Equal(standard.ProfileId, root.GetProperty("DefaultProfileId").GetString());
        Assert.Equal(standard.Version, configured.Version);
        Assert.Equal(standard.SessionModuleIds, configured.SessionModuleIds);
        foreach (var phase in standard.Phases)
        {
            Assert.Equal(standard.GetFeatures(phase.StateId), configured.GetFeatures(phase.StateId));
            Assert.Equal(phase.RetainOnExitFeatureIds,
                configured.Phases.Single(p => p.StateId == phase.StateId).RetainOnExitFeatureIds);
        }
    }

    [Fact]
    public void ResourceProfiles_ProvideDistinctValidatedBattleCompositions()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "battle_flow_profiles.json")));
        var profiles = document.RootElement.GetProperty("Profiles").EnumerateArray()
            .Select(element => JsonSerializer.Deserialize<MobaBattleFlowProfile>(
                element.GetRawText(), new JsonSerializerOptions { IncludeFields = true })!)
            .ToDictionary(profile => profile.ProfileId);

        Assert.Contains("moba.standard", profiles.Keys);
        Assert.Contains("moba.quiet", profiles.Keys);
        foreach (var profile in profiles.Values) profile.ValidateOrThrow();
        Assert.Contains("debug_ongui", profiles["moba.standard"].GetFeatures("Battle.Connect"));
        Assert.Empty(profiles["moba.quiet"].GetFeatures("Battle.Connect"));
        profiles["moba.quiet"].ValidateHostMode("Local");
        Assert.Throws<InvalidOperationException>(() =>
            profiles["moba.quiet"].ValidateHostMode("GatewayRemote"));
    }

    [Fact]
    public void ValidateOrThrow_RejectsMissingAndDuplicatePhases()
    {
        var missing = MobaBattleFlowProfile.CreateStandard();
        missing.Phases = missing.Phases.Skip(1).ToArray();
        Assert.Throws<InvalidOperationException>(missing.ValidateOrThrow);

        var duplicate = MobaBattleFlowProfile.CreateStandard();
        duplicate.Phases[1].StateId = duplicate.Phases[0].StateId;
        Assert.Throws<InvalidOperationException>(duplicate.ValidateOrThrow);
    }

    [Fact]
    public void ValidateOrThrow_RejectsDuplicateFeatures()
    {
        var profile = MobaBattleFlowProfile.CreateStandard();
        profile.Phases[1].FeatureRefs = new[]
        {
            new MobaBattleFlowFeatureRef { GroupId = "debug_overlay" },
            new MobaBattleFlowFeatureRef { FeatureId = "debug_ongui" }
        };

        Assert.Throws<InvalidOperationException>(profile.ValidateOrThrow);
    }

    [Fact]
    public void ValidateOrThrow_RejectsRetentionOutsideThePhase()
    {
        var profile = MobaBattleFlowProfile.CreateStandard();
        profile.Phases[1].RetainOnExitFeatureIds = new[] { "session" };

        Assert.Throws<InvalidOperationException>(profile.ValidateOrThrow);
    }

    [Fact]
    public void ValidateOrThrow_RejectsMissingBattleScopedRetention()
    {
        var profile = MobaBattleFlowProfile.CreateStandard();
        profile.Phases[0].RetainOnExitFeatureIds = Array.Empty<string>();

        Assert.Contains("context", Assert.Throws<InvalidOperationException>(profile.ValidateOrThrow).Message);
    }

    [Fact]
    public void Resolve_RejectsReinstallingAStillRetainedFeature()
    {
        var profile = MobaBattleFlowProfile.CreateStandard();
        profile.Phases[1].FeatureRefs = new[]
        {
            new MobaBattleFlowFeatureRef { FeatureId = "session" }
        };

        Assert.Contains("reinstalls retained feature",
            Assert.Throws<InvalidOperationException>(profile.Resolve).Message);
    }

    [Fact]
    public void Resolve_RejectsEmptyOrDuplicateSessionModules_AndValidatesCapabilities()
    {
        var empty = MobaBattleFlowProfile.CreateStandard();
        empty.SessionModuleIds = Array.Empty<string>();
        Assert.Throws<InvalidOperationException>(empty.Resolve);

        var missing = MobaBattleFlowProfile.CreateStandard();
        missing.SessionModuleIds = missing.SessionModuleIds
            .Where(id => id != "session_plan").ToArray();
        var composition = missing.Resolve();
        Assert.Contains("plan", Assert.Throws<InvalidOperationException>(() =>
            MobaBattleFlowProfile.ValidateSessionCapabilities(composition.SessionModuleIds,
                MobaBattleFlowProfile.GetBuiltInModuleCapabilities)).Message);

        var duplicate = MobaBattleFlowProfile.CreateStandard();
        duplicate.SessionModuleIds = new[] { "session_events", "session_events" };
        Assert.Throws<InvalidOperationException>(duplicate.Resolve);
    }

    [Fact]
    public void SessionCapabilities_AllowAnAlternateModuleImplementation()
    {
        var profile = MobaBattleFlowProfile.CreateStandard();
        profile.SessionModuleIds = profile.SessionModuleIds
            .Where(id => id != "session_lifecycle")
            .Concat(new[] { "custom_lifecycle" }).ToArray();
        var composition = profile.Resolve();

        MobaBattleFlowProfile.ValidateSessionCapabilities(composition.SessionModuleIds,
            id => id == "custom_lifecycle" ? new[] { "lifecycle" } :
                MobaBattleFlowProfile.GetBuiltInModuleCapabilities(id));
    }

    [Fact]
    public void Resolve_AllowsOptionalAndRegisteredExtensionModuleIds()
    {
        var profile = MobaBattleFlowProfile.CreateStandard();
        profile.SessionModuleIds = profile.SessionModuleIds
            .Where(id => id != "session_editor_hooks")
            .Concat(new[] { "custom_observer" }).ToArray();

        Assert.Contains("custom_observer", profile.Resolve().SessionModuleIds);
    }

    [Fact]
    public void Resolve_ExpandsGroupsInReferenceOrderAndCopiesSourceArrays()
    {
        var profile = MobaBattleFlowProfile.CreateStandard();
        profile.Phases[3].FeatureRefs = new[]
        {
            new MobaBattleFlowFeatureRef { GroupId = "debug_overlay" },
            new MobaBattleFlowFeatureRef { FeatureId = "loading_screen" }
        };

        var composition = profile.Resolve();
        profile.Groups[0].FeatureIds[0] = "changed_after_resolution";

        Assert.Equal(new[] { "Battle.Prepare", "Battle.Connect", "Battle.CreateOrJoinWorld",
            "Battle.LoadAssets", "Battle.InMatch", "Battle.End" }, composition.StateOrder);
        Assert.Equal(new[] { "debug_ongui", "loading_screen" },
            composition.GetFeatures("Battle.LoadAssets"));
    }

    [Fact]
    public void Resolve_RejectsUnknownGroupAndMixedReferenceModes()
    {
        var unknown = MobaBattleFlowProfile.CreateStandard();
        unknown.Phases[1].FeatureRefs[0].GroupId = "missing";
        Assert.Throws<InvalidOperationException>(unknown.Resolve);

        var mixed = MobaBattleFlowProfile.CreateStandard();
        mixed.Phases[1].FeatureIds = new[] { "debug_ongui" };
        Assert.Throws<InvalidOperationException>(mixed.Resolve);
    }

    [Fact]
    public void ValidateFactories_RejectsMissingFeatureInsideGroup()
    {
        var composition = MobaBattleFlowProfile.CreateStandard().Resolve();
        var factories = new MobaFeatureFactoryRegistry();
        foreach (var featureId in composition.FeaturesByState.Values.SelectMany(ids => ids).Distinct())
            if (featureId != "debug_ongui")
                factories.Register(featureId, (in GamePhaseContext ctx) => null!);

        var error = Assert.Throws<InvalidOperationException>(() => composition.ValidateFactories(factories));

        Assert.Contains("debug_ongui", error.Message);
    }

    [Fact]
    public void Resolve_AcceptsVersion1FlatFeatureLists()
    {
        var profile = MobaBattleFlowProfile.CreateStandard();
        var expanded = profile.Phases.Select(phase => profile.GetFeatures(phase.StateId)).ToArray();
        profile.Version = 1;
        profile.Groups = Array.Empty<MobaBattleFlowFeatureGroup>();
        for (var i = 0; i < profile.Phases.Length; i++)
        {
            profile.Phases[i].FeatureIds = expanded[i];
            profile.Phases[i].FeatureRefs = Array.Empty<MobaBattleFlowFeatureRef>();
        }

        Assert.Equal(new[] { "loading_screen", "debug_ongui" },
            profile.Resolve().GetFeatures("Battle.LoadAssets"));
    }

    [Fact]
    public void Resolve_LegacyVersionRetainsPrepareFeatures()
    {
        var profile = MobaBattleFlowProfile.CreateStandard();
        profile.Version = 2;
        profile.Phases[0].RetainOnExitFeatureIds = Array.Empty<string>();

        Assert.Equal(profile.GetFeatures("Battle.Prepare"),
            profile.Resolve().GetRetainedFeatures("Battle.Prepare"));
    }

    [Fact]
    public void Resolve_RequiresRegisteredVersion4StageActions()
    {
        var profile = MobaBattleFlowProfile.CreateStandard();
        profile.Version = 4;
        profile.Phases[4].EnterAfterActionIds = new[] { "mode.show_objective" };
        profile.Phases[4].ExitActionIds = new[] { "mode.hide_objective" };

        var composition = profile.Resolve();
        Assert.Contains("mode.show_objective",
            Assert.Throws<InvalidOperationException>(() => composition.ValidateExtensions(null)).Message);

        var registry = new MobaFlowExtensionRegistry()
            .Register("mode.show_objective", (_, _) => { })
            .Register("mode.hide_objective", (_, _) => { });
        composition.ValidateExtensions(registry);
        var configuration = MobaFlowConfiguration.CreateResolved(composition);
        Assert.Contains("mode.show_objective", configuration.BattleInMatchFeatures.EnterAfterActionIds);
        Assert.Contains("mode.hide_objective", configuration.BattleInMatchFeatures.ExitActionIds);

        profile.Version = 3;
        Assert.Throws<InvalidOperationException>(profile.Resolve);
    }

    [Fact]
    public void ExtensionRegistry_ExecutesRegisteredActionAndProtectsLifecycleActions()
    {
        var installedCount = -1;
        var registry = new MobaFlowExtensionRegistry()
            .Register("mode.show_objective", (_, count) => installedCount = count);

        Assert.True(registry.TryExecute("mode.show_objective", default, 3));
        Assert.Equal(3, installedCount);
        Assert.False(registry.TryExecute("mode.unknown", default, 0));
        Assert.Throws<InvalidOperationException>(() => registry.Register(
            MobaFlowActionIds.ReturnLobbyAfterBattleEnd, (_, _) => { }));
    }

    [Fact]
    public void Resolve_RejectsMissingBattleScopeDependency()
    {
        var profile = MobaBattleFlowProfile.CreateStandard();
        profile.Phases[0].FeatureIds = new[] { "context", "session", "gameplay_presentation" };
        profile.Phases[0].RetainOnExitFeatureIds = (string[])profile.Phases[0].FeatureIds.Clone();

        Assert.Contains("entity", Assert.Throws<InvalidOperationException>(profile.Resolve).Message);
    }

    [Fact]
    public void Resolve_RejectsUnknownHostMode()
    {
        var profile = MobaBattleFlowProfile.CreateStandard();
        profile.AllowedHostModes = new[] { "typo" };
        Assert.Contains("typo", Assert.Throws<InvalidOperationException>(profile.Resolve).Message);
    }

    [Fact]
    public void CreateDefault_UsesCustomFeatureListsButKeepsLifecycleTransitions()
    {
        var standard = MobaFlowConfiguration.CreateDefault();
        var profile = MobaBattleFlowProfile.CreateStandard();
        profile.ProfileId = "moba.minimal";
        profile.Phases[1].FeatureIds = Array.Empty<string>();
        profile.Phases[1].FeatureRefs = Array.Empty<MobaBattleFlowFeatureRef>();
        profile.Phases[4].FeatureIds = new[] { "view" };
        profile.Phases[4].FeatureRefs = Array.Empty<MobaBattleFlowFeatureRef>();

        var custom = MobaFlowConfiguration.CreateDefault(profile);

        Assert.Equal("moba.minimal", custom.BattleProfileId);
        Assert.Empty(custom.BattleConnectFeatures.FeatureIds);
        Assert.Equal(new[] { "view" }, custom.BattleInMatchFeatures.FeatureIds);
        Assert.Equal(
            standard.RootMachine.Transitions.Select(t => (t.Trigger, t.From, t.To, t.ConditionId)),
            custom.RootMachine.Transitions.Select(t => (t.Trigger, t.From, t.To, t.ConditionId)));
        Assert.Equal(
            standard.BattleMachine.Transitions.Select(t => (t.Trigger, t.From, t.To, t.ConditionId)),
            custom.BattleMachine.Transitions.Select(t => (t.Trigger, t.From, t.To, t.ConditionId)));
        Assert.Equal(standard.BattleConnectFeatures.SwitchFlowIds, custom.BattleConnectFeatures.SwitchFlowIds);
        Assert.Equal(standard.BattleEndFeatures.EnterAfterActionIds, custom.BattleEndFeatures.EnterAfterActionIds);
    }

    [Fact]
    public void ValidateFactories_RejectsMissingExtensionDependenciesBeforeBattle()
    {
        var profile = MobaBattleFlowProfile.CreateStandard();
        profile.Phases[1].FeatureRefs = new[]
        {
            new MobaBattleFlowFeatureRef { FeatureId = "observer" }
        };
        var composition = profile.Resolve();
        var factories = RegisterCompositionFeatures(composition);
        factories.Register("observer", (in GamePhaseContext ctx) => null!,
            new MobaFeatureContract(requiredFeatureIds: new[] { "missing_owner" }));

        Assert.Contains("missing_owner",
            Assert.Throws<InvalidOperationException>(() => composition.ValidateFactories(factories)).Message);
    }

    [Fact]
    public void ValidateFactories_RequiresDeclaredPortAndRetainedLifetime()
    {
        var profile = MobaBattleFlowProfile.CreateStandard();
        profile.Phases[1].FeatureRefs = new[]
        {
            new MobaBattleFlowFeatureRef { FeatureId = "observer" }
        };
        var composition = profile.Resolve();
        var factories = RegisterCompositionFeatures(composition);
        factories.Register("observer", (in GamePhaseContext ctx) => null!,
            new MobaFeatureContract(requiredPorts: new[] { typeof(IDisposable) }));
        Assert.Contains("IDisposable",
            Assert.Throws<InvalidOperationException>(() => composition.ValidateFactories(factories)).Message);

        var retainedFactories = RegisterCompositionFeatures(composition);
        retainedFactories.Register("observer", (in GamePhaseContext ctx) => null!,
            new MobaFeatureContract(MobaFeatureLifetime.Battle));
        Assert.Contains("retained",
            Assert.Throws<InvalidOperationException>(() => composition.ValidateFactories(retainedFactories)).Message);
    }

    private static MobaFeatureFactoryRegistry RegisterCompositionFeatures(MobaBattleFlowComposition composition)
    {
        var factories = new MobaFeatureFactoryRegistry();
        foreach (var featureId in composition.FeaturesByState.Values.SelectMany(ids => ids).Distinct())
            if (featureId != "observer")
                factories.Register(featureId, (in GamePhaseContext ctx) => null!);
        return factories;
    }
}
