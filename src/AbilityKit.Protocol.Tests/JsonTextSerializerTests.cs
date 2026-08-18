using AbilityKit.Protocol;
using AbilityKit.Protocol.Serialization;
using Newtonsoft.Json;

namespace AbilityKit.Protocol.Tests;

/// <summary>
/// JsonTextSerializer 的可观测行为：
/// 默认配置为 NullValueHandling.Ignore + DefaultValueHandling.Ignore + ReferenceLoopHandling.Ignore，
/// 因此序列化会丢弃 null 与默认值成员，全默认对象变成 "{}"。
/// </summary>
public sealed class JsonTextSerializerTests
{
    private static readonly JsonTextSerializer Serializer = new();

    // ---------- 序列化：精确线格式 ----------

    [Fact]
    public void Serialize_AllDefaultMembers_ProducesEmptyJsonObject()
    {
        var json = Serializer.Serialize(new SampleDto());

        Assert.Equal("{}", json);
    }

    [Fact]
    public void Serialize_OmitsDefaultAndNullMembers()
    {
        var json = Serializer.Serialize(new SampleDto { IntValue = 7, StringValue = "abc" });

        Assert.Equal("{\"IntValue\":7,\"StringValue\":\"abc\"}", json);
    }

    [Fact]
    public void Serialize_NegativeAndBoundaryIntegers_ExactJson()
    {
        var json = Serializer.Serialize(new SampleDto { IntValue = int.MinValue, LongValue = long.MaxValue });

        Assert.Equal("{\"IntValue\":-2147483648,\"LongValue\":9223372036854775807}", json);
    }

    [Fact]
    public void Serialize_PrimitiveInt_ReturnsBareNumber()
    {
        Assert.Equal("42", Serializer.Serialize(42));
    }

    [Fact]
    public void Serialize_Enum_IsNumericByDefault()
    {
        Assert.Equal("{}", Serializer.Serialize(new EnumProbeDto())); // Off == 0 → 默认值被丢弃
        Assert.Equal("{\"Mode\":1}", Serializer.Serialize(new EnumProbeDto { Mode = TestMode.On }));
        Assert.Equal("{\"Mode\":99}", Serializer.Serialize(new EnumProbeDto { Mode = TestMode.Weird }));
    }

    [Fact]
    public void Serialize_MessageEnvelope_UsesPublicReadonlyFields()
    {
        Assert.Equal("{\"MessageType\":7,\"Payload\":\"abc\"}", Serializer.Serialize(new MessageEnvelope(7, "abc")));
        Assert.Equal("{}", Serializer.Serialize(default(MessageEnvelope)));
    }

    [Fact]
    public void Serialize_EmptyArray_IsWrittenNotOmitted()
    {
        var json = Serializer.Serialize(new SampleDto { IntValue = 3, Numbers = Array.Empty<int>() });

        Assert.Equal("{\"IntValue\":3,\"Numbers\":[]}", json);
    }

    [Fact]
    public void Serialize_NullReferenceValue_ReturnsNullString()
    {
        Assert.Null(Serializer.Serialize<SampleDto>(null!));
        Assert.Null(Serializer.Serialize<string?>(null));
    }

    [Fact]
    public void Serialize_EmptyString_IsQuotedNotOmitted()
    {
        var json = Serializer.Serialize(string.Empty);

        Assert.Equal("\"\"", json);
    }

    [Fact]
    public void PrettyPrint_AddsIndentation()
    {
        var dto = new SampleDto { IntValue = 1, StringValue = "abc" };

        var compact = Serializer.Serialize(dto);
        var pretty = Serializer.Serialize(dto, prettyPrint: true);

        Assert.DoesNotContain('\n', compact);
        Assert.Contains("  \"", pretty);
        Assert.True(pretty.Length > compact.Length);
    }

    // ---------- 往返保真 ----------

    [Fact]
    public void Roundtrip_Properties_PreservesEveryMember()
    {
        var original = new SampleDto
        {
            IntValue = -42,
            LongValue = long.MaxValue - 7L,
            BoolValue = true,
            StringValue = "hello",
            Numbers = new[] { -1, 0, int.MaxValue, int.MinValue },
            Nested = new SampleNested { Name = "子对象", Count = 99 },
        };

        var back = Serializer.Deserialize<SampleDto>(Serializer.Serialize(original));

        Assert.Equal(original.IntValue, back.IntValue);
        Assert.Equal(original.LongValue, back.LongValue);
        Assert.Equal(original.BoolValue, back.BoolValue);
        Assert.Equal(original.StringValue, back.StringValue);
        Assert.Equal(original.Numbers, back.Numbers);
        Assert.Equal(original.Nested!.Name, back.Nested!.Name);
        Assert.Equal(original.Nested.Count, back.Nested.Count);
    }

    [Fact]
    public void Roundtrip_PublicFields_PreservesEveryMember()
    {
        var original = new TestServerPush { Frame = -987654321012345L, Health = int.MinValue, Alive = true };

        var back = Serializer.Deserialize<TestServerPush>(Serializer.Serialize(original));

        Assert.Equal(original.Frame, back.Frame);
        Assert.Equal(original.Health, back.Health);
        Assert.Equal(original.Alive, back.Alive);
    }

    [Fact]
    public void Roundtrip_EmptyArray_IsPreservedNotNull()
    {
        var original = new SampleDto { IntValue = 1, Numbers = Array.Empty<int>() };

        var back = Serializer.Deserialize<SampleDto>(Serializer.Serialize(original));

        Assert.NotNull(back.Numbers);
        Assert.Empty(back.Numbers);
    }

