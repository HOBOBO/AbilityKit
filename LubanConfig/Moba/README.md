# MOBA Luban 配置管线

## 目录约定

`Production/` 保存权威 Excel、Luban 定义和导出规则清单；顶层 `export_pipeline.py` 与 `export_moba_configs.ps1` 是生产入口，其他顶层 Python 模块由导出器调用。`business/` 按业务领域放投影与校验规则，`tests/` 放管线测试，`tools/` 放可重复使用的工作簿维护与影响分析工具。`migrations/` 只归档一次性改表脚本；它们会修改工作簿，不属于日常导出流程。`MiniTemplate/` 是历史模板。

从仓库根目录运行测试、维护工具和生产导出：

```powershell
python -m unittest discover -s LubanConfig/Moba -p "test_*.py"
python LubanConfig/Moba/tools/annotate_workbooks.py
python LubanConfig/Moba/tools/modifier_impact_report.py --help
python LubanConfig/Moba/export_pipeline.py --check-published
```

## 导出规则扩展

生产入口是 `export_pipeline.py`；`export_moba_configs.ps1` 仅转发到该入口，不再读取 `MiniTemplate` 或写入旧的 `.generated` 目录。`Production/export_rules.json` 按顺序登记业务模块，通用调度器 `pipeline_rules.py` 只负责加载、分阶段执行和检查生成代码冲突。业务规则位于 `business/`，按角色、技能、Buff、投射物、召唤物、表现、场景、标签和修饰器拆分。

新增或替换业务规则时，在 `business/` 增加一个提供 `Rule` 类的模块，并在 `export_rules.json` 登记其导入名；需要先执行的规则写在该项的 `after` 列表。可实现 `normalize(table, row)`、`project(context)`、`transform(context)`、`validate(context)`、`generate(context)`、`references()` 和 `nested_relations()`。`project` 负责兼容运行时 DTO 的业务投影，`transform` 负责编译衍生表；`generate` 返回 `{C#文件名: 源码}`；`references` 返回 `(字段到目标表的映射, 必填字段集合)`，列表内关系用 `nested_relations` 返回 `ReferenceSpec`。生成文件和引用字段都只能有一个业务所有者，生成代码不能覆盖 Luban 原生代码。字段结构仍由 `Production/Defines` 和 Excel 管理。

导出会先核对注册表与 Luban JSON、`.bytes` 的表全集，再执行投影、标签规则编译、ABI 与跨来源引用校验。`local/luban-export/reference-report.json` 列出正向引用和按目标 ID 汇总的使用方；`publication-report.json` 记录 Excel/定义/规则清单及全部预期产物的 SHA-256 和本次差异。`tag-usage-report.json` 提供授予源、规则、门禁及 `tagImpact` 反向影响；`--impact-tag State.Stunned` 可按标签查看。标签运行时使用的 7 张标签表及 Buff、持续流程、技能二进制汇成 `luban/moba/tag_bundle_manifest.json`，Unity 与 Console 各发布一份，运行时逐表验证 SHA-256。`--check-published` 与 `--apply` 使用同一份发布清单；发布前检查 Unity/Console 的 Flow、Trigger 发布副本及状态机源与副本，逐文件写入失败时恢复已变更文件。Pipeline 和 Trigger 的 Unity 源资产仍需用各自的编辑器 `Check` 验证，Python 只校验已发布副本。

新规则应补独立测试，并运行上述测试与发布一致性检查。一次性迁移脚本和 `MiniTemplate` 不参与生产导出。

修饰器参数目录与跨领域扩展契约见 [MobaModifierAuthoring](../../Docs/MobaModifierAuthoring.md)。`J-效果表.xlsx` 的 `修饰参数` 子表是参数 ID、类型、操作和触发器可写权限的唯一编辑源；运行时键由导表生成。

状态机图已迁至 `moba.view.runtime/Configs/MobaStateMachineAuthoring/`，旧 `effect_plans` 已退出生产管线。角色 HFSM 和投射物状态机通过 **MOBA 状态机编辑器** 发布；触发器条件与动作通过 Trigger Authoring 发布。Excel 继续维护 Buff、持续流程、投射物等参数表；技能伤害由 Trigger Authoring 的动作参数配置。编辑与校验命令见 [MobaStateMachineAuthoring](../../Docs/MobaStateMachineAuthoring.md)。

