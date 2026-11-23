# MOBA Trigger Authoring

`payload_actor_type(participant, main_type, unit_sub_type?)` reads existing Actor classification from an event participant
(0=target, 1=source); absent actors/components fail. It does not require the participant to remain alive/selectable.
Buildings are Unit subtypes too: content must explicitly allow combat subtypes rather than assuming MainType Unit excludes towers.

Without an original Cast, synchronous formal effects can explicitly write/read `skill_runtime:effect.*` in their own
EffectExecutionScope scratch board. Each effect identity has an isolated board, discarded when the scope ends; no Cast,
target or child persistent board is synthesized. Original Cast effects retain their existing Blackboard/rollback contract.
Plain unfiltered Self actions resolve the caster identity directly, including death-entry lifecycle Buffs; template/filtered
queries keep combat search filtering and damage/control consumers continue their own legality checks.

`spawn_area.forward_yaw_deg` is an optional shape-only yaw (default 0, positive rotates +Z toward +X).
Aim-relative placement offsets retain their original basis. The resolved Forward uses the existing Area snapshot path.

`sample_skill_parameter(skill_id, parameter, result)` reads the caster's current learned rank and the named production
level parameter; an unlearned skill yields zero. It does not change the original Cast parameter snapshot.
`try_consume_resource(resource_type, amount, death_cast_skill_id, result)` atomically pays a standalone periodic cost,
returning numeric 1/0. It never skips payment because the originating Cast already reserved its activation cost.
For a dead owner, only an active configured death-casting permission for `death_cast_skill_id` permits a free tick;
otherwise it returns 0. Callers branch on that result before applying effects. Interval scheduling and cleanup remain
owned by the existing Buff/Continuous, including its rollback clock. Numeric outputs must use declared writable targets.

## Aim-relative area placement and projectile terrain layers

`spawn_area` keeps world offsets (`offset_x/y/z`) separate from aim-relative offsets.
`direction_offset` moves along normalized planar aim forward; `lateral_offset` (default 0)
moves along right `(forward.Z, 0, -forward.X)`. `position_mode=2` uses the committed aim
position as its center. Offsets only affect creation geometry; group claims, arming,
Enter delivery and lifetime retain their existing contracts.

The production Projectile table's `CollisionLayerMask` uses 0 for the existing
`UnitMask | WorldMask` default; nonzero selects the collision layers explicitly.
Both direct Shoot and launcher emissions resolve it before constructing SpawnParams.
The resolved mask travels through the existing projectile snapshot/restore path.
Unit subtype and team filtering remain independent from terrain layer selection.
Approval and the first content mappings are recorded in
`plans/JinxLiveGameplayRepairAcceptance.md` (2026-11-11).

## Ownership

- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaTriggerAuthoring/Packages/` contains the four production Trigger Authoring modules: skills, buffs, passives, and gameplay.
- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaTriggerAuthoring/Sources/` contains their editable source snapshots. Keep each Source document synchronized through the Trigger Authoring workspace.
- The related skill flow showcases live in `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaSkillFlowAuthoring/`.
- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/ability/ability_trigger_plans.json` and `src/AbilityKit.Demo.Moba.Console/Configs/luban/ability/ability_trigger_plans.json` are generated runtime artifacts.
- The `Ability节点` sheet in `LubanConfig/Moba/Production/Datas/J-资源节点表.xlsx` and `Resources/ability/triggers/` retain the migration baseline. They no longer publish or load production trigger effects. The two old tutorial references without config rows (Buff 100001 and query 101) map to the existing demo Buff 1 and range query 1 during the one-time migration.
- The empty passive entry 10060000 is authored as a debug-only marker because the Mozi passive table references it.

## Runtime rule overlays

Production loads the required aggregate before applying directory rules. A rule with the same Trigger ID replaces the entire aggregate record; validation applies to the effective record. This does not allow duplicate IDs within the aggregate publication inputs. See the [loading baseline](TriggerPlanLoadingBaseline.md) for host behavior and verification.

## Scope contract

Trigger `scope` is part of the reference contract. Validate it by the consumer location, not by whether a Trigger is stored in the skills, passives, buffs, or gameplay module:

| Reference location | Required scope |
|---|---|
| Active skill Flow RulePlan and Timeline effects; projectile and area events; gameplay events; Buff add/remove/interval effects; Continuous interval events | `Global` |
| `PassiveSkill.TriggerIds`, `Buff.TriggerIds`, `Continuous.TriggerIds` | `OwnerBound` |
| `advance_gameplay_counter.trigger_id`, `consume_gameplay_counter.trigger_id` | `Global`, `Callable` |

The runtime battle-config validator blocks session startup when a reference has the wrong scope. After publishing, run the runtime validation and both `CheckRuntimeBatch` and the relevant skill acceptance/smoke tests; check that Unity and Console plans match the production modules. The Console runtime artifact is `src/AbilityKit.Demo.Moba.Console/Configs/luban/ability/ability_trigger_plans.json`.

Counter callbacks execute directly through the effect service. They must not point at an owner event subscription or be included in `PassiveSkill.TriggerIds`; the published counter-callback contract test checks both scope and callable entry. Payload actor fields must match the subscribed event: cast events use `caster.actor_id`, while damage events use their attacker/target fields.

Global skill effects cannot use owner-local blackboards. For a temporary target query and bounded `for_each`, use writable Context values `skill_runtime:effect.target_collection` and `skill_runtime:effect.current_target`. These use the effect runtime's data scope rather than sharing a module variable between owners.

For a shared residence sample, place an explicit `set_num_var` before its consumers: write Payload `area_residence_ratio` to writable Context `skill_runtime:cast.residence_ratio`. Damage expressions read `skill_runtime.cast.residence_ratio` from the original Cast. Context write targets must be registered as writable value sources; registering a field does not sample it automatically. The original Cast Blackboard owns the input and includes the write in its existing rollback snapshot.

Numeric Attribute Modifiers can explicitly select `skill_runtime:cast.<key>`, `skill_runtime:target.<key>`, or `skill_runtime:child.<key>` as their ContextFloat key with OnApplySnapshot evaluation. The projector reads the exact original Cast handle and actual target/Continuous child identity on first application, then freezes the result in the Continuous instance's existing modifier captures. Missing inputs or invalid source identity reject application. Subsequent projection and pause/resume use that capture after the Cast ends. Ordinary refresh preserves the first capture; writing the Blackboard again does not refresh an active Modifier. Unprefixed ContextFloat keys retain their existing attribute-context meaning. This contract adds no Buff parameter store and does not expose Effect scope to persistent Modifiers.

When only the existence of a matching target matters, use condition `has_query_target` with a required positive integer constant `query_template_id`. The referenced query template owns the source, center, filters, radius, ordering and count. The predicate runs through the existing deterministic target resolver inside the current effect execution scope, returns false for no match or absent execution context, and releases its pooled list in `finally`. It writes no blackboard value and allocates no collection handle. Authoring rejects invalid template IDs; a missing template is a runtime configuration error. It supports both a Trigger condition and a `conditional` action condition. See [the approved query scope design](design/09-ImplementationExamples/MOBA/43-MobaExplicitTriggerQueryScopeProposal.md).

`give_damage` accepts one base damage source: fixed `damage_value`, `damage_value_skill_level_parameter`, `source_attack_ratio`, or a `magnitude_*` formula. The optional `damage_value_ref` resolves an additional numeric value from the current Trigger payload and may be combined with a fixed, ranked, or attack-ratio base; it cannot be combined with `magnitude_*`, which defines its own complete base formula. `damage_value_ref_skill_level_parameter` multiplies the payload value by a skill-rank coefficient. `target_missing_hp_ratio_coefficient` or `target_missing_hp_ratio_skill_level_parameter` adds a per-target bonus calculated as `target_max_health * current_missing_health_ratio * coefficient`; its optional min/max clamp applies to that bonus. The ratio and maximum health are read for each target immediately before that target's damage request, so a primary hit and later area targets use their own current health. `damage_scale` or `damage_scale_skill_level_parameter` scales the composed result, followed by the optional `damage_min`/`damage_max` clamp. `NumericValueRef` scale, offset, clamp, required, and fallback policies apply to the payload value. Payload fields are valid only when the referenced event payload exposes that field; a missing field uses an explicitly configured fallback when allowed, otherwise resolution fails and the action is rejected.

Approved optional `target_missing_hp_damage_scale_coefficient` and `target_missing_hp_damage_scale_max_bonus` are paired finite non-negative constants (omitted = disabled). After all additive terms and fixed `damage_scale`, multiply by `1 + min(maxBonus, coefficient * clamp(1 - hp/maxHp, 0, 1))`, then apply final `damage_min/max`. Read each target's current health at impact, without additional state or launch sampling.

`add_buff` accepts optional mutually exclusive `duration_ms` (non-negative integer) or `duration_skill_level_parameter` (nonempty name). The latter requires a valid original Cast and reads its frozen level parameter; it never reads the target/owner's current skill rank or stores Modifier parameters on Buff. Pass the sampled duration to the existing Buff duration override; zero retains the existing configured-duration convention. Missing/invalid Cast parameters reject the action. Existing Buff timing, refresh, control immunity and snapshots remain authoritative.


`adjust_damage_number` also accepts the existing optional `magnitude_*` formula inputs. They are mutually exclusive with `value` (and its aliases), repeat-target decay, and missing-health inputs. Evaluation uses the current combat execution context and the formula's source role; `Target` reads the current damage recipient's attributes on each hit. This does not freeze attributes into a Buff or add snapshot fields. Omitted magnitude inputs retain the existing fixed/decay/missing-health behavior. The approved scope and real damage verification are recorded in [W damage reduction magnitude](../plans/GragasDamageReductionMagnitudeDecision.2026-11-12.md).

`give_damage` also accepts `projectile_impact_kill_bonus`, a constant in `[0, 1]` (default `0`). A positive value requires an immediate `projectile.hit` action against the hit target, with a live projectile and matching launch context. The action scales the composed request by `1 + bonus * prior confirmed impact kills` before the damage pipeline. Only a committed alive-to-dead HP transition from that damage operation counts, at most once per accepted hit. The current hit never benefits from its own kill. External or delayed deaths do not count, and bounce selection is unchanged. The count and last killing hit belong to the active projectile link and are restored by its frame rollback provider; neither is a StateSync presentation field.

## Source Buff claims and original Cast shield groups

The [approved Lux proposal](../plans/LuxImplementationReadiness.2026-11-13.md) adds `source_buff`. Its `buff_id` and normal target request select a target Buff owned by the current caster. Default behavior refreshes through the normal Buff application policy; `claim=true` selects one unambiguous source instance and removes exactly its runtime identity and revision before removal callbacks run. Stale or repeated claims fail. `result` and `eligible_result` are optional numeric Blackboard outputs. Enemy/search/damage eligibility is required, including when a shield absorbs all HP damage. Buff runtime identities use the existing lifecycle recovery format.

An optional `magnitude_*` formula is sampled before the claim's removal callbacks and written to `magnitude_result` on success. `magnitude_level_source=1` selects the current source hero level; default 0 preserves current skill-rank behavior. Level curves use paired level/value points. Temporary claim results belong to the current synchronous Effect scope, not a persistent target map.

Shield stacking policy `MergeSameShieldAndCast` (4) matches shield ID, source actor and nonzero original `RootContextId`. Reapplication adds to the remaining shield and refreshes its expiry; separate casts and separate casters remain separate layers. The existing shield recovery carries this grouping key.

`shoot_projectile.use_aim_distance` defaults to true. False uses the authored full outbound distance while retaining the submitted direction. This supports directional missiles whose nearby pointer target must not shorten the route.

## Projectile, Area and Motion composition acceptance

A projectile arrival Trigger may sequence query damage, Motion and Area creation as sibling actions. An Area created at arrival is not automatically the damage or displacement authority: inspect its authored effects. The published Gragas R arrival uses direct query damage and radial Pull; its Area is an aftermath presentation object. No hero identity branch is required by this composition.

The current simulation runs Motion in Execute, then ProjectileSync and AreaSync in PostExecute. An arrival that adds a Motion source therefore settles damage on the arrival frame while positions remain unchanged until the next Motion tick. Tests must assert that boundary as well as the eventual displacement and single settlement.

For shared Area group claims, enemy and configured UnitSubType qualification precede the original-Cast/group/target claim. Rejected contacts do not consume a later eligible contact. A successful claim survives repeated physical Enter/Cross contacts; a later qualification change does not revoke it. Verify the claim before and after restoration using actual Motion-driven crossings, Buff state and numeric projection, rather than only teleporting or calling the claim API.

Recovery evidence must distinguish same-frame snapshot roundtrip, replaying the same movement command from a checkpoint before that command, and rewinding an already active Motion source. For the approved first slice, joint lifecycle v4 stores exact raw time and frozen values for FixedDelta sources owned by the same surviving Motion Continuous and restores them silently. Other source types, destroyed instances, external Pipeline sources and cross-roster restore remain unsupported. A full Gragas R replay still exposes passive interval reconciliation after restored SkillLoadout notifications; this is a separate pending contract, not evidence that Motion state failed to restore. See the `local/docs/Moba受管位移精确恢复验收.2026-11-17.md`, `local/docs/Moba投射物区域位移组合验收.2026-11-17.md`, and [passive rebind decision](../plans/MobaPassiveContinuousRollbackRebindDecision.2026-11-17.md).

## Publish

`spawn_area`, `spawn_area_batch` and its `spawn_area_pattern` alias accept optional
`claim_unit_sub_type_mask`. Bit n-1 selects positive `UnitSubType` n; only defined
subtypes are valid. Zero preserves the legacy Hero-only group claim. The value is
frozen on each Area and restored with its lifecycle checkpoint (older payloads
default to zero). Enemy and subtype eligibility precede the existing shared
original-Cast/group/target claim for Enter/Cross; no extra claim table is created.
This parameter has no effect when `claim_targets_within_group` is false. See
the approved [Area qualification decision](../plans/MobaAreaGroupQualificationDecision.2026-11-14.md).

1. Edit and save the production modules in **Window > AbilityKit > Trigger Authoring Workspace**. If editing a maintained `Sources/*.trigger.json` file, first run `AbilityKit.Ability.Editor.Utilities.TriggerAuthoringMobaMigration.ImportProductionSourcesBatch` to import all four production Sources into their modules. If editing modules directly, export their Sources from the workspace before publishing.
2. Run **Tools > AbilityKit > MOBA > Publish Trigger Plans**, or Unity batch method `AbilityKit.Ability.Editor.Utilities.TriggerAuthoringMobaMigration.PublishRuntimeBatch`. Stop if import or validation failed; do not treat prior runtime artifacts as the result of the current change.
3. Run `AbilityKit.Ability.Editor.Utilities.TriggerAuthoringMobaMigration.CheckRuntimeBatch` to detect unpublished module changes and confirm Unity/Console parity.
4. For each batch command, inspect its log and confirm the requested method ran and Unity exited with `Exiting batchmode successfully` and return code 0. A project-lock conflict, validation error, or method exception is a failed step even if the process launcher returned or an artifact timestamp changed.
5. Run `python LubanConfig/Moba/export_pipeline.py --check-published`, then the six-hero .NET smoke tests, runtime validation, and Unity EditMode acceptance tests. Confirm both generated runtime files match the production modules and the startup validator reports no blocking `invalid_trigger_scope` errors.

`GenerateBatch` is only for the initial migration. It overwrites the four production modules from the archived split JSON files; do not use it for routine publication.

The runtime loads the editor aggregate once. Unity also loads `ability/rules`; Console loads `luban/ability/rules`. The archived `ability/triggers` directory is not in either load profile.
# Approved CastFrame capture

`spawn_area` supports optional `capture_cast_frame` (default false), approved in `plans/LuxCastPresentationFrameDecision.2026-11-13.md`. It requires a single position without an explicit target request and a live original Cast handle. It records the resolved pre-offset origin and normalized horizontal pre-shape-yaw direction once in that Cast's rollback blackboard. It neither rotates the actor nor changes the area geometry or damage timing. The formal CharacterAction v3 presentation projection carries this optional frame to explicitly configured CastFrame templates.
