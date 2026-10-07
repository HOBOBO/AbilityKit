# MOBA Trigger Authoring

## Ownership

- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaTriggerAuthoring/Packages/` contains the four production Trigger Authoring modules: skills, buffs, passives, and gameplay.
- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaTriggerAuthoring/Sources/` contains their editable source snapshots. Keep each Source document synchronized through the Trigger Authoring workspace.
- The related skill flow showcases live in `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaSkillFlowAuthoring/`.
- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/ability/ability_trigger_plans.json` and `src/AbilityKit.Demo.Moba.Console/Configs/luban/ability/ability_trigger_plans.json` are generated runtime artifacts.
- The `Ability节点` sheet in `LubanConfig/Moba/Production/Datas/J-资源节点表.xlsx` and `Resources/ability/triggers/` retain the migration baseline. They no longer publish or load production trigger effects. The two old tutorial references without config rows (Buff 100001 and query 101) map to the existing demo Buff 1 and range query 1 during the one-time migration.
- The empty passive entry 10060000 is authored as a debug-only marker because the Mozi passive table references it.

## Publish

1. Edit and save the production modules in **Window > AbilityKit > Trigger Authoring Workspace**.
2. Run **Tools > AbilityKit > MOBA > Publish Trigger Plans**, or Unity batch method `AbilityKit.Ability.Editor.Utilities.TriggerAuthoringMobaMigration.PublishRuntimeBatch`.
3. Run `AbilityKit.Ability.Editor.Utilities.TriggerAuthoringMobaMigration.CheckRuntimeBatch` to detect unpublished module changes.
4. Run `python LubanConfig/Moba/export_pipeline.py --check-published`, then the six-hero .NET smoke tests and Unity EditMode acceptance tests.

`GenerateBatch` is only for the initial migration. It overwrites the four production modules from the archived split JSON files; do not use it for routine publication.

The runtime loads the editor aggregate once. Unity also loads `ability/rules`; Console loads `luban/ability/rules`. The archived `ability/triggers` directory is not in either load profile.
