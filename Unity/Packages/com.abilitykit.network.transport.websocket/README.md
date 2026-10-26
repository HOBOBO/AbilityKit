# com.abilitykit.network.transport.websocket

Optional `ITransport` implementation for AbilityKit using WebSocket binary messages.

`WebSocketTransport` accepts a path and optional TLS flag:

```csharp
var transport = new WebSocketTransport(path: "/network", secure: true);
var connection = new ConnectionManager(() => transport);
connection.Open("example.com", 443);
```

Desktop, mobile, and .NET server targets use `ClientWebSocket`. Unity WebGL uses the
included `Plugins/WebGL/AbilityKitWebSocket.jslib` bridge and the browser's native
`WebSocket`. The bridge is selected automatically under `UNITY_WEBGL && !UNITY_EDITOR`.
The server must accept binary messages and expose the same AbilityKit frame bytes; the
WebSocket message boundary is not used as the protocol frame boundary.

The bridge copies each browser message before invoking `BytesReceived`. Desktop and
server receives may use pooled buffers, so consumers that retain a callback payload
must copy it. The package does not provide TLS certificate policy, proxy handling, or
reconnect policy; those belong to the host and connection manager.

The transport package is optional. A project can select TCP, WebSocket, or KCP through
the same `ITransport` factory without changing protocol handlers or session code.