    [Fact]
    public void Roundtrip_SpecialCharactersInStrings_ArePreserved()
    {
        var original = new SampleDto { StringValue = "quote:\" backslash:\\ newline:\n unicode:竞技对战-🙂" };

        var back = Serializer.Deserialize<SampleDto>(Serializer.Serialize(original));

        Assert.Equal(original.StringValue, back.StringValue);
    }

    [Theory]
    [InlineData(long.MinValue)]
    [InlineData(long.MaxValue)]
    [InlineData(-1L)]
    [InlineData(0L)]
    [InlineData(long.MaxValue - 1L)]
    public void Roundtrip_LongBoundaryValues_ArePreserved(long value)
    {
        var original = new LongProbeDto { LongValue = value };

        var back = Serializer.Deserialize<LongProbeDto>(Serializer.Serialize(original));

        Assert.Equal(value, back.LongValue);
    }

    [Theory]
    [InlineData(-123.456)]
    [InlineData(0.5)]
    [InlineData(1e-10)]
    [InlineData(double.MaxValue)]
    [InlineData(double.MinValue)]
    public void Roundtrip_DoubleValues_ArePreserved(double value)
    {
        var original = new DoubleProbeDto { DoubleValue = value };

        var back = Serializer.Deserialize<DoubleProbeDto>(Serializer.Serialize(original));

        Assert.Equal(value, back.DoubleValue);
    }

    [Fact]
    public void Roundtrip_Collections_ListAndDictionary_ArePreserved()
    {
        var original = new CollectionProbeDto
        {
            Numbers = new List<int> { -5, 0, 7, int.MaxValue },
            Stats = new Dictionary<string, int> { ["hp"] = 100, ["伤害"] = -3 },
        };

        var back = Serializer.Deserialize<CollectionProbeDto>(Serializer.Serialize(original));

        Assert.Equal(original.Numbers, back.Numbers);
        Assert.Equal(original.Stats, back.Stats);
    }

    [Fact]
    public void Deserialize_MessageEnvelope_PopulatesReadonlyFields()
    {
        var back = Serializer.Deserialize<MessageEnvelope>("{\"MessageType\":9,\"Payload\":\"xyz\"}");

        Assert.Equal(9, back.MessageType);
        Assert.Equal("xyz", back.Payload);
    }

    // ---------- 反序列化边界与容错 ----------

    [Fact]
    public void Deserialize_NullText_ReturnsDefault()
    {
        Assert.Null(Serializer.Deserialize<string?>(null!));
        Assert.Null(Serializer.Deserialize<SampleDto>(null!));
    }

    [Fact]
    public void Deserialize_EmptyText_ReturnsNullForReferenceType()
    {
        // return default(T) 对引用类型就是 null，而非"字段全默认的实例"。
        Assert.Null(Serializer.Deserialize<SampleDto>(string.Empty));
    }

    [Fact]
    public void Deserialize_EmptyText_ReturnsDefaultForValueType()
    {
        var back = Serializer.Deserialize<TestServerPush>(string.Empty);

        Assert.Equal(0L, back.Frame);
        Assert.Equal(0, back.Health);
        Assert.False(back.Alive);
    }

    [Fact]
    public void Deserialize_InvalidJson_ThrowsJsonException()
    {
        Assert.ThrowsAny<JsonException>(() => Serializer.Deserialize<SampleDto>("not json at all"));
        Assert.ThrowsAny<JsonException>(() => Serializer.Deserialize<SampleDto>("{"));
    }

    [Fact]
    public void Deserialize_TypeMismatchedJson_ThrowsJsonException()
    {
        Assert.ThrowsAny<JsonException>(() => Serializer.Deserialize<SampleDto>("12345"));
    }

    [Fact]
    public void Deserialize_IgnoresUnknownMembers()
    {
        var back = Serializer.Deserialize<SampleDto>("{\"IntValue\":5,\"MysteryField\":123}");

        Assert.Equal(5, back.IntValue);
    }

    [Fact]
    public void Deserialize_IsCaseInsensitive()
    {
        var back = Serializer.Deserialize<SampleDto>("{\"intvalue\":777}");

        Assert.Equal(777, back.IntValue);
    }

    // ---------- 自定义配置 ----------

    [Fact]
    public void CustomSettings_CanIncludeNullMembers()
    {
        var serializer = new JsonTextSerializer(new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include });

        var json = serializer.Serialize(new NullProbeDto { Name = null, Age = 30 });

        Assert.Equal("{\"Name\":null,\"Age\":30}", json);
    }

    [Fact]
    public void CustomSettings_CanIncludeDefaultMembers()
    {
        var serializer = new JsonTextSerializer(new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Include,
            DefaultValueHandling = DefaultValueHandling.Include,
        });

        var json = serializer.Serialize(new NullProbeDto { Name = null, Age = 0 });

        Assert.Equal("{\"Name\":null,\"Age\":0}", json);
    }

    [Fact]
    public void NullSettings_FallsBackToIncludeEverything()
    {
        var serializer = new JsonTextSerializer(null!);

        var json = serializer.Serialize(new NullProbeDto());

        // 默认 JsonSerializerSettings：null 与默认值都写出。
        Assert.Equal("{\"Name\":null,\"Age\":0}", json);
    }
}
