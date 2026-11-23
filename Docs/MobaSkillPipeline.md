# MOBA Skill Pipeline Publishing

## Sources

- The `技能` sheet in `LubanConfig/Moba/Production/Datas/J-技能表.xlsx` owns skill metadata and flow IDs.
- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/Moba/SkillFlowCO.asset` owns all 26 active skill pipelines, including LianPo's three-stage ultimate.
- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaSkillFlowAuthoring/Showcases/` holds the P1/P2 skill flow showcase sources and assets.
- ActionEditor `Resources/moba/action_timeline/skill_*.json` files own authored action tracks. A timeline phase may reference only its exported `*.moba.logic.json`; presentation clips stay in `*.moba.presentation.json`.
- `Production/Archive/skill_flows.xlsx` is migration history. It is no longer registered in `__tables__.xlsx` or used for publishing.
- Trigger effects are authored in the Trigger Authoring modules and published separately; see [MobaTriggerAuthoring](MobaTriggerAuthoring.md).

## Commit-sampled cooldown factors

`UseCommitCooldownFactor` is an explicit Flow opt-in for the previously approved stack-derived cooldown contract. Preparation resolves the existing cooldown inputs and stores that base on the original Cast blackboard. Exactly one explicit `ReserveCast` (`StartSkillCooldown`, `UseResolvedSkillCooldown`, `RequireExplicitCommit`) and one later required `CommitPoint` must lie on the sequential path. Do not retain a legacy `start_cooldown` RulePlan that overwrites the committed result.

At the first economy commit, `Skill.CooldownCommitFactor` resolves at `CastCommit/EventSample`, multiplies the prepared base, rounds milliseconds AwayFromZero, and freezes the effective total on that Cast. Duplicate commits do nothing; source changes cannot alter an active cooldown. Pre-commit cancellation refunds under the existing explicit reservation contract and starts no cooldown. Existing transaction/blackboard snapshots preserve preparation and commit state without a new snapshot layout. Flows without this option retain their original preparation contract.

Optional Modifier definition fields `StackStepSize`, `StackStepDelta`, `StackValueMin` map each source's effective stack count to `max(min, fixedBase + floor(stacks/step)*delta)`. This mapping requires a realtime fixed SkillParameter multiplier; zero step keeps existing stacking and forbids nonzero step-only values. Buff/Continuous remains the stack authority. Selectors bind content; the consumer never identifies a hero or skill. See [Veigar approval and scope](../plans/VeigarSkillAudit.2026-11-12.md) and [current implementation](../plans/VeigarFidelityRepair.2026-11-15.md).

## ActionEditor timing and early animation exit

For an authored Timeline phase, use the reviewed ActionEditor export's full top-level `length`, including delayed starts, overlapping tracks, and intentional tail time. A clip starting at 0.4s and occupying 0.8s ends at 1.2s; its material length alone does not define the phase. Independent projectile/area lifetimes remain separate.

`LogicJsonExporter` and `ActionTimelinePartition` preserve that total length in both runtime exports. `SkillTimelinePhaseDef.MobaLogicTimeline` must explicitly reference the logic export for `MobaActionTimelineCompiler` to derive `DurationMs` from it (milliseconds rounded AwayFromZero). A null reference uses the manually authored Timeline instead; matching filenames do not connect them. Multi-phase Cast duration still follows the phase tree.

When an animation exits early, first check the real source tracks, clip end bounds, both exports, the Flow reference, and any importer that may overwrite it. Empty 500ms timelines do not prove that separate HFSM animations are covered. Model animation metadata supplies playback facts; it does not automatically set phase duration. Do not introduce another completion window before checking this chain.

Follow the repository skill's [timing inspection workflow](../.claude/skills/moba-skill-authoring/references/action-timeline-timing.md), including delayed-start HUD verification and the read-only audit command. Configuration consistency alone does not establish complete playback.

