# MOBA Skill Pipeline Publishing

## Sources

- The `技能` sheet in `LubanConfig/Moba/Production/Datas/J-技能表.xlsx` owns skill metadata and flow IDs.
- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/Moba/SkillFlowCO.asset` owns all 26 active skill pipelines, including LianPo's three-stage ultimate.
- `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaSkillFlowAuthoring/Showcases/` holds the P1/P2 skill flow showcase sources and assets.
- ActionEditor `Resources/moba/action_timeline/skill_*.json` files own authored action tracks. A timeline phase may reference only its exported `*.moba.logic.json`; presentation clips stay in `*.moba.presentation.json`.
- `Production/Archive/skill_flows.xlsx` is migration history. It is no longer registered in `__tables__.xlsx` or used for publishing.
- Trigger effects are authored in the Trigger Authoring modules and published separately; see [MobaTriggerAuthoring](MobaTriggerAuthoring.md).

## Publish Order

1. Edit the ActionEditor source timeline, then save in ActionEditor or run `NBC.ActionEditor.LogicJsonExporter.ExportAllMobaTimelines` in Unity batch mode. Unsupported authoritative clips fail when the pipeline compiles the logic export.
2. Edit the phase tree in `SkillFlowCO.asset`. Run `AbilityKit.Ability.Impl.BattleDemo.Moba.Editor.MobaSkillPipelineExporter.Apply` in Unity batch mode, or use **Tools > AbilityKit > Demos > Moba > Pipeline > Publish Flows**. This validates IDs, phase trees and skill references, then publishes aggregate and per-flow JSON plus the Console copy.
3. Edit other tables in Luban workbooks. Run `python LubanConfig/Moba/export_pipeline.py --apply`. Luban validates flow references against the Pipeline JSON and never publishes `skill_flows`.
4. Run `python LubanConfig/Moba/export_pipeline.py --check-published` and `MobaSkillPipelineExporter.Check` in Unity batch mode. Both commands must pass before release.
5. Run `dotnet test src/AbilityKit.Demo.Moba.Tests/AbilityKit.Demo.Moba.Tests.csproj --filter "FullyQualifiedName~MobaLubanConfigPipelineTests|FullyQualifiedName~BattleFlowRealSkillTests"` and the Unity EditMode hero acceptance tests.

For Unity batch commands, use the local Unity 2022.3 editor with `-batchmode -nographics -projectPath <repo>/Unity -executeMethod <method> -quit -logFile <log>`. For `-runTests`, omit `-quit` so the test runner can finish and write NUnit XML. The test runner in `tools/run-unity-editmode-tests.ps1` handles this.

The default runtime profile loads all other registered tables from Luban bytes and loads `skill_flows` from `Resources/moba/skill_flows.json`. The Console uses its published `Configs/moba/skill_flows.json`. Keep flow DTO registration in place; it is the runtime contract even though Luban no longer generates a Flow table.

## Runtime Rollback Contract

The Pipeline package exposes run phase instances, execution position, and lifecycle calls without owning a rollback format or policy. The MOBA skill layer stores the versioned phase tree and each active skill runner's stage, context, policy, cast sequence cursor, and run bytes. Restore rebuilds phase instances from `IMobaSkillPipelineLibrary`, rebinds event subscriptions and pipeline continuous state, and does not execute pipeline start actions again. The skill runtime provider restores its Blackboard and skill-phase execution-context lifecycle before the runner provider imports.

These snapshots support **in-process prediction rollback**. The Triggering package exposes scheduler entry progress and lifecycle operations; the MOBA trigger runtime owns payload validation, entry selection, ID cursor restore, and the retention window for executable definitions. New snapshots contain waiting entries only. Finished definitions remain available until the MOBA receives a confirmed frame beyond the configured rollback history, then the MOBA service releases them. Preflight requires the saved executable closures to remain available. Execution-context lifecycle restore also requires the original context identities. A fresh process cannot use these local rollback payloads as a reconnect or full-world recovery snapshot.

Keep skill Flow, timeline and trigger definitions fixed for the duration of a rollback history window. The MOBA run codec checks version, config ID/name, phase IDs/types and bounds, but it does not fingerprint all authored timing and effect parameters. `SkillPipelineContext.SharedData` accepts only the codec's primitive/value types; capture fails for an unsupported object so a partial state is never recorded as a valid snapshot.
