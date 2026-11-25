# com.abilitykit.network.transport.kcp

采用 KCP 语义、实现 `ITransport` 的可选可靠 UDP 传输。
它承载与 TCP 和 WebSocket 相同的 AbilityKit 长度前缀帧字节，
使协议处理器和会话代码保持传输无关。

```csharp
var transport = new KcpTransport(new KcpTransportOptions
{
    ConversationId = 1,
    Mtu = 1200
});
var connection = new ConnectionManager(() => transport);
connection.Open("127.0.0.1", 4002);
```

对端必须使用相同的 `ConversationId`。KCP 提供可靠有序的字节消息传输；
身份认证、匹配、重连和玩法恢复语义由协议与会话层定义。

实现派生自本仓库 ET 示例已引入的 ET KCP 实现。再次分发受其源码许可证约束；
向仓库外发布此可选包前，应核对该许可证。
