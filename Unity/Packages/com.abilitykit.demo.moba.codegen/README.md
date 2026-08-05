# AbilityKit Demo MOBA CodeGen

This package owns compile-time behavior that is specific to the MOBA demo runtime:

- Static manifests for config tables, plan actions, payload fields, events, target queries, projectiles, bootstrap stages, behavior-tree nodes, snapshots, battle routes, and input handlers.
- Config-table manifests include strongly typed DTO-table and runtime-entry-table factories plus changed-ID collectors. Runtime entries are still created through each existing `MO(DTO)` constructor, so custom field conversions remain owned by the MO implementation instead of being inferred by the generator.
- Config-table diagnostics (`AKSG1001` and `AKSG1002`) enforce non-empty paths/groups, concrete DTO/MO types, public integer DTO keys, public DTO-to-MO constructors, and unique paths/DTO/MO registrations.
- MOBA plan-action contract diagnostics (`AK2001` through `AK2006`).
- Payload-field diagnostics (`AKSG2001`) enforce supported partial accessor shapes, valid field catalogs and resolver signatures, accessible constant fields, and collision-free generated methods/ID members.
- Battle route and input-handler diagnostics (`AKSG9001` through `AKSG9005`). Invalid shapes, duplicate route identities, unsupported derived route attributes, and zero/unknown route identities are errors. A missing public parameterless input-handler constructor is a warning because DI construction remains supported while the `Activator` fallback is unavailable.
- Bootstrap-stage diagnostics (`AKSG6001` and `AKSG6002`) enforce generated-code accessibility, concrete non-generic stage shape, an accessible parameterless constructor, and unique statically resolvable stage names.
- Behavior-tree node diagnostics (`AKSG7001` and `AKSG7002`) enforce generated-manifest accessibility, non-generic node types, and unique short names within the MOBA behavior-tree namespace.
- Event-mapping diagnostics (`AKSG3001` and `AKSG3002`) enforce compile-time event mapping arguments and unique IDs within the exact or prefix mapping kind.
- Target-query factory diagnostics (`AKSG4001` and `AKSG4002`) enforce the factory interface, generated-code accessibility, concrete non-generic shape, an accessible parameterless constructor, and unique codes within each factory kind.
- Projectile-emitter diagnostics (`AKSG5001` and `AKSG5002`) enforce the launch-sequence interface, generated-code accessibility, concrete non-generic shape, an accessible parameterless constructor, and unambiguous emitter-type/priority pairs.
- Snapshot-emitter diagnostics (`AKSG8001`) enforce concrete, non-generic, generated-code-accessible runtime emitters that implement the snapshot interface and use a constant integer priority. External assemblies without the runtime manifest remain on the reflection/DI extension path.

Framework-wide generators remain in `com.abilitykit.codegen`. Framework-wide analyzers remain in `com.abilitykit.analyzer`.

Each MOBA generator/analyzer pair consumes a shared contract from `Contracts/`. Generators filter invalid declarations and produce deterministic source; analyzers exclusively own diagnostics. Ownership tests prevent diagnostics from moving back into generator implementations or pair-specific validation from bypassing the shared contract.

Build the Roslyn component with:

```powershell
dotnet build DotNet~/AbilityKit.Demo.Moba.CodeGen/AbilityKit.Demo.Moba.CodeGen.csproj
```

The build copies `AbilityKit.Demo.Moba.CodeGen.dll` to the package root. Unity imports that DLL through the `RoslynAnalyzer` label. Generated manifests and their strongly typed config factories are the default MOBA runtime path. An incremental reload builds and validates candidate DTO and MO tables for the entire change batch before replacing any existing table contents, so conversion or validation failures leave the current database state unchanged. Registries and config definitions without generated factories retain the existing reflection fallback for compatibility with external or legacy registrations.

Run the repository contract gate before merging compile-time changes:

```powershell
powershell -ExecutionPolicy Bypass -File tools\run_test_gate.ps1 -Gate moba-codegen
```
