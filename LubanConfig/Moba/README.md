# MOBA Luban 配置管线

除主动技能编排和触发器效果外，`Production/Datas/*.xlsx` 是 MOBA 配置的主数据源。技能编排由 `SkillFlowCO.asset` 和 ActionEditor 创作；触发器效果由 Trigger Authoring 模块创作。两者分别使用专用导出器发布，详见 [MobaSkillPipeline](../../Docs/MobaSkillPipeline.md) 和 [MobaTriggerAuthoring](../../Docs/MobaTriggerAuthoring.md)。Unity package 下的 `Resources/moba`、`Resources/ability`、`Resources/luban`，以及 Console 配置都由对应导出管线发布，不应直接编辑。

`Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/Moba/Excel/` 中的旧 Excel 副本不参与当前 Luban 导表；正式主源仍是本目录下的 `Production/Datas/`。Unity `Assets` 下没有 Luban 生产表副本。

## 日常流程

1. 编辑已登记的 `Production/Datas/*.xlsx`。前两行是 Luban 字段名和类型，数据从第四行开始。旧 `skill_flows.xlsx` 未登记，仅供迁移留档。
2. 运行 `python LubanConfig/Moba/export_pipeline.py` 验证表、ID、跨表引用和 Ability JSON 节点。
3. 运行 `python LubanConfig/Moba/export_pipeline.py --apply` 发布 JSON、`.bytes` 和 C# 类型到 Unity 与 Console。
4. 在 Trigger Authoring 工作台编辑触发器，运行 `TriggerAuthoringMobaMigration.PublishRuntimeBatch` 发布两端聚合文件。
5. 运行 `python LubanConfig/Moba/export_pipeline.py --check-published` 和 `TriggerAuthoringMobaMigration.CheckRuntimeBatch` 检查发布结果。
6. 运行六英雄 .NET 冒烟测试；正式发布前再运行 Unity EditMode 测试。

```powershell
dotnet test src/AbilityKit.Demo.Moba.Tests/AbilityKit.Demo.Moba.Tests.csproj --filter "FullyQualifiedName~BattleFlowRealSkillTests.Luban_six_heroes_spawn_and_cast_first_skill|FullyQualifiedName~MobaLubanConfigPipelineTests"
powershell -ExecutionPolicy Bypass -File tools/run-unity-editmode-tests.ps1 -TestAssembly AbilityKit.Game.UnitTests -TestFilter MobaProductionConfigReferenceValidationTests
```

`--check-baseline` 只用于一次性迁移验收。它逐项比较 Excel 重建的非触发器资源与原始 JSON。`ability_trigger_plans.json` 由 Trigger Authoring 发布，Luban 不再覆盖它。PR 和 push 的 `moba-luban-config` CI job 会运行 `--check-published`。

## 表与加载

运行时注册表中除 `skill_flows` 外的表和 `effects`、`brains` 各有独立工作簿。`MobaLubanConfigGroups.Create(loader, new[] { "characters" })` 可以只将 `characters` 切换到二进制；默认加载让其他运行时表走 Luban `.bytes`，`skill_flows` 固定读取 Pipeline 发布的 `moba/skill_flows.json`。效果配置和脑配置也优先读取二进制，在没有二进制的自定义资源环境下保留 JSON 兼容。

其余 9 份 MOBA JSON 已各自升为 Luban 表。对象根、数组根和迁移时添加的 `RowId` 由 `table_roots.json` 记录，发布时还原原始 JSON 形状，保持现有消费者可用。这些表同时生成 Luban JSON、`.bytes` 和 C# 类型。

`ability_nodes.xlsx` 仍保存 96 份迁移时的 Ability JSON 节点，其中 87 份旧 `ability/triggers` 文件只作历史基线；Luban 不再发布它们。其余 Ability 文档仍按 `Path` 和 RFC 6901 `Pointer` 重建。运行时触发器从 Trigger Authoring 项目发布的 `ability/ability_trigger_plans.json` 加载，Unity 和 Console 均不加载旧分文件目录。

`resource_documents.xlsx` 已归档到 `local/moba-config-migration/`，不参与导出。通用 SO/JSON 导出命令不再是发布入口；英雄向导只创建草稿 SO。Console 正式入口读取 `Configs/luban`，其中 Flow 从 Pipeline 发布的 `Configs/moba/skill_flows.json` 读取。
