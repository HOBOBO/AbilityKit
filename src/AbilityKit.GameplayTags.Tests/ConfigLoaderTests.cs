using System;
using System.Collections.Generic;
using AbilityKit.GameplayTags;
using Xunit;

namespace AbilityKit.GameplayTags.Tests;

/// <summary>
/// 验证配置适配保留输入顺序和字段，以及未安装加载器时的行为。
/// </summary>
[Collection(TagTestCollection.Name)]
public sealed class ConfigLoaderTests : TagTestBase
{
    [Fact]
    public void StructEntries_AreForwardedWithMetadataInInputOrder()
    {
        var loader = new RecordingConfigLoader();
        GameplayTagConfigLoader.SetLoader(loader);

        GameplayTagConfigLoader.LoadFromData(new[]
        {
            new TagConfigEntry("State.Ready", "就绪", "State"),
            new TagConfigEntry("Status.Hidden", "隐藏", "Status")
        });

        Assert.Collection(loader.Received,
            first => Assert.Equal(("State.Ready", "就绪", "State"), (first.Name, first.Description, first.Category)),
            second => Assert.Equal(("Status.Hidden", "隐藏", "Status"), (second.Name, second.Description, second.Category)));
    }

    [Fact]
    public void UnconfiguredLoader_DoesNotEnumerateStructEntries()
    {
        GameplayTagConfigLoader.LoadFromData(Entries());

        static IEnumerable<TagConfigEntry> Entries()
        {
            Assert.Fail("未安装加载器时不应枚举配置。");
            yield break;
        }
    }

    [Fact]
    public void LoadFromNames_SkipsBlankNamesButDoesNotSwallowInvalidNames()
    {
        Assert.Throws<ArgumentException>(() => GameplayTagConfigLoader.LoadFromNames(new[]
        {
            " ", "Config.First", "Config..Invalid", "Config.Last"
        }));

        var manager = GameplayTagManager.Instance;
        Assert.True(manager.TryGetTag("Config.First", out _));
        Assert.False(manager.TryGetTag("Config.Last", out _));
    }
}
