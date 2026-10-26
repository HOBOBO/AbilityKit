# com.abilitykit.network.transport.kcp

Optional reliable UDP transport implementing `ITransport` with KCP semantics.
It carries the same AbilityKit length-prefixed frame bytes as TCP and WebSocket,
so protocol handlers and session code remain transport independent.

```csharp
var transport = new KcpTransport(new KcpTransportOptions
{
    ConversationId = 1,
    Mtu = 1200
});
var connection = new ConnectionManager(() => transport);
connection.Open("127.0.0.1", 4002);
```

The peer must use the same `ConversationId`. KCP is a reliable ordered byte-message
transport; it does not define authentication, matchmaking, reconnect, or gameplay
resume semantics. Use the protocol/session layer for those concerns.

The implementation is derived from the ET KCP implementation already vendored by the
ET demo in this repository. Its source license applies to redistribution; review that
license before publishing this optional package outside the repository.
