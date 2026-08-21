# Protocol Catalog and Traffic Observability

Status: implemented baseline, 2026-08-21

## 1. Scope and decisions

This subsystem standardizes protocol governance and packet observation across AbilityKit projects without changing existing wire bytes.

- Business payload fields remain defined by the project's wire IDL and generated DTOs. Existing MOBA, Shooter, and Room MemoryPack types remain valid.
- YAML is the governance catalog for transport identity and operational policy. It is not a second DTO definition language.
- Room and Battle use separate physical connections by default. Their failure domains, reconnect policy, lifetime, and traffic profile are different.
- The reusable client unit is `NetworkSdkClient`, which owns one `IConnection` and one request client. Raw `IConnection` instances are not pooled or shared across unrelated roles.
- Packet observation is middleware-based and independent of TCP, WebSocket, KCP, or a future transport.

The target source flow is:

```text
payload IDL / generated DTOs        *.protocol.yaml
             |                            |
             +-------- build ------------+
                         |
              protocol-manifest.json
              BuiltInProtocolCatalogs.g.cs
                         |
        runtime lookup / decoder registration / visualization
```

## 2. Repository ownership

| Path | Ownership | Rule |
|---|---|---|
| `Protocols/Catalogs/*.protocol.yaml` | source | Reviewed and edited by project teams |
| `Protocols/protocol-catalog.schema.json` | source | IDE schema and canonical field shape |
| `Protocols/Generated/protocol-manifest.json` | generated | Never hand edit |
| `Runtime/Generated/BuiltInProtocolCatalogs.g.cs` | generated | Never hand edit |
| `Protocol/Catalog/*` | shared runtime | Stable cross-project contracts |
| `tools/AbilityKit.Protocol.CatalogCompiler` | build tool | Strict deterministic compiler |

Catalog files may be split by project and domain. Use globally unique IDs in the form `<organization>.<project>.<domain>`, for example `abilitykit.moba.battle`. Shared domains may use `abilitykit.room` with a shared `projectId`.

## 3. Catalog contract

Minimal example:

```yaml
schemaVersion: 1
catalogId: studio.game.battle
projectId: studio.game
domain: battle
revision: 1
defaultCodec: protobuf
messages:
  - id: cast-skill.request
    opCode: 2100
    direction: c2s
    kind: request
    payloadType: Studio.Game.Protocol.CastSkillRequest
    response: cast-skill.response
    reliability: reliable
    minimumSchemaVersion: 1
    maximumSchemaVersion: 1
    maximumPayloadBytes: 8192
    captureSampleRate: 1.0
    sensitiveFields: [authToken]
```

Message transport identity is the composite key:

```text
(catalogId, opCode, direction, kind)
```

This allows request and response messages to share an opcode while remaining unambiguous. Message IDs are unique within a catalog. Catalog IDs are unique across all loaded projects.

The compiler rejects unknown YAML members and validates reserved opcode zero, direction/kind compatibility, schema ranges, maximum payload size, sample rate, sensitive-field uniqueness, and request/response links.

## 4. Build and CI

Regenerate after editing a catalog:

```powershell
./tools/compile-protocol-catalogs.ps1
```

CI must use check mode and fail when committed outputs are stale:

```powershell
./tools/compile-protocol-catalogs.ps1 -Check
dotnet test src/AbilityKit.Protocol.Tests/AbilityKit.Protocol.Tests.csproj
```

Generation is deterministic: sources are recursively discovered and ordinally sorted; JSON and C# output ordering follows source order. A catalog change and both generated files belong in the same change set.

## 5. Runtime traffic capture

`ConnectionManager` installs `NetworkTrafficProbeMiddleware` before built-in protocol middleware. Every event contains:

- stable logical `ConnectionId`;
- physical-session `Generation`, incremented after reconnect;
- connection `Role`, `CatalogId`, endpoint, and transport name;
- UTC timestamp, direction, opcode, sequence, flags, and payload length;
- an optional bounded payload preview.

Capture defaults to header-only. Payload bytes are copied only when `MaximumPayloadPreviewBytes` is greater than zero. Observer and error-handler exceptions are contained and never stop packet forwarding.

`NetworkTrafficRingBuffer` is a thread-safe bounded collector. When full, it evicts the oldest event and increments `DroppedCount`. It is suitable as the data source for an editor window, local diagnostics page, or exporter bridge.

`NetworkTrafficInspector` is the shared join layer for those consumers. It maps inbound/outbound
traffic to Catalog direction, uses packet flags when they identify request/response/push, and
returns explicit unknown/ambiguous rows when the transport identity is insufficient. It only
invokes a registered decoder when the preview contains the complete payload; truncated previews
remain inspectable as metadata but are never passed to a DTO decoder.

Observers execute on the pipeline's current IO path and must return quickly. Expensive decode, persistence, and remote export should enqueue into a bounded worker-owned buffer rather than block packet handling.

SDK composition example:

```csharp
var traffic = new NetworkTrafficRingBuffer(4096);
var battleClient = new NetworkSdkBuilder()
    .UseTransportFactory(CreateBattleTransport)
    .ObserveTraffic(traffic, options =>
    {
        options.Role = "battle";
        options.CatalogId = "abilitykit.moba.battle";
        options.MaximumPayloadPreviewBytes = 256;
    })
    .Build();
```

The same collector can observe several Room and Battle clients; connection metadata keeps streams separate. `ConfigureTrafficCapture` provides a per-generation observer factory for exporters that need session-scoped state.

