# com.abilitykit.network.transport.websocket

> AbilityKit 的**可选 WebSocket 传输**（`ITransport` 实现）。作为 `TcpTransport` 的替换，让 Web/移动/桌面客户端经 WebSocket 接入。纯加法包 —— 不用 WebSocket 的项目不依赖它。

- **版本**：0.1.0
- **命名空间**：`AbilityKit.Network.Transport.WebSocket`
- **依赖**：`com.abilitykit.network.runtime` 0.1.0
- **类型**：1 个 —— `WebSocketTransport : ITransport`（`System.Net.WebSockets.ClientWebSocket`）

## 适用 / 不适用

- ✅ 桌面 / 移动 / 服务端（.NET `ClientWebSocket`）。
- ✅ 需要走标准 WebSocket 的团队（浏览器中转、企业代理穿透等）。
- ❌ **Unity WebGL**：浏览器不支持 `ClientWebSocket`，WebGL 需平台特化版本（走 JS WS 桥）。本包不含 WebGL 版。

## 用法

`NetworkSdkBuilder` 接受任意 `Func<ITransport>`，注入 WebSocket 即可，上层（`ConnectionManager` / `RoomGatewaySessionFlow` / `NetworkTransport`）无改动：

```csharp
var sdk = new NetworkSdkBuilder()
    .UseTransportFactory(() => new WebSocketTransport(path: "/gateway"))  // 默认 path "/"
    .ConfigureConnection(o => { /* heartbeat / reconnect / FrameCodec */ })
    .Build();
sdk.Open(host, port);
```

- WebSocket 是**消息边界**协议：每条二进制消息 = 一次 `BytesReceived`。载荷即 `ConnectionManager` 的成帧字节（`LengthPrefixedFrameCodec` 的 length-prefix 保留），所以对上层成帧透明。
- ctor：`WebSocketTransport(path: "/", secure: false)` —— `secure:true` 用 `wss://`。

## 服务端配套（重要）

本包是**客户端**传输。要端到端经 WebSocket，**服务端网关也需要 WebSocket 监听端**（当前 `AbilityKit.Orleans.Gateway` 的 `TcpTransportServer` 只讲 TCP）。客户端 WebSocket 连不到裸 TCP 服务端 —— 服务端 WebSocket 支持是独立后续工作。

## 相关

- 默认传输 → `com.abilitykit.network.runtime`（`TcpTransport`）
- 组装根 → `com.abilitykit.network.sdk`
- 接入清单 → `Docs/design/07-NetworkSynchronization/07-MultiplayerSdkIntegrationGuide.md`
