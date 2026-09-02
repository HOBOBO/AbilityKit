# com.abilitykit.protocol.editor

Unity editor tooling for the repository-owned protocol YAML workspace.

Open `Tools/AbilityKit/Framework/Protocol/Protocol Workspace` to edit and validate Catalog and grouped Wire Schema documents, inspect project/message/type coverage, and export one project's generated MemoryPack DTOs and Catalog source.

New schemas default to version-tolerant classes with properties. Existing positional MemoryPack contracts can select `sequential`, `struct`, and field members; sequential field IDs must stay contiguous from zero. Project export follows custom-type references and includes the complete owned schema dependency closure.

YAML remains the source of truth. The editor invokes `AbilityKit.Protocol.CatalogCompiler` for parsing, validation, canonical writes, and export, so CLI, CI, and Unity share one implementation.

Wire Schema v2 is edited as a group document. Select any type from a group and use **Add Type** to stage another type in the same YAML file; saving appends it while preserving the group's other types and document ownership. The `+` button creates a new group document. Existing types can be renamed, and the compiler rejects duplicate names or ownership changes. The inspector shows effective per-type generation settings; document-level defaults and ownership metadata remain source-controlled YAML so changing one type cannot silently rewrite its siblings.

## Legacy ScriptableObject track — FROZEN (superseded, 2026-08)

The old dual-entry track — `ProtocolDefinition` ScriptableObject assets plus `ProtocolCodeGenerator` — is **frozen and removed as an authoring/generation path**. The YAML Protocol Workspace is the only official entry.

- `[CreateAssetMenu]` was removed from `ProtocolDefinition`; the schema type is marked `[Obsolete]` and kept **read-only** so existing `.asset` files still deserialize.
- The legacy generator (MemoryPack DTO / `OpCodes.g.cs` / snapshot-routing glue / codec backend stubs) and the SnapshotRouting importer were **deleted**. The package no longer emits any C# code outside the official compiler export.
- One clearly marked one-time migration entry remains: `Tools/AbilityKit/Framework/Protocol/Migrate Legacy ProtocolDefinition (one-time)`. It only *reads* a legacy `ProtocolDefinition` asset and writes it into a YAML catalog through the same `AbilityKit.Protocol.CatalogCompiler` validation/write path used by the workspace. After migrating, delete the legacy asset.