The presentation Timeline consumer reads the exported animation `playbackSpeed`, `clipOffset`, and `clipLength` fields. At a Timeline offset in seconds, its source normalized time is `(clipOffset + offset * playbackSpeed) / clipLength`; omitted fields retain speed 1, offset 0, and the authored clip duration. HFSM seeking sets animation speed only when it supplies an actual playback intent, so a wait-only state action does not overwrite a Timeline-owned animation's speed.

## Owner-local child channels and independent recasts

A configured child channel belongs to the original Cast runtime, even after its short input Flow has completed. An independent recast binds the exact original runtime/generation/root context and child identity, with owner, slot, and base-skill checks. A valid binding uses the recast input's zero cooldown in release validation; the original loadout cooldown continues unchanged. Flight without a deployed Area cannot be selected for an Area recast.

On owner death, `EndOwnerChildChannels` revokes these channel capabilities in stable RuntimeId order: latch terminal first, despawn the exact attached child references, then force-terminate the original Cast. Manual projectile exit must not run the arrival/deployment path, and queued expiry/arrival cannot claim the terminal again. This cleanup applies to configured child channels; ordinary independent projectiles without a channel retain their existing lifetime behavior. Channel state, terminal latch, and recast bindings use the Cast Blackboard's existing rollback capture. See the [approved conditional mechanism](../plans/GragasConditionalMechanismDecision.2026-11-12.md) and [production verification](../plans/GragasProductionIntegration.2026-11-12.md).

## Concurrent authoring and safe append

Hero ownership does not imply file ownership. Multiple heroes share Luban workbooks, `SkillFlowCO.asset`, Trigger Authoring modules, and the Unity publication outputs. Parallel work is safe only when writers do not save the same source file from stale editor state.

- Assign one active writer per source file, not merely per hero. In particular, serialize edits to a shared `.xlsx`, `SkillFlowCO.asset`, or Trigger module. Different hero timeline files may be edited independently when they have distinct paths.
- Before editing, record the current diff and the IDs/references being added. Make a targeted row or asset-entry change keyed by its stable ID; do not replace a shared workbook, aggregate asset, or module with a copy loaded before another writer's changes.
- Before saving, check whether the file changed since it was opened. If it did, reload the latest version and reapply only the intended ID-level change. After saving, inspect the diff and verify that unrelated rows/entries remain, IDs are unique within their Luban namespace, and all new references resolve.
- If separate branches or worktrees are used, merge authoring-source changes first and resolve source conflicts before publishing. Do not merge generated runtime JSON as an authoring source or hand-edit it to resolve a conflict.
- Treat Unity publication as a single-writer operation for the shared Unity project. Do not run Luban or Unity publishers concurrently from two windows. Publish only after the combined authoring diff is stable; on any failed stage, stop and fix the source before proceeding.
- Stage or submit only the task's intended source paths and generated outputs. Review the final diff for changes from the other hero's work before committing.

For the current seven-hero rollout, use [MobaHeroIdRegistry](../plans/MobaHeroIdRegistry.md) as the only shared identity and resource-mapping reservation list. A `规划` row is not a production config row. Add a production Model/Vfx mapping only when the source asset exists and the intended model or effect use is known; add a Skill/SkillLevel/Flow/Trigger row only when its actual behavior and references are authored. Do not create blank or fabricated rows merely to make every reserved slot appear occupied. Keep planning, authoring, publishing, and in-game acceptance as separate statuses.

## Publish Order

