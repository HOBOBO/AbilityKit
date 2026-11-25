# com.abilitykit.network.host.kcp

`NetworkHost` 的可选 KCP 监听器。`KcpChannelListener` 持有一个 UDP 套接字，
为每个远端地址创建一个 `KcpServerChannel`。会话 ID 必须与客户端传输配置一致：

```csharp
var listener = new KcpChannelListener(new KcpChannelListenerOptions
{
    Address = IPAddress.Any,
    Port = 4002,
    ConversationId = 1
});
using var host = new NetworkHost(listener, new NetworkHostOptions
{
    RequestHandler = handlers
});
host.Start();
```

监听器在创建通道前验证 KCP 会话 ID，已接受的通道交由 `NetworkHost` 所有者管理。
停止时关闭监听器及全部通道。实现与可选客户端包使用相同的 ET 派生 KCP 核心；
再次分发受其源码许可证约束。
