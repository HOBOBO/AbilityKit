# 统一网关连接抽象（v0.1.0 新增）

源文件：`Runtime/Gateway/IGatewayConnection.cs` + `Runtime/Gateway/GatewayConnection.cs`

## 用途

为 MOBA 和 Shooter 两个示例提供统一的网关连接抽象层，消除两个示例在请求/响应发送和推送接收上的模式差异。

## 接口

```csharp
public interface IGatewayConnection
{
    IConnection RawConnection { get; }          // 底层原始连接
    bool IsConnected { get; }                   // 连接是否活跃

    Task<byte[]> SendRequestAsync(uint opCode, byte[] payload, CancellationToken ct = default); // 请求+响应
    Task SendPushAsync(uint opCode, byte[] payload, CancellationToken ct = default);             // 单向推送

    void RegisterPushHandler(uint opCode, Action<byte[]> handler);    // 注册推送处理器
    void UnregisterPushHandler(uint opCode, Action<byte[]> handler);  // 取消注册
}
```

## 默认实现

`GatewayConnection` 包装 `IConnection` + seq 匹配：
- 请求：`IConnection.Send(opCode, payload, flags=Request, seq)` → `PacketReceived` 事件匹配 seq → `TaskCompletionSource`
- 推送：`IConnection.Send(opCode, payload, flags=ServerPush)` 或 `ServerPushReceived` 事件分发到已注册 handler

## 设计决策

1. **薄抽象**：不引入新的网络层次，直接包装 `IConnection`
2. **seq 匹配内置**：不需要外部 `RequestClient`，`GatewayConnection` 自行管理请求/响应 seq 配对
3. **推送分发容错**：handler 异常被捕获记录，不影响其他 handler
4. **线程安全**：`_pendingRequests` 和 `_pushHandlers` 均加锁保护
