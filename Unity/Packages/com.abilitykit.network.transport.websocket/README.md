# com.abilitykit.network.transport.websocket

基于 WebSocket 二进制消息的可选 AbilityKit `ITransport` 实现。

`WebSocketTransport` 接受路径和可选的 TLS 标志：

```csharp
var transport = new WebSocketTransport(path: "/network", secure: true);
var connection = new ConnectionManager(() => transport);
connection.Open("example.com", 443);
```

桌面、移动端和 .NET 服务器使用 `ClientWebSocket`。Unity WebGL 使用随包提供的
`Plugins/WebGL/AbilityKitWebSocket.jslib` 桥接和浏览器原生 `WebSocket`；
在 `UNITY_WEBGL && !UNITY_EDITOR` 下自动选择该桥接。
服务器必须接受二进制消息并提供相同的 AbilityKit 帧字节；WebSocket 消息边界不作为协议帧边界。

桥接在调用 `BytesReceived` 前复制每条浏览器消息。桌面和服务器接收可能使用池化缓冲区，
因此消费方若要保留回调载荷，必须先复制。TLS 证书策略、代理处理和重连策略由宿主及连接管理器负责，
本包不提供这些策略。

此传输包为可选项。项目可通过同一个 `ITransport` 工厂选择 TCP、WebSocket 或 KCP，
无需改变协议处理器或会话代码。