1. Edit the ActionEditor source timeline, then save in ActionEditor or run `NBC.ActionEditor.LogicJsonExporter.ExportAllMobaTimelines` in Unity batch mode. Unsupported authoritative clips fail when the pipeline compiles the logic export.
2. For edits whose IDs and cross-resource references already exist in published runtime data, run the full Luban pipeline with `python LubanConfig/Moba/export_pipeline.py --apply`, then publish the Flow and Trigger authoring assets in Unity. This path is convenient for routine edits, but final validation still requires the checks below.
3. For a new hero, ID migration, or a batch that introduces Flow/Trigger references and the corresponding Luban rows together, use the staged order below. The authoring publishers validate against runtime config catalogs, so publish prerequisite tables before importing/publishing the new authoring IDs.
4. Once both authoring publishers have completed, run `python LubanConfig/Moba/export_pipeline.py --apply` for the full cross-resource check and final Luban publication. Then run `python LubanConfig/Moba/export_pipeline.py --check-published`, `MobaSkillPipelineExporter.Check`, and Trigger Authoring's `CheckRuntimeBatch`. All must pass before release.
5. Run `dotnet test src/AbilityKit.Demo.Moba.Tests/AbilityKit.Demo.Moba.Tests.csproj --filter "FullyQualifiedName~MobaLubanConfigPipelineTests|FullyQualifiedName~BattleFlowRealSkillTests"` and the Unity EditMode hero acceptance tests.

### Staged publish for new IDs or mutually-referencing assets

The apparent cycle comes from two separate validation boundaries. The Flow publisher resolves Flow IDs through the currently published Luban skill catalog, and Trigger Authoring resolves semantic IDs (such as Buff IDs) through the currently published Luban catalogs. In the other direction, the full Luban pipeline validates Skill-to-Flow and other cross-resource references against the published Flow and Trigger outputs. Each publisher therefore needs the other side's runtime data to be current, even though the authoring sources themselves do not form a dependency cycle.

Break that cycle by publishing validated prerequisites first, then the authored runtime assets, then running the full release gate:

```powershell
python LubanConfig/Moba/export_pipeline.py --apply-prerequisites
# In Unity, run the hero sync entry point or the equivalent ordered publishers:
# Trigger Authoring ImportProductionSourcesBatch
# MobaSkillPipelineExporter.Apply / Check
# Trigger Authoring PublishRuntimeBatch / CheckRuntimeBatch
python LubanConfig/Moba/export_pipeline.py --apply
python LubanConfig/Moba/export_pipeline.py --check-published
```

`--apply-prerequisites` still runs Luban schema, table, business-rule, and generated-artifact checks. It skips only cross-resource checks that require the not-yet-published Flow/Trigger outputs, and publishes only the Luban prerequisite tables/binaries/code. It does not bypass validation in the final `--apply` and must not be treated as a release-complete state. Generated runtime JSON remains publisher-owned; do not edit it by hand.

For Annie's current ID migration, the Unity entry point is **Tools > AbilityKit > Demos > Moba > Annie > Publish Passive Q W**, or batch mode `AbilityKit.Ability.Impl.BattleDemo.Moba.Editor.AnnieSkillSync.SyncAndPublish`. Run the prerequisite command first, then this entry point, then the two full Luban commands above. The entry point imports Annie's Trigger Authoring sources, publishes/checks Annie's Flows, and publishes/checks Trigger runtime data in that order.

## Pipeline/Trigger reference contract

Every Trigger ID execution list (RulePlan, Window open/tick/close, Buff, Projectile, Area, and related gameplay lists) may contain a Trigger ID at most once. The runtime reference validator reports `moba.skill.contract.duplicate_trigger_reference` as a blocking error for duplicates. This prevents an accidental repeated damage, Buff, resource, or spawn effect when a phase is assembled from Pipeline data. If an effect must execute more than once, model the repetition explicitly with a `Repeat` phase or author separate Trigger IDs whose different event/effect intent is visible in Trigger Authoring; do not rely on duplicate IDs as an implicit repeat.

Flow structure is also a blocking contract: a Flow must contain at least one supported Phase, and every composite Phase must contain a child. Unknown Phase types, null Phases, empty Flows, and empty composites are rejected with stable `moba.skill.configuration.*` validation codes before runtime pipeline construction. This keeps the editor/publish gate aligned with the runtime builder, which cannot construct these shapes.

