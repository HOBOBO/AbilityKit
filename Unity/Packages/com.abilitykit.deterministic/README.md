# AbilityKit Deterministic

`com.abilitykit.deterministic` provides a deterministic numeric facade for frame-sync simulation code.

This package intentionally stays independent from `core`, `pipeline`, `triggering`, `modifiers`, and the demo packages. Existing float-based packages remain valid for presentation, tooling, configuration, and non-frame-sync runtime paths.

## Current Scope

- `Fixed64`: signed Q32.32 fixed-point scalar with raw `long` storage (checked arithmetic; `Int128` multiply/divide on .NET 7+, exact `decimal` fallback elsewhere, including Unity's compiler).
- `FixedVec2` and `FixedVec3`: deterministic vectors with `Magnitude` / `Normalized` / `Distance` / `Dot` / `Cross` / `Angle` / `Lerp`.
- `DeterministicMath`: `Abs` / `Min` / `Max` / `Clamp` / `Lerp` plus `Floor` / `Ceiling` / `Round` (half up), `Sqrt`, and full trigonometry — `Sin` / `Cos` / `Tan` / `Asin` / `Acos` / `Atan` / `Atan2` — with `Pi` / `TwoPi` / `HalfPi` / `E` constants.
- `DeterministicRandom`: integer-backed repeatable random stream (xoroshiro128+ seeded via SplitMix64) with fixed-point output.
- `DeterministicHash`: stable 64-bit FNV-1a hashing over `Fixed64` / `FixedVec2` / `FixedVec3` for simulation state hashing (rollback reconcile, replay verification). Unlike `object.GetHashCode`, values are identical across processes and platforms by construction.

Floating-point conversions are boundary APIs only. Simulation logic should exchange raw fixed-point values, ratios, integers, or deterministic vectors.

## Determinism Guarantees

Every algorithm in this package is implemented with plain 64-bit integer operations — add / subtract / shift / compare — plus `Fixed64` arithmetic. No `double` or `float` is involved in any computation path, so results are bit-identical across .NET, Mono, and IL2CPP:

- Trigonometry uses CORDIC (rotation mode for sin/cos, vectoring mode for atan2) with a 32-entry `atan(2^-i)` table and gain pre-compensation.
- `Sqrt` uses a digit-by-digit restoring integer square root over the 96-bit operand `raw << 32`, rounded to nearest.
- `src/AbilityKit.Deterministic.Tests/DeterministicGoldenTests.cs` locks bit-exact raw outputs for sampled inputs; any change that shifts a single bit fails the gate (`core-stability` in `tools/test-gates.json`).

Accuracy is a handful of Q32.32 ulps (~1e-8) relative to `System.Math`, verified by tolerance tests against `System.Math` across periods and quadrants.

## Conventions

- Sources compile under Unity 2022.3 (C# 9): block namespaces, no C# 10+ syntax, no runtime feature dependencies.
- Public surface follows the repo idiom of static properties (`Fixed64.Zero`, `Vec3`-style) rather than public fields or constants, and is tracked by the PublicAPI analyzer (`PublicAPI.Unshipped.txt`).

## Backend Policy

The public AbilityKit types are deliberately narrow so the internal backend could later be replaced or bridged to a third-party fixed-point implementation. The current decision (2026-08) is to keep the self-contained implementation above: Q32.32 with `Int128` fast paths already covers the need, the missing math has been filled in with integer-only algorithms, and there is no external dependency to vet for licensing or determinism. ET's `cn.etetet.truesync` (`Fix64` / `TSMath` / `TSVector`) remains a known fallback if requirements change, but nothing depends on it.

## Consumers

Wired into the Unity package graph as the deterministic numeric core of the frame-sync stack (2026-08, roadmap P0→P3 complete):

- `com.abilitykit.core` — `MathUtil.Sqrt` routes through this package, so every `Vec2/Vec3.Magnitude` / `.Normalized` / `.Distance` and `Quat.LookRotation` in the repo is deterministic; `DeterministicMathBridge` (float-boundary facade: Normalize / Magnitude / Sqrt / ToFixed / ToVec3 / Quat.Normalize) lives in `core` as the single shared implementation.
- `com.abilitykit.combat.collision.abstractions` — sqrt/normalization points in raycast / sweep queries.
- `com.abilitykit.combat.motion` — trajectory lengths, locomotion normalization, wall-slide/leash solvers.
- `com.abilitykit.combat.projectile` — projectile kinematics (position / speed / distance budget in Q32.32; rollback snapshot v7 stores raw longs).
- `com.abilitykit.world.framesync` — `FrameTime` accumulates time in Q32.32 (rollback payload v2 stores raw longs).
- `com.abilitykit.demo.moba.runtime` — damage/heal/shield/resource pipeline in Q32.32 with single-conversion float boundaries (`MobaResourceFixedConvert`).

See the framesync package's 《定点帧同步接入指南》 (`com.abilitykit.world.framesync/Document/定点帧同步接入指南.md`) for the boundary rules and rollback conventions when adding new numeric fields.
