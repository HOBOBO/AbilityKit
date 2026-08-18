using System.Text;
using AbilityKit.Protocol.Serialization;

namespace AbilityKit.Protocol.Tests;

/// <summary>
/// 记录调用并返回预制结果；不做真实序列化，保证确定性。
/// </summary>
public sealed class RecordingWireSerializer : IWireSerializer
{
    public byte[] SerializeResult { get; set; } = new byte[] { 0xAB, 0xCD, 0xEF };

    public object? LastSerializedValue { get; private set; }
    public byte[]? LastDeserializedArray { get; private set; }
    public byte[]? LastDeserializedSpan { get; private set; }
    public bool DeserializeShouldThrow { get; set; }

    public byte[] Serialize<T>(in T value)
    {
        LastSerializedValue = value;
        return SerializeResult;
    }

    public T Deserialize<T>(byte[] bytes)
    {
        if (DeserializeShouldThrow) throw new InvalidOperationException("stub deserialize failure");
        LastDeserializedArray = bytes;
        return default!;
    }

    public T Deserialize<T>(ReadOnlySpan<byte> bytes)
    {
        if (DeserializeShouldThrow) throw new InvalidOperationException("stub deserialize failure");
        LastDeserializedSpan = bytes.ToArray();
        return default!;
    }
}

/// <summary>
/// 以 JsonTextSerializer 为后端的线格式序列化器。
/// 用于在没有 MemoryPack 的宿主里，给 ProtocolRegistry / ServerPushHandlerBase 提供真实往返能力。
/// </summary>
public sealed class JsonBackedWireSerializer : IWireSerializer
{
    private readonly JsonTextSerializer _text = new();

    public byte[] Serialize<T>(in T value) => Encoding.UTF8.GetBytes(_text.Serialize(value));

    public T Deserialize<T>(byte[] bytes) => _text.Deserialize<T>(Encoding.UTF8.GetString(bytes));

    public T Deserialize<T>(ReadOnlySpan<byte> bytes) => _text.Deserialize<T>(Encoding.UTF8.GetString(bytes));
}

/// <summary>
/// 记录调用并返回预制文本的 ITextSerializer。
/// </summary>
public sealed class RecordingTextSerializer : ITextSerializer
{
    public string SerializeResult { get; set; } = "{\"stub\":true}";

    public object? LastSerializedValue { get; private set; }
    public bool LastPrettyPrint { get; private set; }
    public string? LastDeserializedText { get; private set; }

    public string Serialize<T>(T value, bool prettyPrint = false)
    {
        LastSerializedValue = value;
        LastPrettyPrint = prettyPrint;
        return SerializeResult;
    }

    public T Deserialize<T>(string text)
    {
        LastDeserializedText = text;
        return default!;
    }
}
