# com.abilitykit.network.transport.websocket

> AbilityKit 的**可选 WebSocket 客户端传输**（`ITransport` 实现），基于 .NET `ClientWebSocket`。它可让桌面、移动和 .NET 进程复用 SDK 上层协议；是否能端到端接入取决于服务端监听与目标平台运行时。

- **版本**：0.1.0
- **命名空间**：`AbilityKit.Network.Transport.WebSocket`
- **依赖**：`com.abilitykit.network.runtime` 0.1.0
- **类型**：1 个 —— `WebSocketTransport : ITransport`（`System.Net.WebSockets.ClientWebSocket`）

## 适用 / 不适用

- 桌面、移动和 .NET 服务端进程可使用 `ClientWebSocket`，仍需按目标平台实际构建与运行验证。
- 标准 WebSocket 可作为代理、网关或浏览器基础设施中的候选通道；本包不自动处理代理认证或证书策略。
- **不支持 Unity WebGL**：浏览器环境不能直接使用本包的 `ClientWebSocket` 实现，WebGL 需要 JavaScript WebSocket bridge 的平台特化 transport。

## 用法

`NetworkSdkBuilder` 接受任意 `Func<ITransport>`，注入 WebSocket 即可，上层（`ConnectionManager` / `RoomGatewaySessionFlow` / `NetworkTransport`）无改动：

```csharp
var sdk = new NetworkSdkBuilder()
    .UseTransportFactory(() => new WebSocketTransport(path: "/gateway"))  // 默认 path "/"
    .ConfigureConnection(o => { /* heartbeat / reconnect / FrameCodec */ })
    .Build();
sdk.Open(host, port);
```

- WebSocket 是消息边界协议：完整收到一条 binary message 后触发一次 `BytesReceived`。消息内容仍是 `ConnectionManager` 的成帧字节，length prefix 不会被移除。
- ctor：`WebSocketTransport(path: "/", secure: false)`；`secure: true` 使用 `wss://`。
- `Send` 同步等待 `SendAsync` 完成，调用线程可能被网络背压阻塞，不应在高频主线程路径无预算地调用。
- 较大接收消息使用池化数组，`BytesReceived` 回调返回后数组会归还；需要异步处理或长期保存时必须在回调内复制。
- 接收循环运行在后台 task。默认 inline dispatcher 下，上层回调不保证位于 Unity 主线程。

## 服务端配套与验证状态

Orleans Gateway 已经注册并托管 `WebSocketTransportServer`，配置节点为 `AbilityKit:Gateway:WebSocket`。默认配置保持 `Enabled: false`，避免影响当前 TCP 生产与 smoke 主链；需要启用时显式配置端口与路径：

```json
{
  "AbilityKit": {
    "Gateway": {
      "WebSocket": {
        "Enabled": true,
        "Host": "0.0.0.0",
        "Port": 4001,
        "Path": "/gateway"
      }
    }
  }
}
```

`AbilityKit.Network.Transport.WebSocket.Tests` 使用本机 `HttpListener` 做 echo round-trip，验证基础握手和二进制收发；`AbilityKit.Orleans.Gateway.Tests` 额外覆盖 Gateway 服务端的配置路径、`NetworkFrameCodec` 收发、错误路径拒绝和 Stop/Restart 生命周期。它们仍不覆盖 TLS、反向代理、Unity 平台矩阵、WebGL、重连恢复或生产消费者。本包仍不标记为生产默认 transport。

## 相关

- 默认传输 → `com.abilitykit.network.runtime`（`TcpTransport`）
- 组装根 → `com.abilitykit.network.sdk`
- 接入清单 → `Docs/design/07-NetworkSynchronization/07-MultiplayerSdkIntegrationGuide.md`