`NetworkTrafficMonitor` is the SDK-level multi-project source used by the Unity editor window. Its
default instance owns a bounded 8192-event ring buffer and the generated built-in Catalog registry;
projects may create a private monitor with a smaller capacity for tests or headless tooling. The
monitor never creates connections itself, so it remains an observer and visualization boundary,
not a second connection pool.

An SDK built from an external `IConnection` cannot install middleware safely and rejects `ObserveTraffic`. The owner of that connection must install a probe in its own pipeline.

## 6. Client hub and leases

`NetworkSdkClientHub` is the reusable-client boundary for multi-project hosts. It owns `NetworkSdkClient` instances, not raw `IConnection` objects, so each client keeps exactly one request tracker and one connection lifecycle.

```csharp
using var hub = new NetworkSdkClientHub();
var roomKey = new NetworkSdkClientKey("abilitykit.moba", "room", "primary");
var battleKey = new NetworkSdkClientKey("abilitykit.moba", "battle", "primary");

using var roomLease = hub.Acquire(roomKey, roomBuilder);
using var battleLease = hub.Acquire(battleKey, battleBuilder);
```

The same key returns the same client and increments its lease count. Different project, role, or instance IDs never share a client. Releasing the last lease does not close the connection: the Hub remains the owner and keeps the client cached for the next feature/session. `Remove` and `Dispose` are the explicit shutdown boundaries; removal while a lease is active is rejected.

This is deliberately different from socket pooling. A Room control connection and a Battle data-plane connection have different protocol catalogs, heartbeat/reconnect policy, QoS, and failure domains. The Hub centralizes ownership and reuse without collapsing those boundaries.

## 7. Visualization and security boundary

Visualization should join `NetworkTrafficEvent.CatalogId` and packet identity with `ProtocolCatalogRegistry`, then dispatch payload bytes through `ProtocolPayloadDecoderRegistry`. Decoders are registered by protocol packages so the shared network runtime does not depend on project DTO assemblies.

`sensitiveFields` is governance metadata, not automatic byte-level redaction. Raw payload preview can contain credentials or personal data before decoding. Production profiles should remain header-only or apply a capture filter. A decoded visualization/export layer must redact catalog-declared fields before persistence or remote export.

The SDK `NetworkTrafficJsonExporter` implements that boundary for the first editor workflow:

- decoded values are projected into plain JSON-compatible values and recursively redact public
  fields/properties whose names match `sensitiveFields` (case-insensitive);
- raw payload previews are omitted by default because bytes cannot be field-redacted safely;
- enabling raw preview for a sensitive message requires an explicit controlled-workflow flag and
  the editor window shows a second confirmation;
- unknown, ambiguous, and truncated packets remain exportable as metadata with a decode error,
  rather than being silently guessed or partially decoded.

Open `Window/AbilityKit/Network Traffic Monitor` in a development/editor build. MOBA Room/Battle
and Shooter Room/Battle sample composition paths opt into the shared monitor only under
`UNITY_EDITOR || DEVELOPMENT_BUILD`; Release builds do not allocate payload previews. Injected
GameFramework `IConnection` instances remain opt-in at their owner boundary because the SDK cannot
insert middleware safely after construction.

### Decoder module composition

Each protocol package owns an explicit, dependency-local decoder module:

```csharp
var decoders = NetworkTrafficMonitor.Default.Decoders;
RoomProtocolDecoderModule.Register(decoders);
MobaProtocolDecoderModule.Register(decoders);
ShooterProtocolDecoderModule.Register(decoders);
```

Applications call only the modules for protocols they host, before enabling traffic capture. Module
registration uses `ProtocolPayloadDecoderRegistry.TryRegister`, so repeated bootstrap, Unity domain
reload, and multiple composition roots are safe. Registration is first-wins: a host may install a
custom decoder before a package module, and the package will not overwrite it. Static constructors
and assembly scanning are intentionally avoided because their order is not a reliable multi-project
composition contract.

Room, MOBA, and Shooter sample clients install these modules only on their editor/development
observability paths. Tests enumerate every message in the generated built-in Catalogs and require a
matching decoder registration, preventing a new YAML message from silently appearing as undecodable
in the traffic monitor.

Sampling policy is catalog metadata. The first runtime baseline exposes a capture filter; deterministic per-message sampling and redaction belong in the catalog-aware observer, outside the transport hot path.

## 8. Migration rules

1. Inventory opcodes and existing DTO types without changing serialization.
2. Add one catalog per project/domain and pair request/response entries explicitly.
3. Run generation and fix all validator errors before adopting the generated registry.
4. Replace ad hoc opcode metadata lookup with `ProtocolCatalogRegistry` at diagnostic/tooling boundaries first.
5. Register project-owned decoders for visualization. Do not move project DTO dependencies into the shared runtime.
6. Enable payload preview only in explicit development or controlled diagnostic profiles.

Changing a field layout remains an IDL migration. Changing opcode, direction, kind, codec, or compatibility range is a catalog contract change and must increment `revision`. Removing or reusing an opcode requires an explicit compatibility window; silent reuse in the same catalog is prohibited.

## 9. Next extensions

- Add `.proto` sources and generated C#/server DTOs for projects that need language-neutral field IDL. YAML continues to carry governance metadata.
- Add a catalog-aware observer for sampling, decode, redaction, metrics, and trace export.
- Generate or validate decoder-module registration stubs from Catalog payload metadata while keeping codec implementation inside protocol packages.
- Add a remote/CI exporter that accepts the same `NetworkTrafficJsonExporter` policy object and
  streams bounded batches instead of blocking the packet IO path.
