# com.abilitykit.network.transport.inmemory

> AbilityKit 的**进程内 ITransport**（测试用）。一对 linked 传输中，一端 `Send` 会同步触发另一端 `BytesReceived`，全程没有真实 socket。它适合快速验证客户端 SDK 与进程内协议处理器的装配，不替代真实服务端 smoke 或网络环境测试。

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
- `Connect` 触发 `Connected`，但不解析或验证 host/port。
- `Send` **同步**路由到对端 `BytesReceived`；不跨线程，`Send` 返回前对端已处理回调。
- `Close`/`Dispose` 触发本端 `Disconnected`。

## 测试边界

`AbilityKit.Network.Transport.InMemory.Tests` 有 linked-pair 同步回环测试。仓库另有一条 SDK + InMemory 集成测试，但它没有完成真实 request-response 断言，因此当前证据只支持“transport 回环和基础装配可工作”。

本实现不模拟 socket 建连、带宽、时延、抖动、丢包、重复、乱序、半开连接、异步回调或真实断网。需要验证这些行为时，应使用对应真实 transport、服务端和可控网络环境。

## 相关
- 默认/可选传输 → `network.runtime`（TCP）、`network.transport.websocket`、`network.transport.litenet`
- 组装根 → `com.abilitykit.network.sdk`
- 接入清单 → `Docs/design/07-NetworkSynchronization/07-MultiplayerSdkIntegrationGuide.md`
