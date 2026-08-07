# com.abilitykit.network.transport.inmemory

> AbilityKit 的**进程内 ITransport**（测试用）。一对 linked 传输，一端 `Send` → 另一端 `BytesReceived`，无真实 socket。用于快速 in-process 集成测试（客户端 `NetworkSdkClient` 全栈 + 一个 in-process 服务端），替代跑真实服务端的重 smoke。

- **版本**：0.1.0
- **命名空间**：`AbilityKit.Network.Transport.InMemory`
- **依赖**：`com.abilitykit.network.runtime` 0.1.0
- **类型**：1 个 —— `InMemoryTransport : ITransport`（linked pair）

## 用法

```csharp
// 一对互相 linked 的 in-memory 传输
var (clientTransport, serverTransport) = InMemoryTransport.CreateConnectedPair();

// 客户端：用 clientTransport 组装真实 NetworkSdkClient 栈
var clientSdk = new NetworkSdkBuilder()
    .UseTransportFactory(() => clientTransport)   // ConnectionManager 会调 Connect（no-op，已配对）
    .Build();
clientSdk.Open("inmemory", 0);

// 服务端：用 serverTransport 写 in-process 服务端（处理 room/battle 协议）
serverTransport.BytesReceived += bytes => { /* 解析请求、响应 */ };
serverTransport.Connect("inmemory", 0);
```

- `CreateConnectedPair()` 创建一对（A↔B linked）。
- `Connect` 触发 `Connected`（在 `ConnectionManager` 订阅后调用，时序与真实传输一致）。
- `Send` **同步**路由到对端 `BytesReceived`（即时投递，`Send` 返回前对端已收到）。
- `Close`/`Dispose` 触发 `Disconnected`。

## 相关
- 默认/可选传输 → `network.runtime`（TCP）、`network.transport.websocket`、`network.transport.litenet`
- 组装根 → `com.abilitykit.network.sdk`
- 接入清单 → `Docs/design/07-NetworkSynchronization/07-MultiplayerSdkIntegrationGuide.md`
