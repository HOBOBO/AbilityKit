---
trigger: manual
---

# AbilityKit UPM / asmdef Architecture (Structure Spec)

## 0. Scope

This document defines the canonical directory/package layout, assembly boundaries, allowed dependency directions, naming conventions, and runtime DI/module registration rules for this repo after the UPM refactor.

It is intended to be used as a **single source of truth** for:

- new package creation
- adding/removing asmdef references
- deciding where code should live
- debugging “type not found” / DI resolution issues


## 1. Repo layout

- `Unity/Packages/com.abilitykit.*`
  - Primary code location (UPM packages)
  - Each package has `package.json` + `Runtime/` (+ optional `Editor/`, `Tests/`)
- `Unity/Assets/`
  - Avoid putting framework/runtime logic here going forward
  - Allowed mainly for project-level glue, scenes, resources, and legacy transitions


## 2. Package tiers (dependency direction)

### 2.1 Tier list (bottom -> top)

- **Tier 0: Core**
  - Examples: `AbilityKit.Core`
  - Responsibilities: logging, tags, shared utilities, low-level primitives

- **Tier 1: Data / Common Subsystems**
  - Examples: `AbilityKit.Attributes`, `AbilityKit.ActionSchema`
  - Responsibilities: attribute system, action/trigger DTOs, shared data definitions

- **Tier 2: World Infrastructure**
  - Examples: `AbilityKit.World.DI`, `AbilityKit.World.ECS`, `AbilityKit.World.FrameSync`, `AbilityKit.World.Snapshot`, `AbilityKit.World.Record`
  - Responsibilities: world container, DI, ECS world, frame/time, snapshot/record/rollback plumbing

- **Tier 3: Ability Runtime**
  - Examples: `AbilityKit.Ability.RuntimePkg` (and related)
  - Responsibilities: triggering, pipelines, effect system, world services interfaces

- **Tier 4: Game Logic Implementations (Demo / Moba Runtime)**
  - Examples: `com.abilitykit.demo.moba.runtime`
  - Responsibilities: Moba services/systems/modules, entitas adapters, gameplay rules

- **Tier 5: Game View / Presentation Runtime**
  - Examples: `com.abilitykit.demo.moba.view.runtime`
  - Responsibilities: view binder, snapshot-driven presentation, battle session, debug runtime UI

- **Tier 6: Editor Tools**
  - Examples: `com.abilitykit.demo.moba.editor`
  - Responsibilities: editor windows, exporters, debug panels

### 2.2 Allowed dependency rule

- Dependencies should generally go **from higher tier -> lower tier**.
- Lower tiers must not depend on higher tiers.
- Editor packages can depend on runtime packages; runtime packages must not depend on editor.


## 3. asmdef rules

### 3.1 Non-transitive references

- asmdef references are **not transitive**.
- If assembly A references B, and B references C, A does **not** automatically see C.

**Rule:** When a type/namespace is missing, fix it by adding the **direct asmdef reference** where the source file is compiled.

### 3.2 Runtime/Editor/Tests boundaries

- `Runtime/*.asmdef`
  - Must not reference `UnityEditor` or any Editor-only asmdef

- `Editor/*.asmdef`
  - `includePlatforms: ["Editor"]`

- Tests
  - Prefer dedicated `*.UnitTests.asmdef` or `Tests/` with explicit test-only references
  - If an asmdef sets `overrideReferences: true`, you must list **all** required assemblies explicitly


## 4. Naming conventions

### 4.1 Package name vs assembly name

- Package ID: `com.abilitykit.xxx.yyy`
- Assembly name (asmdef `name`): should be stable and referenced by other asmdefs

### 4.2 Namespaces

- Namespaces are logical; assembly boundaries are physical.
- Prefer aligning namespaces with package responsibility, but do not rely on namespace to infer asmdef.


## 5. World DI + Module installation (critical)

### 5.1 Two registration mechanisms

There are two ways services get registered into the world container:

1) **Attribute scanning**

- `WorldServiceContainerFactory.CreateWithAttributes(...)`
- Scans assemblies for types annotated with `[WorldService]`
- Registers discovered service implementations into `WorldContainerBuilder`

2) **Module explicit registration**

- `WorldCreateOptions.Modules.Add(IWorldModule)`
- During `EntitasWorld.Initialize()`:
  - iterates `options.Modules`
  - calls `builder.AddModule(module)` which calls `module.Configure(builder)`

### 5.2 Rule: module must be attached to be executed

If a service is registered in `IWorldModule.Configure()` (example: `IUnitResolver` in `EntitasEcsWorldModule`), then **it will not exist** unless the module is added to `WorldCreateOptions.Modules`.

### 5.3 Entitas contexts resolution rule

In `EntitasWorld.Initialize()` the container registers `Entitas.IContexts` (interface). It may not register the generated concrete `Contexts` type.

**Rule:** Modules that require generated contexts must resolve `Entitas.IContexts` and cast to generated `Contexts`.


## 6. Dependency matrix (quick reference)

This is a practical guide (not exhaustive):

- `AbilityKit.Core`
  - depends on: (none)

- `AbilityKit.Attributes`
  - depends on: `AbilityKit.Core`

- `AbilityKit.World.DI`
  - depends on: `AbilityKit.Core` (and minimal infra)

- `AbilityKit.World.ECS / FrameSync / Snapshot / Record`
  - depends on: `AbilityKit.World.DI`, `AbilityKit.Core` (as needed)

- `AbilityKit.Ability.RuntimePkg`
  - depends on: `AbilityKit.Core`, `AbilityKit.Attributes`, `AbilityKit.World.*`

- `com.abilitykit.demo.moba.runtime`
  - depends on: `AbilityKit.Ability.RuntimePkg`, `AbilityKit.World.*`, `AbilityKit.Core`

- `com.abilitykit.demo.moba.view.runtime`
  - depends on: `com.abilitykit.demo.moba.runtime`, `AbilityKit.World.*`, `AbilityKit.Core`

- `com.abilitykit.demo.moba.editor`
  - depends on: `com.abilitykit.demo.moba.view.runtime` + runtime deps, plus any extra infra it uses


## 7. Checklist (when adding code)

### 7.1 When you add a new runtime type

- Which package owns it?
- Which asmdef compiles it?
- Which packages should depend on it?

### 7.2 When you add a new dependency

- Add `package.json` dependency (UPM level) if cross-package
- Add `.asmdef` reference (assembly level)
- Validate direction: higher -> lower only

### 7.3 When DI cannot resolve a service

- Is the service supposed to be discovered by `[WorldService]`?
  - confirm the implementation has the attribute and the scan assemblies include its assembly
- Or is it registered via module?
  - confirm `WorldCreateOptions.Modules` includes that module
- If it depends on Entitas contexts:
  - confirm module resolves `Entitas.IContexts` and casts to generated `Contexts`
