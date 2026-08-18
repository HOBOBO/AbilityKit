using System.Reflection;
using AbilityKit.Protocol;
using AbilityKit.Protocol.Serialization;

namespace AbilityKit.Protocol.Tests;

/// <summary>
/// ProtocolRegistry 是进程级单例，注册表 + 序列化器都是可变状态。
/// 每个测试在构造函数里 Clear() 注册表；序列化器是单向可写的（无 getter / Clear 不重置），
/// 因此所有 Encode/Decode 测试都显式 SetSerializer 建立自己的基线。
/// </summary>
public sealed class ProtocolRegistryTests : IDisposable
{
    private readonly ProtocolRegistry _registry = ProtocolRegistry.Instance;

    public ProtocolRegistryTests()
    {
        _registry.Clear();
        WireSerializer.Current = null!;
    }

    public void Dispose()
    {
        _registry.Clear();
        WireSerializer.Current = null!;
    }

    // ---------- 注册与反查 ----------

    [Fact]
    public void RegisterType_WithOpCodeAttribute_MapsBothDirections()
    {
        _registry.RegisterType(typeof(TestClientRequest));

        Assert.Same(typeof(TestClientRequest), _registry.GetType(TestOpCodes.ClientRequest));
        Assert.Equal((uint?)TestOpCodes.ClientRequest, _registry.GetOpCode<TestClientRequest>());
        Assert.Contains(TestOpCodes.ClientRequest, _registry.GetAllOpCodes());
    }

    [Fact]
    public void RegisterType_RecordsDirectionForLookup()
    {
        _registry.RegisterType(typeof(TestServerPush));
        _registry.RegisterType(typeof(TestBiEvent));

        Assert.Equal(ProtocolDirection.ServerToClient, _registry.GetDirection(TestOpCodes.ServerPush));
        Assert.Equal(ProtocolDirection.Bidirectional, _registry.GetDirection(TestOpCodes.BiEvent));
    }

    [Fact]
    public void RegisterType_WithoutAttribute_IsIgnored()
    {
        _registry.RegisterType(typeof(TestPlainStruct));

        Assert.Null(_registry.GetOpCode<TestPlainStruct>());
        Assert.Empty(_registry.GetAllOpCodes());
    }

    [Fact]
    public void RegisterType_Null_IsIgnored()
    {
        _registry.RegisterType(null!);

        Assert.Empty(_registry.GetAllOpCodes());
    }

    [Fact]
    public void RegisterType_SameTypeTwice_IsIdempotent()
    {
        _registry.RegisterType(typeof(TestClientRequest));

        // 同一类型重复注册是 no-op（支持重复扫描），不应抛异常，且映射保持。
        _registry.RegisterType(typeof(TestClientRequest));

        Assert.Same(typeof(TestClientRequest), _registry.GetType(TestOpCodes.ClientRequest));
        Assert.Equal((uint?)TestOpCodes.ClientRequest, _registry.GetOpCode<TestClientRequest>());
    }

    [Fact]
    public void GetType_UnknownOpCode_ReturnsNull()
    {
        Assert.Null(_registry.GetType(88888u));
    }

    [Fact]
    public void GetOpCode_UnregisteredType_ReturnsNull()
    {
        Assert.Null(_registry.GetOpCode<TestPlainStruct>());
    }

    [Fact]
    public void GetDirection_UnknownOpCode_ReturnsNull()
    {
        Assert.Null(_registry.GetDirection(88888u));
    }

    // ---------- 程序集扫描 ----------

    [Fact]
    public void ScanAssembly_RegistersAllAttributedTypes_AndMarksScanned()
    {
        _registry.ScanAssembly(typeof(TestClientRequest).Assembly);

        Assert.True(_registry.IsScanned);
        Assert.Same(typeof(TestClientRequest), _registry.GetType(TestOpCodes.ClientRequest));
        Assert.Same(typeof(TestServerPush), _registry.GetType(TestOpCodes.ServerPush));
        Assert.Same(typeof(TestBiEvent), _registry.GetType(TestOpCodes.BiEvent));
        Assert.Equal((uint?)TestOpCodes.BiEvent, _registry.GetOpCode<TestBiEvent>());
    }

    [Fact]
    public void ScanAssembly_SecondScan_IsIdempotent()
    {
        var assembly = typeof(TestClientRequest).Assembly;

        _registry.ScanAssembly(assembly);
        _registry.ScanAssembly(assembly); // 重复扫描不再抛异常

        Assert.Same(typeof(TestClientRequest), _registry.GetType(TestOpCodes.ClientRequest));
        Assert.True(_registry.IsScanned);
    }

    [Fact]
    public void ScanAssembly_NullAssembly_IsIgnored_DoesNotMarkScanned()
    {
        _registry.ScanAssembly((Assembly)null!);

        Assert.False(_registry.IsScanned);
        Assert.Empty(_registry.GetAllOpCodes());
    }

    [Fact]
    public void ScanAssembly_EmptyParams_IsNoOp()
    {
        _registry.ScanAssembly();

        Assert.False(_registry.IsScanned);
    }

    [Fact]
    public void ScanAssembly_NullParamsArray_ThrowsNullReferenceException()
    {
        // 实际实现没有校验 params 数组，foreach 空数组直接抛 NRE。
        Assert.Throws<NullReferenceException>(() => _registry.ScanAssembly((Assembly[])null!));
    }

