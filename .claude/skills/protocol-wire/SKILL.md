---
name: protocol-wire
description: Author or change AbilityKit protocol catalogs, grouped Wire Schema v2 files, MemoryPack DTOs, protocol export manifests, decoder registration, or compatibility baselines for MOBA, Shooter, and Room.
---

# Protocol Wire workflow

Use this skill for every change to a protocol message, payload, opcode, serializer layout, generated DTO, or protocol export.

## Sources of truth

- Transport identity and policy: `Protocols/Catalogs/*.protocol.yaml`.
- Payload fields and serialization layout: `Protocols/WireSchemas/*.wire.yaml`.
- Wire Schema generation applies to catalog-backed `memorypack` transport payloads. A `custom-binary` codec, legacy decode-only layout, transient formatter buffer, or process-local persistence record stays with its specialized codec.
- Only grouped Wire Schema v2 is valid: `schemaVersion: 2`, `projectId`, stable `groupId`, one shared `namespace`, and `types[]`.
- Do not add schema v1 or one-file-per-type wire definitions.

## Group and dependency rules

- Group by business owner, namespace, and release cadence. Keep large cohesive domains together; split unrelated namespaces or owners.
- Project-local reusable types belong in a stable `common` group. Cross-project types need a real shared owner.
- Custom `type` fields join the generated dependency closure. Use `external: true` only when another compiled package already owns the exact value object or enum.
- Use `scalarType: uint8` for C# `byte` fields.
- In the Unity Protocol Workspace, edit a type inside its grouped document; use `Add Type` to append a new type to the selected group. The compiler preserves sibling types and rejects duplicate names. Do not split a group into per-type files.

## Compatibility rules

- Copy existing `MemoryPackOrder` values exactly to field `id` values.
- Sequential MemoryPack IDs are contiguous from zero. Do not reorder, remove, widen, or renumber fields casually.
- Keep DTO fields only in generated `*.MemoryPack.g.cs`. Handwritten partial types may contain constructors, validation, constants, conversions, and codecs, but no duplicate serialization fields or `[MemoryPackable]` attribute.
- Never hand-edit generated DTOs, catalog glue, codec registry, export manifest, or compatibility baseline content.

## Runtime boundary rules

- At a network receive boundary, use the bounded overload `ProtocolPayloadDecoderRegistry.Decode(catalogId, definition, payload, schemaVersion)` or install `ProtocolPacketBoundaryValidator` on `NetworkPacketRouter` before invoking business handlers.
- The legacy `Decode(catalogId, messageId, payload)` overload exists only for compatibility and local trusted callers. New transport, dispatch, capture, and diagnostics code must not use it because it cannot enforce catalog limits.
- Treat `ProtocolDecodeFailureKind` and `ProtocolPacketBoundaryFailureKind` as stable categories for metrics and policy. Do not branch on exception message text.
- `schemaVersion` in a Wire Schema document describes the YAML document format. Online contract negotiation uses each message's `minimumSchemaVersion`/`maximumSchemaVersion` and `ProtocolSchemaVersionNegotiator`; these are separate from catalog `revision`.
- Negotiate the catalog/message schema range during connection or session setup and pass the selected version to the receive boundary. A catalog registry helper does not perform transport handshake automatically.
- Use the generated `abilitykit.system` catalog and `catalog-advertisement.request/response` for a formal catalog handshake. Build the payload with `ProtocolCatalogAdvertisement.FromCatalogs` and use `ProtocolCatalogAdvertisementCodec`; do not invent a project-local JSON or binary advertisement.
- A physical connection may advertise several catalogs. Use `ProtocolCatalogRegistry.TryNegotiateAdvertisement` for the aggregate payload, or `ProtocolCatalogNegotiator.Negotiate` for one catalog, and persist each result's `SelectedSchemaVersions`. Unknown optional catalogs may coexist, but every shared catalog must be compatible.
- The system advertisement is a formal bounded `custom-binary` contract because it carries a dynamic catalog/message set. Do not migrate it to a generated business DTO or compare catalog `revision` as if it were a wire version. Treat negotiation failure kinds as stable policy/metric categories.
- Bind negotiation to a `ProtocolCatalogNegotiationSession` per physical connection. Reset it on every reconnect, apply the remote catalog only after the business handshake has authenticated it, and enable boundary `requireNegotiated` when packets must not be accepted before negotiation.
- When negotiation gates a connection that still needs login, explicitly list only the login/handshake message IDs in `bootstrapMessageIds`; never disable the gate globally just to let authentication through.

## Required workflow

```powershell
./tools/compile-protocol-catalogs.ps1
./tools/export-protocol-wire.ps1 -Projects shooter,moba

./tools/compile-protocol-catalogs.ps1 -Check
./tools/export-protocol-wire.ps1 -Projects shooter,moba -Check -Strict
dotnet test src/AbilityKit.Protocol.CatalogCompiler.Tests/AbilityKit.Protocol.CatalogCompiler.Tests.csproj
dotnet build src/AbilityKit.Protocol.Moba/AbilityKit.Protocol.Moba.csproj
dotnet build src/AbilityKit.Protocol.Shooter/AbilityKit.Protocol.Shooter.csproj
```

Run focused golden-byte and round-trip tests for every changed payload family. Refresh the compatibility baseline only when the reviewed schema is intentionally becoming the new baseline, then rerun its read-only check.
