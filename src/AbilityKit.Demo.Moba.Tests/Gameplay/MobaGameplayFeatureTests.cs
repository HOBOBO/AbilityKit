using System;
using System.IO;
using System.Linq;
using AbilityKit.Ability.Config;
using AbilityKit.Demo.Moba.Console.Bootstrap;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Gameplay.Structures;
using AbilityKit.Demo.Moba.Share.Config;
using Newtonsoft.Json;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Gameplay;

public sealed class MobaGameplayFeatureTests
{
    [Fact]
    public void Feature_set_supports_enable_disable_and_reset()
    {
        var features = new MobaGameplayFeatureSet();

        Assert.False(features.IsConfigured);
        features.Enable(MobaGameplayFeatureIds.MinionWaves);
        Assert.True(features.IsConfigured);
        Assert.True(features.IsEnabled(MobaGameplayFeatureIds.MinionWaves));
        Assert.Contains(MobaGameplayFeatureIds.MinionWaves, features.EnabledFeatures);

        features.Disable(MobaGameplayFeatureIds.MinionWaves);
        Assert.False(features.IsEnabled(MobaGameplayFeatureIds.MinionWaves));

        features.Enable("custom_feature");
        features.Reset();
        Assert.True(features.IsConfigured);
        Assert.Empty(features.EnabledFeatures);
    }

    [Fact]
    public void Feature_set_rejects_blank_feature_ids()
    {
        var features = new MobaGameplayFeatureSet();

        Assert.Throws<ArgumentException>(() => features.Enable(" "));
        Assert.False(features.IsEnabled(" "));
    }

    [Fact]
    public void Published_gameplay_configuration_declares_optional_modules()
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(root.FullName, "Unity", "Packages", "com.abilitykit.demo.moba.view.runtime",
            "Resources", "luban", "moba", "gameplays.json");
        var gameplays = JsonConvert.DeserializeObject<GameplayDTO[]>(File.ReadAllText(path));

        Assert.NotNull(gameplays);
        Assert.Equal(new[] { MobaGameplayFeatureIds.MinionWaves, MobaGameplayFeatureIds.Structures },
            gameplays!.Single(x => x.Id == 1).FeatureIds);
        Assert.Equal(new[] { MobaGameplayFeatureIds.MinionWaves, MobaGameplayFeatureIds.Structures },
            gameplays.Single(x => x.Id == 2).FeatureIds);
    }

    [Fact]
    public void Gameplay_mo_exposes_published_feature_ids()
    {
        var dto = new GameplayDTO { Id = 7, MapId = 1, FeatureIds = new[] { MobaGameplayFeatureIds.MinionWaves } };
        var gameplay = new AbilityKit.Demo.Moba.Config.BattleDemo.MO.GameplayMO(dto);

        Assert.Equal(new[] { MobaGameplayFeatureIds.MinionWaves }, gameplay.FeatureIds);
    }

    [Fact]
    public void Structure_resource_requires_unique_ids_valid_kind_and_positive_radius()
    {
        const string valid = """
            {"Version":1,"MapId":1,"Structures":[{"Id":1,"Name":"Base","Kind":"Base","TeamId":1,"AttributeTemplateId":2001,"Position":{"X":0,"Y":0,"Z":0},"Radius":1.5}]}
            """;

        var loaded = MobaMapStructureResourceLoader.LoadResource(valid);
        Assert.Single(loaded.Structures);

        Assert.Throws<InvalidOperationException>(() => MobaMapStructureResourceLoader.LoadResource(
            "{\"MapId\":1,\"Structures\":[{\"Id\":1,\"Kind\":\"Base\",\"TeamId\":1,\"AttributeTemplateId\":1,\"Radius\":1},{\"Id\":1,\"Kind\":\"Tower\",\"TeamId\":2,\"AttributeTemplateId\":1,\"Radius\":1}]}"));
        Assert.Throws<InvalidOperationException>(() => MobaMapStructureResourceLoader.LoadResource(
            "{\"MapId\":1,\"Structures\":[{\"Id\":1,\"Kind\":\"Unknown\",\"TeamId\":1,\"AttributeTemplateId\":1,\"Radius\":1}]}"));
        Assert.Throws<InvalidOperationException>(() => MobaMapStructureResourceLoader.LoadResource(
            "{\"MapId\":1,\"Structures\":[{\"Id\":1,\"Kind\":\"Base\",\"TeamId\":1,\"AttributeTemplateId\":1,\"Radius\":0}]}"));
        Assert.Throws<InvalidOperationException>(() => MobaMapStructureResourceLoader.LoadResource(
            "{\"Version\":0,\"MapId\":1,\"Structures\":[]}"));
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null &&
            (!Directory.Exists(Path.Combine(directory.FullName, "LubanConfig")) ||
             !Directory.Exists(Path.Combine(directory.FullName, "Unity"))))
            directory = directory.Parent;

        return directory ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
