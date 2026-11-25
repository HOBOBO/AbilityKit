# MOBA summon behavior activation foundation

## Purpose

This increment closes the creation-frame gap between a configured summon and its independent behavior runtime. A summon can be created by the skill pipeline after `MobaBrainSystem` has already completed its `BrainTick` for the frame. Before this change, the new actor had `ActorBrain` metadata but no `BehaviorRuntime` until the next frame's Brain pass. That made summon initialization depend on an otherwise invisible system-order delay and left the creation frame incomplete for consumers that inspect behavior ownership immediately.

The service now binds the runtime after the summon has received its configured components and skill loadout. It does not make a behavior decision or move the summon out of order. `MobaBrainSystem` remains the sole ordered decision tick.

## Creation and first decision timing

```mermaid
sequenceDiagram
    participant Skill as Skill pipeline
    participant Summon as MobaSummonService
    participant Actor as Summoned ActorEntity
    participant Brain as MobaBrainService
    participant Tick as MobaBrainSystem / BrainTick
    participant Output as Brain output and motion systems

    Skill->>Summon: TrySummon(...)
    Summon->>Actor: create, configure, register, install loadout
    Summon->>Brain: EnsureBehavior(actor)
    Brain->>Actor: write BehaviorInstanceId
    Note over Actor,Brain: Creation frame: actor and behavior runtime are both bound
    Tick->>Actor: EnsureBehavior(actor), idempotent running-runtime lookup
    Tick->>Brain: Tick behavior decision
    Tick->>Output: publish/apply outputs in configured system order
    Output->>Actor: movement input consumed by later motion systems
```

`MobaSummonService.TrySummon` follows the existing setup order: resolve the summon configuration and root owner, prepare capacity, spawn/register the actor, apply owner/root-owner and summon lifecycle metadata, install component templates and skill loadout, bind Brain if the actor has `ActorBrain`, then track summon/source ownership and retain the originating skill runtime when required. The immediate `EnsureBehavior` call is deliberately after the loadout step so a behavior driver that reads the actor's installed skills sees the configured actor.

`EnsureBehavior` is idempotent for a running runtime. On the next `BrainTick`, `MobaBrainSystem` calls it again and receives the existing runtime; the system then ticks the decision. The call during creation only resolves the definition, creates/registers the runtime, and writes its `BehaviorInstanceId`. It does not invoke the decision driver tick, apply movement output, or bypass `MobaSystemOrder`. If definition or driver creation fails, the summon itself remains valid and the existing Brain service failure suppression/logging rules apply; this change does not turn Brain configuration failure into summon spawn failure.

The observable contract is therefore:

| Point in time | Actor metadata | Behavior runtime | Decision/output |
|---|---|---|---|
| Before summon setup completes | Not yet final | None | None |
| Return from successful `TrySummon` with configured Brain | `ActorBrain.BrainId` and positive `BehaviorInstanceId` when binding succeeds | Created and owned by `MobaBrainService` | Not ticked yet |
| Next ordered `BrainTick` | Same Brain identity | Existing runtime reused | First decision executes, then output follows the configured system order |

## Ownership and nested summons

Summon caster and summon root owner are different concepts. A nested summon stores its immediate caster in `OwnerLink.OwnerActorId`, while `OwnerLink.RootOwnerActorId` remains the original hero. Capacity accounting and root-owner death cleanup use the root owner. This means a summon created by Tibbers-like AI still consumes the owning hero's configured summon capacity and can be cleaned when the hero dies, even if the direct caster is another summon.

`DespawnOnRootOwnerDeath(rootOwnerActorId)` compacts stale tracked entries and selects only summons whose `SummonMeta.DespawnOnOwnerDie` is true. It then routes each through `TryDespawn(..., OwnerDead)`, preserving the single cleanup path for Brain deactivation, despawn snapshot, owner/source tracking, skill-runtime retain release, lifecycle notifications, registry removal, and entity destruction. It is explicit death policy: a dead hero can remain in the registry during death/respawn, so registry absence is not a substitute for a death signal. Summons configured to survive owner death are not removed by this method.

## Capacity replacement and deferred cleanup

Capacity is evaluated per root owner and summon id. `ReplaceOldest` and `ReplaceNewest` submit despawn requests for the selected old summons and then allow creation of the replacement. The request is consumed by the ordered actor despawn cleanup system, so the old entity can remain registered for the rest of the current frame while the new entity is already registered. This is an intentional deferred-cleanup window, not an atomic swap.

Consequences for callers and tests:

- Immediately after a replacement `TrySummon`, a registry query may briefly see both the old summon with `ActorDespawnRequest` and the new summon.
- The replacement request keeps its `SummonReplacedByLimit` reason and follows normal cleanup/snapshot handling.
- After the despawn cleanup tick, the live count returns to `MaxAlivePerOwner` (one in the regression fixture).
- Consumers that need the stable post-cleanup count should query after the ordered cleanup phase, rather than assuming `TrySummon` synchronously destroys an old actor.

This preserves system ordering and lets remote/presentation consumers observe an explicit despawn contract. If a future skill requires a same-frame atomic capacity swap, it needs an explicit reservation/commit policy and failure compensation; this foundation does not claim that behavior.

## Failure, rollback, and presentation boundaries

Summon creation remains transactional around actor registration and ownership bookkeeping. Brain binding occurs before summon tracking/skill-runtime retain completion. If a later setup step fails, the summon compensation path destroys the actor and releases tracked resources; Brain teardown must also release any runtime already attached to that actor. Normal despawn deactivates Brain before entity destruction. Rollback is distinct from gameplay despawn: rollback reconciliation removes/rebuilds entity state without publishing an authoritative gameplay despawn snapshot, and Brain runtime state is reconciled by the rollback lifecycle path.

The logic-side activation change does not itself create a model, animation, VFX, or network event. Those remain driven by the existing actor spawn/despawn snapshot, presentation catalog, and summon lifecycle cue contracts. A view may learn about the summon at spawn snapshot time while the logic Brain is already bound; a view must not attempt to simulate the behavior decision.

## Regression coverage

`MobaSummonBTreeSkillSmokeTests` covers three linked contracts:

1. The configured summon has `ActorBrain`, a positive behavior instance id, and a registered `MobaBTree` decision immediately after `TrySummon` returns, before another battle tick.
2. A nested summon links to its immediate summon caster while preserving the hero root owner; root-owner death removes both policy-enabled summons through normal cleanup.
3. With `MaxAlivePerOwner = 1` and replacement policy, the previous summon first carries a deferred `SummonReplacedByLimit` request, then disappears after cleanup while the new summon remains and the stable alive count is one.

Focused verification:

```text
dotnet test src/AbilityKit.Demo.Moba.Tests/AbilityKit.Demo.Moba.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~MobaSummonBTreeSkillSmokeTests"
```

Result: 4/4 summon behavior Smoke tests passed; the combined summon behavior and summon despawn snapshot run passed 7/7. The build emits extensive pre-existing package and analyzer warnings, but this focused build-and-test command completed successfully. This is targeted test evidence, not a full-repository test run.