RulePlan, Window open/tick/close, and Timeline `EffectId` references are required to point to `TriggerPlanScope.Global`. These entries execute directly inside the skill pipeline: RulePlan and Window use the current `SkillPipelineContext`, while Timeline passes the effect ID to `MobaEffectExecutionService` for direct plan execution. They are not owner-bound subscriptions. The validator reports `moba.skill.contract.invalid_trigger_scope` when an owner-bound plan is referenced from one of these entries.

Event-driven direct lists use the same split at runtime. Buff `OnAddEffects`/`OnRemoveEffects`/`OnIntervalEffects`, Continuous `IntervalTriggerIds`, Projectile `On*TriggerIds`, and Area `On*TriggerIds` are dispatched by `MobaTriggerExecutionGateway.ExecuteDirectTrigger` and therefore consume `Global` plans. The reference validator enforces that scope for these lists. Owner-bound behavior associated with an event is registered from Passive/Buff/Continuous owner bindings and is executed by `MobaTriggerPlanSubscriptionService` through the typed event subscription path. `MobaEffectExecutionService.ExecuteTrigger` checks this boundary again before constructing an execution context; a rejected OwnerBound plan returns `false` and the gateway does not count it as executed. This prevents a direct event list from bypassing owner gates or executing an owner plan without its source context.

The scope rule is enforced twice. Configuration validation rejects an invalid reference during startup, and every runtime entry point checks the loaded `TriggerPlanJsonDatabase.Record.Scope` again before execution. `MobaTriggerPlanExecutor.ExecuteRulePlan` and `MobaEffectExecutionService.ExecuteRulePlan` return `false` for an owner-bound plan; `MobaEffectExecutionService.ExecuteWithResult` throws an explicit scope error for a Timeline effect; `MobaEffectExecutionService.ExecuteOwnerBoundTriggerActions` returns `false` for a Global plan. The two execution routes are mutually exclusive: direct Pipeline/Timeline entries accept only `Global`, while the owner subscription path accepts only `OwnerBound`. This second guard protects hot-loaded or manually injected plans when the full startup validator was not run. Owner-bound plans remain executable only through `MobaTriggerPlanSubscriptionService` and its owner event subscription path. This keeps the boundary aligned with the existing architecture: Pipeline owns one cast's direct sequence, while subscriptions own event-driven behavior.

Ongoing owner-bound bindings are reconciled once per world tick from the active `OngoingTriggerPlans` component. The reconcile cache uses the list hash as a fast change check, but stores and compares the exact trigger ID sequence as well; a 32-bit hash collision must still cause the subscription set to refresh. Empty or missing owners are stopped and removed from the cache so a despawned actor cannot retain an event subscription.

`MobaTriggerPlanSubscriptionService.OnInit` is re-entry safe: a repeated world initialization or hot-reload first disposes existing owner registrations, releases their owner blackboards, and clears evaluation snapshots before rebuilding the plan indexes. Updating an owner binding also removes evaluation snapshots for trigger IDs no longer present; diagnostic reads therefore describe only currently subscribed plans and cannot retain stale owner/trigger entries.

For Unity batch commands, use the local Unity 2022.3 editor with `-batchmode -nographics -projectPath <repo>/Unity -executeMethod <method> -quit -logFile <log>`. For `-runTests`, omit `-quit` so the test runner can finish and write NUnit XML. The test runner in `tools/run-unity-editmode-tests.ps1` handles this.

The default runtime profile loads all other registered tables from Luban bytes and loads `skill_flows` from `Resources/moba/skill_flows.json`. The Console uses its published `Configs/moba/skill_flows.json`. Keep flow DTO registration in place; it is the runtime contract even though Luban no longer generates a Flow table.

## Runtime Rollback Contract

The Pipeline package exposes run phase instances, execution position, and lifecycle calls without owning a rollback format or policy. The MOBA skill layer stores the versioned phase tree and each active skill runner's stage, context, policy, cast sequence cursor, and run bytes. Restore rebuilds phase instances from `IMobaSkillPipelineLibrary`, rebinds event subscriptions and pipeline continuous state, and does not execute pipeline start actions again. The skill runtime provider restores its Blackboard and skill-phase execution-context lifecycle before the runner provider imports.

