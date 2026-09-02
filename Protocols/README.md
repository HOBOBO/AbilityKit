# Protocol source format

Protocol catalogs and wire schemas have separate responsibilities:

- `Catalogs/*.protocol.yaml` assigns transport identities and runtime policy.
- `WireSchemas/*.wire.yaml` defines payload fields and serialization compatibility.

Grouped Wire Schema generation covers catalog-backed `memorypack` transport payloads. Types used
only by `custom-binary` codecs, legacy decoding, transient buffers, or process-local persistence
remain owned by those specialized implementations and are not counted as missing Wire Schemas.

`Catalogs/system.protocol.yaml` is the formal bootstrap contract for exchanging one or more
catalog advertisements. Its dynamic payload is deliberately owned by the bounded
`ProtocolCatalogAdvertisementCodec` custom-binary codec; applications should not duplicate this
contract in a business catalog or model it as a Wire Schema DTO.

## Grouped wire schema

The canonical wire format groups related types that share a project, stable business-domain
`groupId`, and C# namespace. Document defaults apply to every type unless that type overrides
them. Each type still has its own field IDs, reserved IDs, compatibility identity, and generated
source file.

```yaml
# yaml-language-server: $schema=../wire-schema.schema.json
schemaVersion: 2
projectId: abilitykit.shooter
groupId: battle
namespace: AbilityKit.Protocol.Shooter
defaults:
  memoryPackMode: sequential
  declaration: struct
  memberStyle: field
types:
  - name: ShooterPlayerCommand
    fields:
      - id: 0
        name: playerId
        scalarType: int32
        required: true
      - id: 1
        name: moveX
        scalarType: float
        required: true

  - name: ShooterInputPayload
    declaration: class
    memberStyle: property
    fields:
      - id: 0
        name: commands
        type: AbilityKit.Protocol.Shooter.ShooterPlayerCommand
        array: true
        required: true
```

When `defaults` is omitted, the defaults are `version-tolerant`, `class`, and `property`.
Overrides are allowed on each item in `types`. A grouped document must contain at least one type,
and type names must be unique within the document.

`groupId` is unique within a project and uses lower-case letters, digits, dots, and hyphens. Use a
cohesive business domain such as `battle`, `state-sync`, or `room.auth`. Project-local shared
types belong to a formal `common` group; genuinely cross-project types belong to a dedicated
shared project. Split a group when types have different ownership or release cadence. Do not
combine protocol catalog messages and wire types in the same document.

Use `scalarType: uint8` for C# `byte` fields. A custom `type` normally joins the generated
dependency closure. Set `external: true` only when that exact type is already owned and compiled
by another package (for example a shared value object or enum); the field keeps its declared C#
type, but the current project does not generate another definition for it. Do not use `external`
to hide a missing project-local wire type.

The Unity Protocol Workspace expands grouped documents into individual types. Saving a type
updates its entry while preserving sibling types; `Add Type` stages a new type for the selected
group and appends it on save. The inspector shows effective type settings; document-level
defaults and `projectId`, `groupId`, and `namespace` are edited directly in YAML. Export manifests record every generated type under its
owning group.

After changing a protocol source, regenerate and check the committed outputs:

```powershell
./tools/compile-protocol-catalogs.ps1
./tools/export-protocol-wire.ps1 -Projects shooter,moba

./tools/compile-protocol-catalogs.ps1 -Check
./tools/export-protocol-wire.ps1 -Projects shooter,moba -Check -Strict
```