    [Fact]
    public void ScanAssembly_ProtocolOwnAssembly_RegistersNothing_ButMarksScanned()
    {
        _registry.ScanAssembly(typeof(ProtocolRegistry).Assembly);

        Assert.True(_registry.IsScanned);
        Assert.Empty(_registry.GetAllOpCodes());
    }

    // ---------- 序列化器注入 ----------

    [Fact]
    public void WireSerializer_Current_ReplacesPreviousSerializer()
    {
        var first = new RecordingWireSerializer { SerializeResult = new byte[] { 1 } };
        var second = new RecordingWireSerializer { SerializeResult = new byte[] { 2 } };

        WireSerializer.Current = first;
        WireSerializer.Current = second;

        var bytes = _registry.Encode<TestServerPush>(default);
        Assert.Same(second.SerializeResult, bytes);
    }

    // ---------- 编解码路由 ----------

    [Fact]
    public void Encode_Generic_RoutesToSerializer_AndReturnsItsBytes()
    {
        var stub = new RecordingWireSerializer();
        WireSerializer.Current = stub;
        var dto = new TestServerPush { Frame = 42L, Health = 7, Alive = true };

        var bytes = _registry.Encode(dto);

        Assert.Same(stub.SerializeResult, bytes);
        var recorded = Assert.IsType<TestServerPush>(stub.LastSerializedValue);
        Assert.Equal(42L, recorded.Frame);
    }

    [Fact]
    public void Encode_ObjectOverload_PassesSameBoxedInstance()
    {
        var stub = new RecordingWireSerializer();
        WireSerializer.Current = stub;
        object boxed = new TestServerPush { Frame = 1L };

        _registry.Encode(boxed);

        Assert.Same(boxed, stub.LastSerializedValue);
    }

    [Fact]
    public void Decode_NullPayload_ThrowsArgument()
    {
        var stub = new RecordingWireSerializer();
        WireSerializer.Current = stub;

        Assert.Throws<ArgumentException>(() => _registry.Decode<TestServerPush>(null!));
    }

    [Fact]
    public void Decode_EmptyPayload_ThrowsArgument()
    {
        var stub = new RecordingWireSerializer();
        WireSerializer.Current = stub;

        Assert.Throws<ArgumentException>(() => _registry.Decode<TestServerPush>(Array.Empty<byte>()));
    }

    [Fact]
    public void Decode_RoutesToSerializer_WithSameByteArrayReference()
    {
        var stub = new RecordingWireSerializer();
        WireSerializer.Current = stub;
        var payload = new byte[] { 1, 2, 3 };

        var result = _registry.Decode<TestServerPush>(payload);

        Assert.Same(payload, stub.LastDeserializedArray);
        Assert.Equal(0L, result.Frame); // stub 返回 default(T)
    }

    // ---------- DecodeByOpCode ----------

    [Fact]
    public void DecodeByOpCode_MismatchedRegisteredType_Throws()
    {
        var stub = new RecordingWireSerializer();
        WireSerializer.Current = stub;
        _registry.RegisterType(typeof(TestClientRequest));

        var ex = Assert.Throws<InvalidOperationException>(
            () => _registry.DecodeByOpCode<TestServerPush>(TestOpCodes.ClientRequest, new byte[] { 1 }));

        Assert.Contains(nameof(TestClientRequest), ex.Message);
        Assert.Contains(nameof(TestServerPush), ex.Message);
    }

    [Fact]
    public void DecodeByOpCode_MatchingRegisteredType_RoundTrips()
    {
        WireSerializer.Current = new JsonBackedWireSerializer();
        _registry.RegisterType(typeof(TestServerPush));
        var push = new TestServerPush { Frame = 555L, Health = -3, Alive = true };

        var bytes = _registry.Encode(push);
        var back = _registry.DecodeByOpCode<TestServerPush>(TestOpCodes.ServerPush, bytes);

        Assert.Equal(push.Frame, back.Frame);
        Assert.Equal(push.Health, back.Health);
        Assert.Equal(push.Alive, back.Alive);
    }

    [Fact]
    public void DecodeByOpCode_UnregisteredOpCode_Throws()
    {
        WireSerializer.Current = new JsonBackedWireSerializer();
        var push = new TestServerPush { Frame = 123L, Health = 4, Alive = true };

        var bytes = _registry.Encode(push);

        Assert.Throws<InvalidOperationException>(
            () => _registry.DecodeByOpCode<TestServerPush>(99999u, bytes));
    }

    // ---------- Clear ----------

    [Fact]
    public void Clear_RemovesRegistrations_AndResetsIsScanned()
    {
        _registry.RegisterType(typeof(TestClientRequest));
        _registry.ScanAssembly(typeof(ProtocolRegistry).Assembly);

        _registry.Clear();

        Assert.False(_registry.IsScanned);
        Assert.Null(_registry.GetType(TestOpCodes.ClientRequest));
        Assert.Empty(_registry.GetAllOpCodes());
    }

    [Fact]
    public void Clear_DoesNotAffectWireSerializer()
    {
        var stub = new RecordingWireSerializer { SerializeResult = new byte[] { 0x11 } };
        WireSerializer.Current = stub;

        _registry.Clear();

        // Clear 只清注册表映射，不影响全局 WireSerializer.Current。
        Assert.Same(stub.SerializeResult, _registry.Encode<TestServerPush>(default));
    }
}