`Production/Datas/*.xlsx` 是 MOBA 数值、资源和 ID 映射表的主数据源，包括模型、技能按钮表现、特效资源及特效表现模板。技能编排由 `SkillFlowCO.asset` 和 ActionEditor 创作；触发器效果由 Trigger Authoring 模块创作；角色 HFSM 与投射物状态机由 MOBA 状态机编辑器创作。各自使用专用导出器发布，详见 [MobaSkillPipeline](../../Docs/MobaSkillPipeline.md)、[MobaTriggerAuthoring](../../Docs/MobaTriggerAuthoring.md) 和 [MobaStateMachineAuthoring](../../Docs/MobaStateMachineAuthoring.md)。Unity package 下的 `Resources/moba`、`Resources/vfx`、`Resources/ability`、`Resources/luban`，以及 Console 配置都由对应导出管线发布，不应直接编辑。

`Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/Moba/Excel/` 中的旧 Excel 副本不参与当前 Luban 导表；正式主源仍是本目录下的 `Production/Datas/`。Unity `Assets` 下没有 Luban 生产表副本。

## 强类型字段
`J-效果表.xlsx` 的 `修饰器定义` 子 sheet 是 Buff、持续流程修饰器引用的主源。Buff/持续流程通过有序 `ModifierIds` 引用修饰器，并通过 `TagIds` 引用 `J-标签表.xlsx` 中的标签字典。标签规则组、逐行规则节点、持续模板以及结果 flag 映射统一由 `J-标签表.xlsx` 管理，导出二进制、JSON 和结果枚举；详见 [MobaTagAuthoring](../../Docs/MobaTagAuthoring.md)。持续模板 ID 保留，日常只编辑工作簿。

无战斗消费路径的普通 `标签模板`、`演示物品` 和 `旧属性模板` 已退出生产导表；`Ability节点` 中由 Trigger Authoring 接管的旧触发器分文件行也已清除。非触发器 Ability 文档仍由 `Ability节点` 发布。

`效果`、`旧Buff`、`持续效果` 子 sheet 已因无战斗消费路径退出生产导表。旧 `effects` 的伤害数值不会影响技能；伤害动作参数在 Trigger Authoring 中维护。

角色的 `SkillIds`、`PassiveSkillIds` 只在 `J-角色表.xlsx` 的 `角色` 子 sheet 编辑。属性模板只保存数值和普通攻击 ID；导出器从角色表补齐旧 DTO 需要的 `ActiveSkills`、`PassiveSkills`。修饰器定义允许多个 Buff 共用同一个 ID，修改共用定义会影响所有引用它的 Buff。标签定义的 `Namespace` 为 `GameplayState`（`State.*` 和 `Debuff.*`）或 `SkillSelection`（按需配置的 AI 选技分类）；技能表的 `Tags` 默认留空，有筛选需求时才引用 `SkillSelection`。召唤物和玩法表的旧 `Tags` 列也必须留空。标签的 ID 分段、点分层级、预置目录及退休 ID 见 [MobaTagAuthoring](../../Docs/MobaTagAuthoring.md)。`migrations/refine_config_ownership.py`、`migrations/normalize_tag_definitions.py` 和 `migrations/plan_tag_catalog.py` 记录一次性整理，日常改表无需运行。

`J-角色表.xlsx` 的 `属性类型` 子 sheet 按属性逐行定义 `Id/Key/Name/ValueKind/DefaultValue`，ID 与 `BattleAttributeType` 的 1–23 号对应。`ValueKind` 使用 `Flat` 或 `Percent` 枚举；它表示属性值的类型，尚未改变运行时修饰器叠加和公式计算。原表中“战士/法师”两行是横向属性预设，不是属性类型定义；英雄的具体数值继续由 `属性模板` 子 sheet 编辑。属性类型表同时发布到 Luban 与旧 `moba/attr_types.json` 加载路径。新增属性时先扩展运行时枚举和属性定义，再扩展模板与计算规则。`migrations/migrate_attribute_types.py` 是一次性迁移脚本，日常无需运行。

