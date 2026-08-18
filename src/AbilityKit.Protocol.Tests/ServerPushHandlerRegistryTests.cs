using System.Reflection;
using AbilityKit.Protocol;

namespace AbilityKit.Protocol.Tests;

public sealed class ServerPushHandlerRegistryTests : IDisposable
{
    private readonly ServerPushHandlerRegistry _registry = ServerPushHandlerRegistry.Instance;

    public ServerPushHandlerRegistryTests() => _registry.Clear();

    public void Dispose() => _registry.Clear();

    // ---------- 手动注册 / 反查 ----------

    [Fact]
    public void Register_AndGetHandler_ReturnsSameInstance()
    {
        var handler = new StubPushHandler(TestOpCodes.StubDispatchHandler);

        _registry.Register(TestOpCodes.StubDispatchHandler, handler);

        Assert.Same(handler, _registry.GetHandler(TestOpCodes.StubDispatchHandler));
    }

    [Fact]
    public void Register_NullHandler_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _registry.Register(1u, null!));
    }

    [Fact]
    public void Register_SameOpCodeTwice_Throws()
    {
        _registry.Register(7u, new StubPushHandler(7u));

        var ex = Assert.Throws<InvalidOperationException>(() => _registry.Register(7u, new StubPushHandler(7u)));

        Assert.Contains("already registered", ex.Message);
    }

    [Fact]
    public void TryGetHandler_Missing_ReturnsFalseAndNull()
    {
        var found = _registry.TryGetHandler(12345u, out var handler);

        Assert.False(found);
        Assert.Null(handler);
    }

    [Fact]
    public void GetAllHandlers_AndOpCodes_ReflectRegistrations()
    {
        _registry.Register(1u, new StubPushHandler(1u));
        _registry.Register(2u, new StubPushHandler(2u));
        _registry.Register(3u, new StubPushHandler(3u));

        Assert.Equal(3, _registry.GetAllHandlers().Count);
        Assert.Equal(3, _registry.GetAllOpCodes().Count);
        Assert.Contains(2u, _registry.GetAllOpCodes());
    }

    [Fact]
    public void Unregister_Existing_ReturnsTrue_AndRemoves()
    {
        _registry.Register(5u, new StubPushHandler(5u));

        var removed = _registry.Unregister(5u);

        Assert.True(removed);
        Assert.Null(_registry.GetHandler(5u));
    }

    [Fact]
    public void Unregister_Missing_ReturnsFalse()
    {
        Assert.False(_registry.Unregister(404u));
    }

    [Fact]
    public void Clear_RemovesAllHandlers_AndResetsIsScanned()
    {
        _registry.Register(1u, new StubPushHandler(1u));
        _registry.ScanAndRegister(typeof(ProtocolRegistry).Assembly);

        _registry.Clear();

        Assert.False(_registry.IsScanned);
        Assert.Empty(_registry.GetAllHandlers());
    }

    [Fact]
    public void RegisterGeneric_UsesHandlerDeclaredOpCode()
    {
        _registry.Register<PlainInterfaceHandler>();

        Assert.IsType<PlainInterfaceHandler>(_registry.GetHandler(TestOpCodes.PlainInterfaceHandler));
    }

    // ---------- 类型级注册 ----------

    [Fact]
    public void RegisterHandlerType_Null_IsIgnored()
    {
        _registry.RegisterHandlerType(null!);

        Assert.Empty(_registry.GetAllHandlers());
    }

    [Fact]
    public void RegisterHandlerType_MissingAttribute_IsSkipped()
    {
        _registry.RegisterHandlerType(typeof(PlainInterfaceHandler));

        Assert.Null(_registry.GetHandler(TestOpCodes.PlainInterfaceHandler));
    }

    [Fact]
    public void RegisterHandlerType_NotAssignablyHandler_IsSkipped()
    {
        _registry.RegisterHandlerType(typeof(MisattributedNonHandler));

        Assert.Null(_registry.GetHandler(TestOpCodes.MisattributedNonHandler));
    }

    [Fact]
    public void RegisterHandlerType_Abstract_IsSkipped()
    {
        _registry.RegisterHandlerType(typeof(AbstractPushHandler));

        Assert.Null(_registry.GetHandler(TestOpCodes.AbstractHandler));
    }

    [Fact]
    public void RegisterHandlerType_AttributedHandler_IsActivatedAndRegistered()
    {
        _registry.RegisterHandlerType(typeof(RecordingPushHandler));

        Assert.IsType<RecordingPushHandler>(_registry.GetHandler(TestOpCodes.AttributedPushHandler));
    }

    [Fact]
    public void RegisterHandlerType_ConstructorThrows_WrapsInInvalidOperationException()
    {
        ConditionalThrowingCtorHandler.ThrowInConstructor = true;
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => _registry.RegisterHandlerType(typeof(ConditionalThrowingCtorHandler)));

            Assert.Contains("Failed to create instance", ex.Message);
            // Activator.CreateInstance 会把构造函数异常包装进 TargetInvocationException，
            // 再由 RegisterHandlerType 包装进 InvalidOperationException。
            Assert.IsType<TargetInvocationException>(ex.InnerException);
            Assert.IsType<ArgumentException>(ex.InnerException.InnerException);
        }
        finally
        {
            ConditionalThrowingCtorHandler.ThrowInConstructor = false;
        }
    }

    // ---------- 程序集扫描 ----------

    [Fact]
    public void ScanAndRegister_NullAssembly_IsIgnored_NotMarkedScanned()
    {
        _registry.ScanAndRegister((Assembly)null!);

        Assert.False(_registry.IsScanned);
    }

    [Fact]
    public void ScanAndRegister_RegistersAttributedHandlers_MarksScanned()
    {
        _registry.ScanAndRegister(typeof(RecordingPushHandler).Assembly);

        Assert.True(_registry.IsScanned);
        Assert.IsType<RecordingPushHandler>(_registry.GetHandler(TestOpCodes.AttributedPushHandler));
        Assert.IsType<RecordingRawPushHandler>(_registry.GetHandler(TestOpCodes.AttributedRawPushHandler));
        Assert.IsType<ConditionalThrowingCtorHandler>(_registry.GetHandler(TestOpCodes.ConditionalCtorHandler));

        // 带属性但不实现接口 / 抽象类应被跳过。
        Assert.Null(_registry.GetHandler(TestOpCodes.MisattributedNonHandler));
        Assert.Null(_registry.GetHandler(TestOpCodes.AbstractHandler));
    }

    [Fact]
    public void ScanAndRegister_SecondScan_ThrowsDuplicate()
    {
        var assembly = typeof(RecordingPushHandler).Assembly;

        _registry.ScanAndRegister(assembly);

        Assert.Throws<InvalidOperationException>(() => _registry.ScanAndRegister(assembly));
    }

    // ---------- 分发 ----------

    [Fact]
    public void Handle_DispatchesPayloadToHandler()
    {
        var handler = new StubPushHandler(TestOpCodes.StubDispatchHandler);
        _registry.Register(TestOpCodes.StubDispatchHandler, handler);
        var payload = new byte[] { 4, 5, 6 };

        _registry.Handle(TestOpCodes.StubDispatchHandler, payload);

        Assert.Equal(1, handler.HandleCount);
        Assert.Same(payload, handler.LastPayload);
    }

    [Fact]
    public void Handle_UnknownOpCode_CompletesWithoutThrowing()
    {
        var ex = Record.Exception(() => _registry.Handle(99999u, new byte[] { 1 }));

        Assert.Null(ex);
    }

    // ---------- ServerPushHandlerAttribute ----------

    [Fact]
    public void ServerPushHandlerAttribute_StoresOpCode()
    {
        var attribute = new ServerPushHandlerAttribute(4242u);

        Assert.Equal(4242u, attribute.OpCode);
    }

    [Fact]
    public void ServerPushHandlerAttribute_Usage_ClassOnlySingle()
    {
        var usage = typeof(ServerPushHandlerAttribute).GetCustomAttribute<AttributeUsageAttribute>();

        Assert.NotNull(usage);
        Assert.Equal(AttributeTargets.Class, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
    }
}