These snapshots support **in-process prediction rollback**. The Triggering package exposes scheduler entry progress and lifecycle operations; the MOBA trigger runtime owns payload validation, entry selection, ID cursor restore, and the retention window for executable definitions. New snapshots contain waiting entries only. Finished definitions remain available until the MOBA receives a confirmed frame beyond the configured rollback history, then the MOBA service releases them. Preflight requires the saved executable closures to remain available. Execution-context lifecycle restore also requires the original context identities. A fresh process cannot use these local rollback payloads as a reconnect or full-world recovery snapshot.

In a joint lifecycle world, structural provider 10011 revokes old Continuous contributions and rebuilds the target skill-parameter projection from its owners. The later value provider 10015 validates its payload but must not clear or import that projection again. Standalone provider behavior is unchanged. This is an ownership-order repair, with no payload version change; see `local/docs/Moba投射物区域位移组合验收.2026-11-17.md`.

Actor transform recovery is not complete Motion-source recovery. The approved [managed Motion recovery decision](../plans/MobaManagedMotionRecoveryDecision.2026-11-17.md) assigns exact FixedDelta source values (including raw time, activity, velocity, arbitration and collision policies) to joint lifecycle continuous-local. Lifecycle envelope v4 rejects v3; other provider versions are unchanged. Recovery requires the same surviving managed instance and original source bound to its owner Pipeline, validates frozen configuration, and restores silently without activation or finish callbacks. Missing, replaced or unsupported managed sources fail preflight; source values participate in group compensation. Other source types, external Pipeline sources and destroyed-instance reconstruction remain outside this contract.

Managed Motion reserves an identity from the existing rollback-owned ModifierSourceId allocator even when it has no modifiers. This distinguishes an identically configured replacement without introducing nondeterministic process identifiers. The earlier arrival +1 checkpoint retains an unresolved execution-context/skill-runtime discrepancy recorded in the `local/docs/Moba受管位移精确恢复验收.2026-11-17.md`.

The approved [passive rollback rebind decision](../plans/MobaPassiveContinuousRollbackRebindDecision.2026-11-17.md) separates restored bindings from normal loadout changes. In the formal joint lifecycle registry, component provider 10018 marks the actual imported SkillLoadout for one reactive rebind. That rebind updates passive listeners, owner bindings and ongoing plans without upserting, admitting, reconciling or ending the checkpoint's interval instances; provider 10011 already restored their membership and index. Absent checkpoint intervals remain absent even if currently admissible. A subsequent real SkillLoadout replacement invalidates the mark immediately, including a replacement before reactive consumption; normal synchronization still evaluates RequireOutOfCombat and cleans removed passives. Marks are cleared on consumption, actor unregister and teardown. No cooldown, combat-activity, protocol or production-content changes are involved. Formal R replay at the mid-motion checkpoint now requires byte equality for all providers with gated passive targets too; evidence is in the `local/docs/Moba被动恢复重绑定验收.2026-11-17.md`.

## Runtime lifecycle hook contract

`MobaSkillRuntimeLifecycleHookService` is an observation and integration seam for the skill runtime aggregate. It does not own skill state, child retention, pipeline completion, or rollback decisions. The runtime service remains the only writer of lifecycle state; hooks receive the resulting `MobaSkillRuntimeLifecycleEvent` after the corresponding transition has been recorded.

Each `Notify` call dispatches from a stable snapshot of the hooks registered at the start of the call. A hook may unregister itself, unregister another hook, or register a follow-up hook while handling an event. Those mutations affect the next event: they cannot skip an existing hook, invoke a newly registered hook reentrantly, or change the current event's ordering. Registration remains idempotent and `Clear` removes all future notifications.

