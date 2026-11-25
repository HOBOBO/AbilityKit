# Protocol network API generation

The catalog and wire schema remain the protocol authority. The catalog compiler can additionally
emit a typed client and typed server adapter for a consuming project:

```powershell
./tools/compile-protocol-catalogs.ps1 `
  -ApiCatalog Protocols/Catalogs/my-game.protocol.yaml `
  -ApiOutput src/MyGame/Generated/NetworkApi `
  -ApiNamespace MyGame.Protocol.GeneratedApi
```

The output contains `ProtocolClients.g.cs` and `ProtocolHandlers.g.cs`. It should be compiled by
the project that owns the payload types, alongside references to `com.abilitykit.network.sdk` and
`com.abilitykit.network.host` (or their .NET project equivalents). The shared protocol package
only owns catalog metadata and payload contracts; it does not reference the SDK or Host packages.

Client composition is explicit about the response envelope:

```csharp
using var sdk = new NetworkSdkBuilder()
    .UseTransportFactory(CreateTransport)
    .UseRawResponseBody()
    .Build();

using var game = new SharedRoomProtocolClient(
    sdk,
    new MemoryPackProtocolCodec(),
    BuiltInProtocolCatalogs.CreateRegistry());

var response = await game.GuestLoginAsync(request, cancellationToken: cancellationToken);
using var subscription = game.SubscribeFrameSyncFramePush(HandleFrame);
```

On the server, implement the generated handler contract and register its adapter with the existing
host router:

```csharp
var handlers = new SharedRoomProtocolRequestHandler(
    new RoomHandlers(),
    codec,
    RawRequestResponseEnvelopeCodec.Instance);
var router = handlers.Register(new AsyncServerRequestRouter());
var host = new NetworkHost(listener, new NetworkHostOptions { AsyncRequestHandler = router });
```

Generated methods own opcode, message ids, request/response type matching, serialization calls and
response writes. Application code implements business handler methods and chooses the codec,
transport and response envelope. `TcpGatewayRequestResponseEnvelopeCodec` remains available for
legacy Gateway status frames; new protocols do not require it.

The compiler's normal `-Check` mode also checks API files when `-ApiOutput` and `-ApiCatalog` are
supplied, so generated outputs can be committed and guarded by CI. API output is optional because
generated clients and server adapters belong to the consuming project rather than the shared
protocol package. A runnable request/response/push example is in `samples/ProtocolNetworkApi`.
