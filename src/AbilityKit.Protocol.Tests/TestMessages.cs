using AbilityKit.Protocol;

namespace AbilityKit.Protocol.Tests;

/// <summary>
/// 测试协议 OpCode 与 Handler OpCode 的集中定义。
/// 测试程序集内所有带 [ProtocolOpCode] 的类型必须唯一，才能支撑"扫描程序集并注册全部类型"的 happy path。
/// </summary>
internal static class TestOpCodes
{
    public const uint ClientRequest = 91001;
    public const uint ServerPush = 91002;
    public const uint BiEvent = 91003;

    public const uint AttributedPushHandler = 92901;
    public const uint AttributedRawPushHandler = 92902;
    public const uint ThrowingRawHandler = 92903;
    public const uint ConditionalCtorHandler = 92905;
    public const uint AbstractHandler = 92906;
    public const uint PlainInterfaceHandler = 92907;
    public const uint MisattributedNonHandler = 92999;

    public const uint StubDispatchHandler = 93001;
}

// ---------- 带 [ProtocolOpCode] 的协议 DTO ----------

[ProtocolOpCode(TestOpCodes.ClientRequest, ProtocolDirection.ClientToServer, "ClientRequest")]
public struct TestClientRequest
{
    public int Sequence;
    public string? Token;
}

[ProtocolOpCode(TestOpCodes.ServerPush, ProtocolDirection.ServerToClient)]
public struct TestServerPush
{
    public long Frame;
    public int Health;
    public bool Alive;
}

[ProtocolOpCode(TestOpCodes.BiEvent)]
public struct TestBiEvent
{
    public int Code;
}

/// <summary>不带属性，用于"无 OpCode"路径。</summary>
public struct TestPlainStruct
{
    public int Value;
}

// ---------- JSON 序列化探针 DTO（属性版） ----------

public sealed class SampleDto
{
    public int IntValue { get; set; }
    public long LongValue { get; set; }
    public bool BoolValue { get; set; }
    public string? StringValue { get; set; }
    public int[]? Numbers { get; set; }
    public SampleNested? Nested { get; set; }
}

public sealed class SampleNested
{
    public string? Name { get; set; }
    public int Count { get; set; }
}

public sealed class NullProbeDto
{
    public string? Name { get; set; }
    public int Age { get; set; }
}

public sealed class LongProbeDto
{
    public long LongValue { get; set; }
}

public sealed class DoubleProbeDto
{
    public double DoubleValue { get; set; }
}

public enum TestMode
{
    Off = 0,
    On = 1,
    Weird = 99,
}

public sealed class EnumProbeDto
{
    public TestMode Mode { get; set; }
}

public sealed class CollectionProbeDto
{
    public List<int>? Numbers { get; set; }
    public Dictionary<string, int>? Stats { get; set; }
}

// ---------- Server Push Handler ----------

public sealed class StubPushHandler : IServerPushHandler
{
    public StubPushHandler(uint opCode) => OpCode = opCode;

    public uint OpCode { get; }

    public int HandleCount { get; private set; }
    public byte[]? LastPayload { get; private set; }

    public void Handle(byte[] payload)
    {
        HandleCount++;
        LastPayload = payload;
    }
}

/// <summary>带 [ServerPushHandler] 的泛型处理器；被扫描程序集测试用于验证自动实例化。</summary>
[ServerPushHandler(TestOpCodes.AttributedPushHandler)]
public sealed class RecordingPushHandler : ServerPushHandlerBase<TestServerPush>
{
    public TestServerPush? LastPush { get; private set; }
    public Exception? LastError { get; private set; }
    public byte[]? LastErrorPayload { get; private set; }

    protected override void OnPush(TestServerPush payload) => LastPush = payload;

    protected override void OnDeserializeError(Exception ex, byte[] payload)
    {
        LastError = ex;
        LastErrorPayload = payload;
    }
}

/// <summary>payload 类型缺少 [ProtocolOpCode]，用于验证 OpCode 抛错路径。</summary>
public sealed class UnattributedPayloadHandler : ServerPushHandlerBase<TestPlainStruct>
{
    public TestPlainStruct? LastPush { get; private set; }

    protected override void OnPush(TestPlainStruct payload) => LastPush = payload;
}

[ServerPushHandler(TestOpCodes.AttributedRawPushHandler)]
public sealed class RecordingRawPushHandler : ServerPushHandlerBase
{
    public byte[]? LastPayload { get; private set; }
    public bool SawEmptySpan { get; private set; }
    public Exception? LastError { get; private set; }

    public override uint OpCode => TestOpCodes.AttributedRawPushHandler;

    protected override void OnPush(ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty)
        {
            SawEmptySpan = true;
            return;
        }

        LastPayload = payload.ToArray();
    }

    protected override void OnDeserializeError(Exception ex, byte[] payload) => LastError = ex;
}

/// <summary>OnPush 抛异常，用于验证错误回调吞掉异常、不外抛。</summary>
public sealed class ThrowingRawPushHandler : ServerPushHandlerBase
{
    public Exception? LastError { get; private set; }

    public override uint OpCode => TestOpCodes.ThrowingRawHandler;

    protected override void OnPush(ReadOnlySpan<byte> payload) => throw new InvalidOperationException("boom");

    protected override void OnDeserializeError(Exception ex, byte[] payload) => LastError = ex;
}

/// <summary>构造函数可控抛异常，用于验证 RegisterHandlerType 的包装逻辑。</summary>
[ServerPushHandler(TestOpCodes.ConditionalCtorHandler)]
public sealed class ConditionalThrowingCtorHandler : IServerPushHandler
{
    public static bool ThrowInConstructor;

    public ConditionalThrowingCtorHandler()
    {
        if (ThrowInConstructor) throw new ArgumentException("simulated ctor failure");
    }

    public uint OpCode => TestOpCodes.ConditionalCtorHandler;

    public void Handle(byte[] payload) { }
}

/// <summary>带属性但不实现 IServerPushHandler，扫描时应被跳过。</summary>
[ServerPushHandler(TestOpCodes.MisattributedNonHandler)]
public sealed class MisattributedNonHandler
{
}

/// <summary>抽象处理器，扫描时应被跳过。</summary>
[ServerPushHandler(TestOpCodes.AbstractHandler)]
public abstract class AbstractPushHandler : IServerPushHandler
{
    public abstract uint OpCode { get; }

    public abstract void Handle(byte[] payload);
}

/// <summary>实现接口但无属性，扫描时应被跳过。</summary>
public sealed class PlainInterfaceHandler : IServerPushHandler
{
    public uint OpCode => TestOpCodes.PlainInterfaceHandler;

    public void Handle(byte[] payload) { }
}