Hook failures are isolated per callback. The service continues dispatching the rest of the snapshot, retains the first exception as the call's failure, logs later exceptions, returns the pooled snapshot in `finally`, and then rethrows the first exception. Hook code therefore must be observational or explicitly recoverable; it must not assume that a later hook has already run, and it must not use callback order as a gameplay rule. Gameplay effects, state transitions, and rollback data belong in the runtime, Pipeline, Trigger, or Modifier owners described above.

The snapshot list is pooled to avoid a per-event long-lived allocation. Pooling is an implementation detail; callers must not retain the lifecycle event or mutate hook registration from another thread. Runtime lifecycle notifications are main simulation-thread operations and should be consumed synchronously before the next world tick.

## Skill child retention contract

Every child retained by `MobaSkillCastRuntimeService` is represented by one `MobaSkillRuntimeRetainHandle`. The handle records the runtime identity, generation, child identity, and a monotonic retain ID. Both the legacy `runtimeId + child` API and the handle-based API enter the same registration path; the legacy release overload first consumes the matching handle, so a child cannot be released while leaving an invisible capability in the service registry.

Release is exactly once. A stale handle, a mismatched runtime generation, a different child identity, or a second release is rejected. `ForceTerminate` and `Clear` revoke all remaining child capabilities before finalization. `ValidateRetentionState` provides a diagnostic consistency check: every active child must have one retain handle, and every registered handle must point to an active runtime child. A nonzero orphaned or untracked count is a lifecycle defect and must be fixed at the owning Buff, Projectile, Area, Summon, or derived-skill cleanup path rather than hidden by forcibly deleting the registry entry.

Keep skill Flow, timeline and trigger definitions fixed for the duration of a rollback history window. The MOBA run codec checks version, config ID/name, phase IDs/types and bounds, but it does not fingerprint all authored timing and effect parameters. `SkillPipelineContext.SharedData` accepts only the codec's primitive/value types; capture fails for an unsupported object so a partial state is never recorded as a valid snapshot.

## Temporary combat object cleanup

A failed or rolled-back skill cast releases its Actor, Projectile/Area runtime link, execution context and skill-runtime retain in reverse creation order. Area cleanup uses `IAreaRollbackService.CancelArea`; `DespawnArea` remains reserved for gameplay removal because it publishes `AreaExpireEvent`. This prevents cancellation and prediction rollback from being interpreted as an expire trigger and keeps multi-area skills deterministic when a later spawn fails.

## View snapshot consumption contract

`BattleClientSnapshotStream` is the ownership boundary between gateway ingress and presentation projections. Network callbacks enqueue immutable-by-convention `GatewayStateSyncSnapshot` values; the presentation thread drains the queue and applies accepted snapshots synchronously. The stream owns its actor dictionary and never exposes that dictionary or a reusable actor buffer to a projection.

`AttachProjection` and full-snapshot recovery build a complete baseline from the stream's actor cache. Actor IDs are kept in a cached sorted order and rebuilt only when the actor set changes; the baseline actor array itself is a fresh array for that attach/recovery operation. This keeps sorting and temporary list creation out of the normal `Present`/`Drain` path while preserving deterministic actor order.

Projection `Apply` callbacks must consume the supplied snapshot synchronously and must not use it as a cross-thread or long-lived mutable cache. A projection that needs interpolation owns a copy or its own ring/buffer. `DetachProjection`, session stop, and stream reset call `Reset`; after detach/reset no further snapshot is delivered to that projection. Recovery discards deltas until a full snapshot is accepted, then rebuilds the projection baseline through the same cache-copy boundary.

Presentation cue reconciliation has a separate history contract. The controller's capacity is a history bound, not permission to discard an active cue. Active keys retain prediction/authority history until deactivation or generation reset; only inactive entries may be evicted. Receipt records carry an entry identity so a key that is removed and later reused cannot be deleted by an old queue record. This is required for predicted skill VFX that remain active while later authoritative confirmation, correction, or rejection arrives.

Session feature teardown is best-effort across the complete step list. A failed step does not short-circuit later cleanup; the lifecycle controller records only failed steps and retries those steps on the next detach attempt. This makes subscription, snapshot buffer, network binding, and presentation resource cleanup independent while retaining deterministic failure reporting.

