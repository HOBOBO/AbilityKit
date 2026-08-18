using AbilityKit.GameplayTags;
using Xunit;

namespace AbilityKit.GameplayTags.Tests;

/// <summary>DefaultTagSerializer：单标签与容器的字符串序列化往返。</summary>
[Collection(TagTestCollection.Name)]
public sealed class PersistenceTests : TagTestBase
{
    // ---------- 单标签 ----------

    [Fact]
    public void Serialize_ValidTag_ReturnsName()
    {
        var tag = T("P.A.B");

        Assert.Equal("P.A.B", DefaultTagSerializer.Instance.Serialize(tag));
    }

    [Fact]
    public void Serialize_InvalidTag_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, DefaultTagSerializer.Instance.Serialize(GameplayTag.None));
    }

    [Fact]
    public void Deserialize_EmptyOrWhitespace_ReturnsNone()
    {
        Assert.Equal(GameplayTag.None, DefaultTagSerializer.Instance.Deserialize(""));
        Assert.Equal(GameplayTag.None, DefaultTagSerializer.Instance.Deserialize("   "));
        Assert.Equal(GameplayTag.None, DefaultTagSerializer.Instance.Deserialize(null!));
    }

    [Fact]
    public void Deserialize_ExistingTag_ReturnsSameInstance()
    {
        var original = T("P2.A");

        var restored = DefaultTagSerializer.Instance.Deserialize("P2.A");

        Assert.Equal(original, restored);
    }

    [Fact]
    public void Deserialize_UnknownTag_AutoRegisters()
    {
        var restored = DefaultTagSerializer.Instance.Deserialize("P3.A.B");

        Assert.True(restored.IsValid);
        Assert.Equal("P3.A.B", restored.TagName);
        // 反序列化未知标签会走 RequestTag 自动注册，并隐式注册父标签。
        Assert.True(GameplayTagManager.Instance.TryGetTag("P3", out _));
    }

    [Fact]
    public void Serialize_then_Deserialize_round_trips()
    {
        var original = T("P4.A.B");
        var restored = DefaultTagSerializer.Instance.Deserialize(DefaultTagSerializer.Instance.Serialize(original));

        Assert.Equal(original, restored);
    }

    // ---------- 容器 ----------

    [Fact]
    public void SerializeContainer_EmptyOrNull_ReturnsEmptyJsonArray()
    {
        Assert.Equal("[]", DefaultTagSerializer.Instance.SerializeContainer(new GameplayTagContainer()));
        Assert.Equal("[]", DefaultTagSerializer.Instance.SerializeContainer(null!));
    }

    [Fact]
    public void SerializeContainer_WithTags_ReturnsJsonArrayOfNames()
    {
        var container = C("PC.A", "PC.B.C");

        var json = DefaultTagSerializer.Instance.SerializeContainer(container);

        Assert.Equal("[\"PC.A\",\"PC.B.C\"]", json);
    }

    [Fact]
    public void DeserializeContainer_RoundTrips()
    {
        var container = C("PC2.A", "PC2.B");

        var restored = DefaultTagSerializer.Instance.DeserializeContainer(
            DefaultTagSerializer.Instance.SerializeContainer(container));

        Assert.Equal(2, restored.Count);
        Assert.True(restored.HasTagExact(T("PC2.A")));
        Assert.True(restored.HasTagExact(T("PC2.B")));
    }

    [Fact]
    public void DeserializeContainer_MalformedInput_ReturnsEmptyContainer()
    {
        Assert.Equal(0, DefaultTagSerializer.Instance.DeserializeContainer("").Count);
        Assert.Equal(0, DefaultTagSerializer.Instance.DeserializeContainer("no-brackets").Count);
        Assert.Equal(0, DefaultTagSerializer.Instance.DeserializeContainer("   ").Count);
        Assert.Equal(0, DefaultTagSerializer.Instance.DeserializeContainer(null!).Count);
    }

    [Fact]
    public void DeserializeContainer_IgnoresEmptyEntries()
    {
        var restored = DefaultTagSerializer.Instance.DeserializeContainer("[\"PC3.A\",\"\",\"PC3.B\"]");

        Assert.Equal(2, restored.Count);
    }
}