导出时会检查角色、技能、等级、Buff、持续流程、投射物、区域、召唤、特效、运动组、战斗开局与 Pipeline/Trigger Authoring 产物的 ID 引用。`emitters.TemplateId` 引用特效表现模板。


`Production/Defines/strong_types.xml` 定义了战斗配置的 Luban bean 和 enum。技能等级、Buff/持续流程修饰器、组件操作、目标查询、地图和战斗开局的嵌套对象已在生产表中展开；技能类型、技能按钮瞄准、投射物命中策略和标签行为门禁等有限整数列也使用 enum。`complex_columns.json` 只登记仍按 JSON 文本解析的历史图结构与触发器迁移字段。枚举在 Excel 中填写名称，Luban 输出原有数值；标签行为门禁直接使用 Luban 生成的 `MobaTagBehaviorGate`，工作簿不再重复维护枚举名称。

bean 的 `*字段名` 是列表：第一、二行的字段名与类型横跨成员列并合并，第三行填写成员名，一项占一行，同一主表记录的 ID 只在第一行填写。例如技能等级的 `CooldownMs`、`Cost`、`Params` 各有独立列。目标查询规则与评分器的可选成员留空即可，导出时不会生成该键。地图向量采用 `X,Y,Z` 的 Luban `MapVector3` 值。地图碰撞形状、投射物响应及战斗开局模式在 Excel 中用 enum 名称填写；发布边界会转换回现有 JSON 字符串。地图 `ObjectType` 是可扩展分类，仍使用字符串。

`migrations/migrate_strong_types.py` 记录这次一次性迁移步骤，日常改表无需运行。调整 bean/enum 定义时，先用 `python LubanConfig/Moba/export_pipeline.py --check-baseline` 对照运行时 JSON，再发布并检查二进制加载；不要编辑生成的 C# 或发布副本。

## 日常流程

1. 编辑 `Production/Datas/` 中对应的 `J-`、`M-` 或 `T-` 工作簿及其中文子 sheet。前两行是 Luban 字段名和类型，数据从第四行开始；不要改动字段名、类型和 `__tables__.xlsx` 中的英文输出名。
2. 先发布 ActionEditor、Pipeline、Trigger Authoring；状态机有改动时先发布状态机。运行各编辑器的 `Check`，其中触发器使用 `TriggerAuthoringMobaMigration.CheckRuntimeBatch`。
3. 运行 `python LubanConfig/Moba/export_pipeline.py` 验证表、ID、跨表引用和 Ability JSON 节点，并审阅 `local/luban-export/` 下的引用与发布报告。
4. 运行 `python LubanConfig/Moba/export_pipeline.py --apply` 发布 JSON、`.bytes` 和 C# 类型到 Unity 与 Console。
5. 运行 `python LubanConfig/Moba/export_pipeline.py --check-published` 检查发布结果。
6. 运行六英雄 .NET 冒烟测试；正式发布前再运行 Unity EditMode 测试。

```powershell
dotnet test src/AbilityKit.Demo.Moba.Tests/AbilityKit.Demo.Moba.Tests.csproj --filter "FullyQualifiedName~BattleScenarioRealSkillTests.Luban_six_heroes_spawn_and_cast_first_skill|FullyQualifiedName~MobaLubanConfigPipelineTests"
powershell -ExecutionPolicy Bypass -File tools/run-unity-editmode-tests.ps1 -TestAssembly AbilityKit.Game.UnitTests -TestFilter MobaProductionConfigReferenceValidationTests
```

`--check-baseline` 只用于一次性迁移验收。它逐项比较 Excel 重建的非触发器资源与原始 JSON。`ability_trigger_plans.json` 由 Trigger Authoring 发布，Luban 不再覆盖它。PR 和 push 的 `moba-luban-config` CI job 会运行 `--check-published`。

## 表与加载