## Reliable event baseline contract

Reliable battle event delivery treats each `RequireAuthoritativeBaseline` call as the start of a new recovery window. Batches retained while waiting for the previous baseline are discarded before the new window accepts events; otherwise an old out-of-order batch can be replayed after the new watermark and raise a second sequence-gap recovery. Batches that arrive in the current window are replayed only after the matching timeline baseline is adopted, and the cursor still removes events covered by the authoritative watermark.

Pending batches cross the transport callback boundary, so the delivery runtime snapshots their event list before retaining them. Network decode buffers and caller-owned lists may be reused or mutated after `Handle`; replay must observe the batch as received. The queue bound is a batch bound and remains a recovery safeguard; event-level ordering and watermark coverage remain the cursor's responsibility.

The delivery queue also enforces an event-count bound (`MaxPendingEvents`, default 512) in addition to the batch-count bound. A single oversized push therefore cannot consume an unbounded amount of recovery memory. Overflow clears the pending window and reports one `PendingQueueOverflow` invalidation for that recovery window; subsequent pushes are discarded and are not reintroduced into the queue until a new baseline or generation resets the signal. A successful baseline starts a fresh window and may report a later overflow independently. `RecoveryWindowId` identifies the active recovery window, while `PendingOverflowCount` records cumulative overflow pressure for diagnostics; neither value is used to make gameplay decisions.

While waiting for an authoritative baseline, a batch whose stream identifier does not match the active cursor is stale data from another battle and is dropped before entering the pending queue. The runtime increments `PendingInvalidBatchCount` and emits an observed `BatchRejected/InvalidStream` diagnostic, but does not invalidate the current recovery window or request another resync. Timeline mismatches are still retained during recovery because a new authoritative baseline may establish a new epoch; after the baseline is adopted, replay remains restricted to the matching timeline.

The MOBA authoritative recovery coordinator associates `ReliableEventResyncRequired` with this window ID. Repeated reliable-event invalidations observed in the same window are suppressed after the first report, so a burst of equivalent failures cannot enqueue duplicate full-state requests. Starting a new generation resets the association. Executing the recovery action calls `RequireAuthoritativeBaseline`, which advances `RecoveryWindowId`; a later failure therefore remains eligible to retry. This suppression applies only to reliable-event resync signals and is diagnostic coordination state, not a Pipeline, Trigger, or gameplay decision.

### Approved continuation and auxiliary recast contracts

The 2026-11-14 approved D2 normal recovery contract is authored as `Timeline.RecoveryStartMs` (`-1` disables it, `0` permits handoff after immediate effects). Initial support is limited to the final top-level Timeline: its entry must precede the complete authored duration, all its gameplay events must be at or before entry, and no later/nested phase may declare recovery. Runtime config loading and the editor publisher both validate this boundary. ActionEditor compilation retains the full length and copies only the explicit recovery setting from Flow authoring.

Recovery eligibility derives from the current terminal phase, its deterministic elapsed time, its submitted-event cursor and execution/control validity. Existing Pipeline snapshots already store these facts; restoration neither replays effects nor needs a second lifecycle table. A successful incoming cast (including the normal attack slot) completes conflicting recovery runs through the normal completion path. A rejected input leaves the old run intact. Nonzero valid movement input can finish recovery only when combat rules allow movement. Without new input the full authored action continues. Handoff never cancels the original Cast: `MarkPipelineEnded(PipelineCompleted)` retains its committed children, and source-qualified cleanup cannot refund committed payment. Ordinary death/interrupt semantics and unconfigured flows retain their contract.

Initial content: Lux W entry 250ms, matching its existing projectile submission; Annie E entry 0ms, matching its immediate shield submission. Complete lengths remain 5042ms/4167ms. Meraki supplementary descriptions report 0.25s/none respectively but are latest data, not independent proof of all 16.22.1 timings. Further skills require individual timing review.

