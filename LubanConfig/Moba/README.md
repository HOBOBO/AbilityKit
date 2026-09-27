# MOBA Luban 配置管线

`Production/Datas/*.xlsx` 是 MOBA 配置的主数据源。Unity package 下的 `Resources/moba`、`Resources/ability`、`Resources/luban`，以及 Console 的 `Configs/luban` 都由导出管线发布，不应直接编辑。

## 日常流程

1. 编辑 `Production/Datas/*.xlsx`。前两行是 Luban 字段名和类型，数据从第四行开始。
2. 运行 `python LubanConfig/Moba/export_pipeline.py` 验证表、ID、跨表引用和 Ability JSON 节点。
3. 运行 `python LubanConfig/Moba/export_pipeline.py --apply` 发布 JSON、`.bytes` 和 C# 类型到 Unity 与 Console。
4. 运行 `python LubanConfig/Moba/export_pipeline.py --check-published` 检查发布结果与 Excel 一致。
5. 运行六英雄 .NET 冒烟测试；正式发布前再运行 Unity EditMode 测试。

```powershell
dotnet test src/AbilityKit.Demo.Moba.Tests/AbilityKit.Demo.Moba.Tests.csproj --filter "FullyQualifiedName~BattleFlowRealSkillTests.Luban_six_heroes_spawn_and_cast_first_skill|FullyQualifiedName~MobaLubanConfigPipelineTests"
powershell -ExecutionPolicy Bypass -File tools/run-unity-editmode-tests.ps1 -TestAssembly AbilityKit.Game.UnitTests -TestFilter MobaProductionConfigReferenceValidationTests
```

`--check-baseline` 只用于一次性迁移验收。它逐项比较 Excel 重建的资源与原始 JSON；聚合 `ability_trigger_plans.json` 由逐条触发源重新编译，原聚合曾与这些源存在内容偏差，因此不作为迁移基线。PR 和 push 的 `moba-luban-config` CI job 会运行 `--check-published`。

## 表与加载

26 张运行时注册表和 `effects`、`brains` 各有独立工作簿。`MobaLubanConfigGroups.Create(loader, new[] { "characters" })` 可以只将 `characters` 切换到二进制；默认加载已经让全部运行时注册表走 Luban `.bytes`。效果配置和脑配置也优先读取二进制，在没有二进制的自定义资源环境下保留 JSON 兼容。

其余 9 份 MOBA JSON 已各自升为 Luban 表。对象根、数组根和迁移时添加的 `RowId` 由 `table_roots.json` 记录，发布时还原原始 JSON 形状，保持现有消费者可用。这些表同时生成 Luban JSON、`.bytes` 和 C# 类型。

96 份 Ability JSON 存于 `ability_nodes.xlsx`，每行以 `Path` 和 RFC 6901 `Pointer` 定位一个 JSON 节点；`Kind` 指定容器或 JSON 值。`ValueJson` 的 `json:` 前缀用于保留空字符串等值，不应删除。`ability/ability_trigger_plans.json` 不单独编辑，导出时由 87 份 `ability/triggers` 文件编译。修改逐条触发配置后重新发布即可同步聚合。

`resource_documents.xlsx` 已归档到 `local/moba-config-migration/`，不参与导出。旧 SO/JSON 导出命令不再是发布入口；英雄向导只创建草稿 SO。Console 原 `Configs/moba` 保留为旧副本，正式入口读取 `Configs/luban`。