`Production/luban.conf` 指定 `dataDir: "Datas"`，并通过 `Datas/__tables__.xlsx` 登记生产表；这里是当前唯一生效的 Luban Excel 目录。登记表按逻辑和表现职责分组到下列工作簿，表定义中的 `input` 使用 `子sheet名@工作簿名.xlsx`。中文工作簿名和子 sheet 名只用于编辑定位，英文表名、字段名以及导出的 JSON、`.bytes` 和 C# 类型保持不变。

| 工作簿 | 内容 |
|---|---|
| `J-角色表.xlsx` | 角色、属性、AI、逻辑状态机 |
| `M-模型表.xlsx` | 模型资源、角色动画动作 |
| `J-技能表.xlsx` | 主动/被动技能、等级、目标查询 |
| `J-技能表现表.xlsx` | 技能按钮和瞄准表现；主动技能的特效时序仍由 ActionEditor 编辑 |
| `T-特效表.xlsx` | 特效资源路径与时长、特效表现模板中的挂点、跟随、偏移、缩放和颜色 |
| `J-效果表.xlsx` | Buff、持续流程、运动、组件、修饰器定义 |
| `J-标签表.xlsx` | 标签定义、规则组/节点、持续模板、结果标志与映射 |
| `J-投射物表.xlsx` | 投射发射器、投射物、区域、发射器 |
| `J-召唤物表.xlsx` | 召唤物、召唤属性继承、召唤动作模板 |
| `J-战斗场景表.xlsx` | 地图、开局、玩法 |
| `J-资源节点表.xlsx` | 仍需发布的非触发器 Ability 文档节点 |

新增表时，在对应工作簿新增子 sheet，并在 `__tables__.xlsx` 登记其 `input`；字段名和输出名仍按现有运行时契约命名。`repartition_workbooks.py` 记录了本次分组迁移，日常编辑无需运行。未登记的旧 `skill_flows.xlsx` 已移到 `Production/Archive/`，仅供迁移留档。

`T-特效表.xlsx` 的 `特效资源` 子 sheet 是 `vfx` 的主源，发布时生成两端的 Luban JSON、`.bytes` 和 Unity 使用的 `Resources/vfx/vfx.json`。`特效表现模板` 子 sheet 保存复用特效的挂点、跟随与外观参数；其 `AssetId` 引用特效资源 ID。旧 `VfxTable.asset` 和 `VfxJsonExporter` 不再发布生产特效配置。

`MobaLubanConfigGroups.Create(loader, new[] { "characters" })` 可以只将 `characters` 切换到二进制，其余表默认读取同次导出的 Luban JSON；完整生产加载让其他运行时表（包括持续标签模板）走 Luban `.bytes`，`skill_flows` 固定读取 Pipeline 发布的 `moba/skill_flows.json`。显式传入 `useLubanJsonForRemaining: false` 才使用旧 JSON 路径，该路径保留迁移基线，不能作为当前 Excel 修改后的生产数据来源。标签规则和结果映射从二进制编译，标签变化后重算缓存结果位；不会为普通标记标签生成代码枚举。脑配置优先读取二进制，在没有二进制的自定义资源环境下保留 JSON 兼容。

其余 9 份 MOBA JSON 已各自升为 Luban 表。对象根、数组根和迁移时添加的 `RowId` 由 `table_roots.json` 记录，发布时还原原始 JSON 形状，保持现有消费者可用。这些表同时生成 Luban JSON、`.bytes` 和 C# 类型。

`J-资源节点表.xlsx` 的 `Ability节点` 子 sheet 仍保存 96 份迁移时的 Ability JSON 节点，其中 87 份旧 `ability/triggers` 文件只作历史基线；Luban 不再发布它们。其余 Ability 文档仍按 `Path` 和 RFC 6901 `Pointer` 重建。运行时触发器从 Trigger Authoring 项目发布的 `ability/ability_trigger_plans.json` 加载，Unity 和 Console 均不加载旧分文件目录。

`resource_documents.xlsx` 已归档到 `local/moba-config-migration/`，不参与导出。通用 SO/JSON 导出命令不再是发布入口；英雄向导只创建草稿 SO。Console 正式入口读取 `Configs/luban`，其中 Flow 从 Pipeline 发布的 `Configs/moba/skill_flows.json` 读取。