The bounded extensions approved in [Lux readiness](../plans/LuxImplementationReadiness.2026-11-13.md) are explicit Flow fields and default to false:

- `ContinueThroughControl` separates admission from continuation. New casts still pass the skill's ordinary combat gate. An existing cast ignores that skill's control result mask in both per-cast checks and its Pipeline Continuous execution gate; explicitly configured Continuous template requirements still apply. Death and removal terminate the cast. Character HFSM retains this cast action through ordinary control.
- `PreserveCharacterCastContext` makes an auxiliary cast leave the primary character Cast, aim and action cursor intact. HFSM cast selection and `TryGetLatestRunning` exclude that auxiliary context; its own runtime, effects and cleanup remain independent.
- `QueueRecastInFlight` lets a recast bind the exact original channel, slot, base skill, Cast generation and unique projectile. Repeated requests set one pending flag. Landing retains the Area before releasing the projectile and consumes the pending request through the same terminal claim as deployed recast. The original Cast blackboard carries pending identity through rollback; death clears the owned channel.

`resolve_cast_area` supports `pending_only` for landing handover and `owned_area` for resolving a unique Area owned by the current Cast. Area callbacks carry the original center and forward vector; the query reads that frame rather than the caster's later transform. These contracts contain no hero identity checks.

Character presentation binding must preserve a live action's progress when a forward snapshot is behind the frame already sampled by the view timeline. Binding the same action/Cast samples at the later frame instead of replaying one-shot clips; explicit backward seeks still stop/rebuild presentation as before. Placement-only (`Follow=false`) timeline particles must not retain the moving implicit self Transform as a parent. These are consumer contract repairs, not a source of gameplay timing or aim authority.

### Approved Cast presentation frame

The [CastFrame decision](../plans/LuxCastPresentationFrameDecision.2026-11-13.md) was explicitly approved. `spawn_area.capture_cast_frame=true` captures the single resolved position before offsets and normalized horizontal aim before shape yaw in the original Cast blackboard (module 1002, Vec3 keys 140/141). Multiple-target capture is rejected. First capture wins; auxiliary casts and later actor movement never replace it. Existing blackboard rollback restores values without world references.

`CharacterActionData.CastFrame` projects that optional value by original Cast context and caster identity. CharacterAction codec v3 writes an optional frame and validates finite origin/unit horizontal forward; v1/v2 remain readable with no frame. The frame is presentation data and never changes character facing or hit queries. Templates with `PlacementSpace=0` retain existing attachment behavior; `1` requires the matching Cast frame, applies template offsets in its local space and produces a fixed world request. Missing frames reject playback, rather than infer aim from the visual Transform. Requests carry value copies across asynchronous loading; the existing Timeline/Cast lifecycle owns cancellation and cleanup.

Model action sampling uses the resolved action catalog's explicit duration when present. Unity may return a default/stale `AnimatorStateInfo.length` immediately after a zero-delta seek, even while its current Clip has the correct longer duration. That fallback cannot override known authored timing. Actions without explicit duration retain the Animator-length fallback; simulation Cast timing and actor movement remain authoritative.


## Explicit ground point range policy (2026-11-15)

Skill button `PointRangePolicy` defaults to `Legacy=0`. `ClampToResolvedRange=1` is valid only with `AimMode=Point` and `UsePointMode=TargetPoint`. CastPrepare resolves the existing CastRange Modifier, rejects nonfinite coordinates/range or negative range, and clamps XZ distance from the current caster while preserving submitted ground Y. The final AimPos is stored in the original Cast/request and existing snapshots; all Areas, triggers and VFX consume that same point. A zero range selects the caster's XZ. No new snapshot fields or hero dispatch are introduced.

For opted buttons the HUD uses positive static `SelectRange`, falling back to `Skill.Range`; its TargetCircle `IndicatorWorldWidth` is the world radius. Runtime resolved range remains authoritative. Dynamic external range synchronization to HUD is outside the approved minimum scope. Unopted content preserves its existing behavior.
