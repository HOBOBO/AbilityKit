using AbilityKit.Protocol;
using AbilityKit.Protocol.Serialization;

namespace AbilityKit.Protocol.Tests;

/// <summary>
/// ServerPushHandlerBase 的解码路径通过全局 ProtocolRegistry.Instance 的当前序列化器完成，
/// 因此每个解码测试都显式 SetSerializer 建立基线。
/// </summary>
public sealed class ServerPushHandlerBaseTests
{
    private readonly ProtocolRegistry _protocol = ProtocolRegistry.Instance;

    // ---------- 泛型基类 ----------

    [Fact]
    public void OpCode_ReflectsPayloadTypeAttribute()
    {
        var handler = new RecordingPushHandler();

        Assert.Equal((uint)TestOpCodes.ServerPush, ((IServerPushHandler)handler).OpCode);
    }

    [Fact]
    public void OpCode_PayloadTypeWithoutAttribute_Throws()
    {
        var handler = new UnattributedPayloadHandler();

        var ex = Assert.Throws<InvalidOperationException>(() => ((IServerPushHandler)handler).OpCode);

        Assert.Contains(nameof(ProtocolOpCodeAttribute), ex.Message);
    }

    [Fact]
    public void Handle_EmptyPayload_PushesDefaultWithoutDeserializing()
    {
        // 反序列化器故意设置为抛错，若空载荷未短路就会走到解码并报错。
        var stub = new RecordingWireSerializer { DeserializeShouldThrow = true };
        WireSerializer.Current = stub;
        var handler = new RecordingPushHandler();

        ((IServerPushHandler)handler).Handle(Array.Empty<byte>());

        Assert.True(handler.LastPush.HasValue);
        Assert.Equal(0L, handler.LastPush.Value.Frame);
        Assert.Null(handler.LastError);
    }

    [Fact]
    public void Handle_NullPayload_PushesDefault()
    {
        var stub = new RecordingWireSerializer { DeserializeShouldThrow = true };
        WireSerializer.Current = stub;
        var handler = new RecordingPushHandler();

        ((IServerPushHandler)handler).Handle(null!);

        Assert.True(handler.LastPush.HasValue);
        Assert.Equal(0L, handler.LastPush.Value.Frame);
        Assert.Null(handler.LastError);
    }

    [Fact]
    public void Handle_EncodedPayload_DecodesAndForwards()
    {
        WireSerializer.Current = new JsonBackedWireSerializer();
        var push = new TestServerPush { Frame = 9876543210123L, Health = -17, Alive = true };
        var payload = _protocol.Encode(push);
        var handler = new RecordingPushHandler();

        ((IServerPushHandler)handler).Handle(payload);

        Assert.NotNull(handler.LastPush);
        Assert.Equal(push.Frame, handler.LastPush.Value.Frame);
        Assert.Equal(push.Health, handler.LastPush.Value.Health);
        Assert.Equal(push.Alive, handler.LastPush.Value.Alive);
        Assert.Null(handler.LastError);
    }

    [Fact]
    public void Handle_UndecodablePayload_ReportsErrorAndSwallowsException()
    {
        var stub = new RecordingWireSerializer { DeserializeShouldThrow = true };
        WireSerializer.Current = stub;
        var handler = new RecordingPushHandler();
        var payload = new byte[] { 1, 2, 3 };

        ((IServerPushHandler)handler).Handle(payload); // 不应抛出

        Assert.False(handler.LastPush.HasValue);
        Assert.NotNull(handler.LastError);
        Assert.IsType<InvalidOperationException>(handler.LastError);
        Assert.Same(payload, handler.LastErrorPayload);
    }

    // ---------- 非泛型基类 ----------

    [Fact]
    public void RawHandler_OpCode_ComesFromOverride()
    {
        var handler = new RecordingRawPushHandler();

        Assert.Equal((uint)TestOpCodes.AttributedRawPushHandler, ((IServerPushHandler)handler).OpCode);
    }

    [Fact]
    public void RawHandler_EmptyPayload_PushesEmptySpan()
    {
        var handler = new RecordingRawPushHandler();

        ((IServerPushHandler)handler).Handle(Array.Empty<byte>());

        Assert.True(handler.SawEmptySpan);
        Assert.Null(handler.LastPayload);
    }

    [Fact]
    public void RawHandler_Payload_ForwardedAsSpanExactly()
    {
        var handler = new RecordingRawPushHandler();
        var payload = new byte[] { 5, 4, 3, 2, 1 };

        ((IServerPushHandler)handler).Handle(payload);

        Assert.Equal(payload, handler.LastPayload);
    }

    [Fact]
    public void RawHandler_OnPushThrows_RoutedToErrorCallback()
    {
        var handler = new ThrowingRawPushHandler();

        ((IServerPushHandler)handler).Handle(new byte[] { 1 }); // 不应抛出

        Assert.NotNull(handler.LastError);
        Assert.IsType<InvalidOperationException>(handler.LastError);
    }
}
