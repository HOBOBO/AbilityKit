using System.Text;
using AbilityKit.Protocol;
using AbilityKit.Protocol.Serialization;
using Newtonsoft.Json;

namespace AbilityKit.Protocol.Tests;

/// <summary>
/// WireSerializer 是纯静态路由：Current 未安装即抛，TextSerializer 懒创建默认 JsonTextSerializer。
/// 安装器会把内部实现的实例装进静态字段，因此这里同时覆盖两个 Installer。
/// </summary>
public sealed class WireSerializerTests
{
    private static void ResetWireSerializer()
    {
        // 属性 setter 接受 null（无校验），用于把静态状态拨回"未安装"。
        WireSerializer.Current = null!;
        WireSerializer.TextSerializer = null!;
    }

    [Fact]
    public void Current_WhenNotInstalled_Throws()
    {
        ResetWireSerializer();

        var ex = Assert.Throws<InvalidOperationException>(() => WireSerializer.Current);

        Assert.Contains("not installed", ex.Message);
    }

    [Fact]
    public void Serialize_WhenNotInstalled_Throws()
    {
        ResetWireSerializer();

        Assert.Throws<InvalidOperationException>(() => WireSerializer.Serialize<TestServerPush>(default));
    }

    [Fact]
    public void Deserialize_WhenNotInstalled_Throws()
    {
        ResetWireSerializer();

        Assert.Throws<InvalidOperationException>(() => WireSerializer.Deserialize<TestServerPush>(new byte[] { 1 }));
    }

    [Fact]
    public void Serialize_RoutesThroughInstalledCurrent()
    {
        ResetWireSerializer();
        var stub = new RecordingWireSerializer();
        WireSerializer.Current = stub;

        var bytes = WireSerializer.Serialize(42);

        Assert.Same(stub.SerializeResult, bytes);
        Assert.Equal(42, (int)stub.LastSerializedValue!);
    }

    [Fact]
    public void Deserialize_ByteArray_RoutesThroughInstalledCurrent()
    {
        ResetWireSerializer();
        var stub = new RecordingWireSerializer();
        WireSerializer.Current = stub;
        var payload = new byte[] { 9, 8, 7 };

        var result = WireSerializer.Deserialize<int>(payload);

        Assert.Same(payload, stub.LastDeserializedArray);
        Assert.Equal(0, result);
    }

    [Fact]
    public void Deserialize_ReadOnlySpan_PassesExactSliceToCurrent()
    {
        ResetWireSerializer();
        var stub = new RecordingWireSerializer();
        WireSerializer.Current = stub;
        var buffer = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        var slice = buffer.AsSpan(3, 4);

        WireSerializer.Deserialize<int>(slice);

        Assert.Equal(new byte[] { 3, 4, 5, 6 }, stub.LastDeserializedSpan);
    }

    [Fact]
    public void Current_AssignNull_ReturnsToNotInstalledState()
    {
        ResetWireSerializer();
        WireSerializer.Current = new RecordingWireSerializer();
        Assert.NotNull(WireSerializer.Current);

        WireSerializer.Current = null!;

        Assert.Throws<InvalidOperationException>(() => WireSerializer.Current);
    }

    [Fact]
    public void TextSerializer_DefaultsToLazyJsonTextSerializer_AndIsCached()
    {
        ResetWireSerializer();

        var first = WireSerializer.TextSerializer;
        var second = WireSerializer.TextSerializer;

        Assert.IsType<JsonTextSerializer>(first);
        Assert.Same(first, second);
    }

    [Fact]
    public void SerializeToText_And_DeserializeFromText_WorkWithoutAnyInstall()
    {
        ResetWireSerializer();
        var dto = new SampleDto { IntValue = 42, StringValue = "文本" };

        var json = WireSerializer.SerializeToText(dto);
        var back = WireSerializer.DeserializeFromText<SampleDto>(json);

        Assert.Equal("{\"IntValue\":42,\"StringValue\":\"文本\"}", json);
        Assert.Equal(dto.IntValue, back.IntValue);
        Assert.Equal(dto.StringValue, back.StringValue);
    }

    [Fact]
    public void SerializeToText_RoutesThroughReplacedTextSerializer()
    {
        ResetWireSerializer();
        var stub = new RecordingTextSerializer { SerializeResult = "{\"custom\":1}" };
        WireSerializer.TextSerializer = stub;
        var dto = new SampleDto { IntValue = 9 };

        var json = WireSerializer.SerializeToText(dto, prettyPrint: true);

        Assert.Equal("{\"custom\":1}", json);
        Assert.True(stub.LastPrettyPrint);
        Assert.Same(dto, stub.LastSerializedValue);
    }

    [Fact]
    public void DeserializeFromText_RoutesThroughReplacedTextSerializer()
    {
        ResetWireSerializer();
        var stub = new RecordingTextSerializer();
        WireSerializer.TextSerializer = stub;

        var result = WireSerializer.DeserializeFromText<int>("not-json-but-stub");

        Assert.Equal("not-json-but-stub", stub.LastDeserializedText);
        Assert.Equal(0, result);
    }

    // ---------- Installer ----------

    [Fact]
    public void MemoryPackInstaller_Serialize_ThrowsWhenMemoryPackUnavailable()
    {
        ResetWireSerializer();

        MemoryPackWireSerializerInstaller.InstallAsCurrent();
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => WireSerializer.Serialize<TestServerPush>(default));

            Assert.Contains("MemoryPack", ex.Message);
        }
        finally
        {
            ResetWireSerializer();
        }
    }

    [Fact]
    public void MemoryPackInstaller_Deserialize_ThrowsWhenMemoryPackUnavailable()
    {
        ResetWireSerializer();

        MemoryPackWireSerializerInstaller.InstallAsCurrent();
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => WireSerializer.Deserialize<TestServerPush>(new byte[] { 1 }));

            Assert.Contains("MemoryPack", ex.Message);
        }
        finally
        {
            ResetWireSerializer();
        }
    }

    [Fact]
    public void JsonInstaller_DefaultOverload_RestoresJsonSemantics()
    {
        ResetWireSerializer();

        JsonTextSerializerInstaller.InstallAsCurrent();
        try
        {
            var json = WireSerializer.SerializeToText(new NullProbeDto { Age = 30 });

            // 证明 TextSerializer 被替换为带 Ignore 语义的 JsonTextSerializer。
            Assert.Equal("{\"Age\":30}", json);
        }
        finally
        {
            ResetWireSerializer();
        }
    }

    [Fact]
    public void JsonInstaller_SettingsOverload_UsesProvidedSettings()
    {
        ResetWireSerializer();

        JsonTextSerializerInstaller.InstallAsCurrent(new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Include,
        });
        try
        {
            var json = WireSerializer.SerializeToText(new NullProbeDto { Name = null, Age = 0 });

            Assert.Equal("{\"Name\":null,\"Age\":0}", json);
        }
        finally
        {
            ResetWireSerializer();
        }
    }

    // ---------- 序列化器互换性 ----------

    [Fact]
    public void JsonBackedWireSerializer_MatchesJsonTextSerializerOutput()
    {
        // JSON 家族内：文本序列化器与 JSON 后端线序列化器产出完全一致，可互换。
        var wire = new JsonBackedWireSerializer();
        var text = new JsonTextSerializer();
        var push = new TestServerPush { Frame = 42L, Health = -1, Alive = true };

        Assert.Equal(text.Serialize(push), Encoding.UTF8.GetString(wire.Serialize(push)));
    }
}
