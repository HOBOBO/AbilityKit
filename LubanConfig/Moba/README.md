# MOBA Luban 配置管线

> 文档类型：项目操作指南
> 事实基线：2026-11-25 本批复核导出命令、输入/产物归属、参数副作用与发布一致性；各领域规则细节按对应源码和领域规范维护。
> 核校状态：部分核校；本批 Python 187 项测试、只读发布一致性及最小 Runtime 示例通过，未重新执行 Unity authoring 发布、业务 Smoke 或正式 Apply。
>
> 跨包所有权和失败边界见 [CodeGen 与 Luban 生产链](../../Docs/design/05-CommonModules/07-CodeGenAndLubanProductionPipeline.md)；单包 API 见 [ConfigDatabase](../../Unity/Packages/com.abilitykit.ability/Documentation~/ConfigDatabase.md)、[CodeGen](../../Unity/Packages/com.abilitykit.demo.moba.codegen/Documentation~/Usage.md)、[Luban Runtime](../../Unity/Packages/com.abilitykit.thirdparty.luban.runtime/Documentation~/Usage.md)。本文保留为生产操作唯一入口，不另建第二套导表指南。

## 目录约定

`Production/` 保存权威 Excel、Luban 定义和导出规则清单；顶层 `export_pipeline.py` 与 `export_moba_configs.ps1` 是生产入口，其他顶层 Python 模块由导出器调用。`business/` 按业务领域放投影与校验规则，`tests/` 放管线测试，`tools/` 放可重复使用的工作簿维护与影响分析工具。`migrations/` 只归档一次性改表脚本；它们会修改工作簿，不属于日常导出流程。`MiniTemplate/` 是历史模板。

从仓库根目录运行只读核对，先显式创建本次日志目录：

```powershell
$run = Join-Path (Get-Location) ('local/Logs/moba-config/' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force $run | Out-Null
python -X utf8 -B -m unittest discover -s LubanConfig/Moba -p 'test_*.py' *> (Join-Path $run 'python-tests.log')
if ($LASTEXITCODE -ne 0) { throw 'Python tests failed; see python-tests.log' }
python -X utf8 -B LubanConfig/Moba/export_pipeline.py --check-published *> (Join-Path $run 'published-check.log')
if ($LASTEXITCODE -ne 0) { throw 'Published artifacts differ or generation failed; see published-check.log' }
```

`--check-published` 仍会重新导表并清理/写入本地 staging，只有正式产物不写入。原生默认 staging 与其中的引用/发布报告固定在 `local/luban-export`，没有命令行自定义 STAGE 参数；本批为隔离报告，在忽略的临时调用器内只将 STAGE 指到 `local/Logs/` 的本次目录，其余执行原管线。这个核校方式不成为第二个正式入口，也未改生产工具默认路径。

`tools/annotate_workbooks.py` 会维护工作簿注释，不属于只读预检；影响分析工具先看 `tools/modifier_impact_report.py --help`，按实际输出参数把运行报告放在 `local/Logs/`。不要把写入型维护工具与测试一起无差别执行。

## 导出规则扩展

生产入口是 `export_pipeline.py`；`export_moba_configs.ps1` 仅转发到该入口，不再读取 `MiniTemplate` 或写入旧的 `.generated` 目录。`Production/export_rules.json` 按顺序登记业务模块，通用调度器 `pipeline_rules.py` 只负责加载、分阶段执行和检查生成代码冲突。业务规则位于 `business/`，按角色、技能、Buff、投射物、召唤物、表现、场景、标签和修饰器拆分。

新增或替换业务规则时，在 `business/` 增加一个提供 `Rule` 类的模块，并在 `export_rules.json` 登记其导入名；需要先执行的规则写在该项的 `after` 列表。可实现 `normalize(table, row)`、`project(context)`、`transform(context)`、`validate(context)`、`generate(context)`、`references()` 和 `nested_relations()`。`project` 负责兼容运行时 DTO 的业务投影，`transform` 负责编译衍生表；`generate` 返回 `{C#文件名: 源码}`；`references` 返回 `(字段到目标表的映射, 必填字段集合)`，列表内关系用 `nested_relations` 返回 `ReferenceSpec`。生成文件和引用字段都只能有一个业务所有者，生成代码不能覆盖 Luban 原生代码。字段结构仍由 `Production/Defines` 和 Excel 管理。

导出会先核对注册表与 Luban JSON、`.bytes` 的表全集，再执行投影、标签规则编译、ABI 与跨来源引用校验。`local/luban-export/reference-report.json` 列出正向引用和按目标 ID 汇总的使用方；`publication-report.json` 记录 Excel/定义/规则清单及全部预期产物的 SHA-256 和本次差异。`tag-usage-report.json` 提供授予源、规则、门禁及 `tagImpact` 反向影响；`--impact-tag State.Stunned` 可按标签查看。标签运行时使用的 7 张标签表及 Buff、持续流程、技能二进制汇成 `luban/moba/tag_bundle_manifest.json`，Unity 与 Console 各发布一份，运行时逐表验证 SHA-256。`--check-published` 与 `--apply` 使用同一份发布清单；发布前检查 Unity/Console 的 Flow、Trigger 发布副本及状态机源与副本，逐文件写入失败时恢复已变更文件。Pipeline 和 Trigger 的 Unity 源资产仍需用各自的编辑器 `Check` 验证，Python 只校验已发布副本。

新规则应补独立测试，并运行上述测试与发布一致性检查。一次性迁移脚本和 `MiniTemplate` 不参与生产导出。

修饰器参数目录与跨领域扩展契约见 [MobaModifierAuthoring](../../Docs/MobaModifierAuthoring.md)。`J-效果表.xlsx` 的 `修饰参数` 子表是参数 ID、类型、操作和触发器可写权限的唯一编辑源；运行时键由导表生成。

状态机图已迁至 `moba.view.runtime/Configs/MobaStateMachineAuthoring/`，旧 `effect_plans` 已退出生产管线。角色 HFSM 和投射物状态机通过 **MOBA 状态机编辑器** 发布；触发器条件与动作通过 Trigger Authoring 发布。Excel 继续维护 Buff、持续流程、投射物等参数表；技能伤害由 Trigger Authoring 的动作参数配置。编辑与校验命令见 [MobaStateMachineAuthoring](../../Docs/MobaStateMachineAuthoring.md)。

`Production/Datas/*.xlsx` 是 MOBA 数值、资源和 ID 映射表的主数据源，包括模型、技能按钮表现、特效资源及特效表现模板。技能编排由 `SkillFlowCO.asset` 和 ActionEditor 创作；触发器效果由 Trigger Authoring 模块创作；角色 HFSM 与投射物状态机由 MOBA 状态机编辑器创作。各自使用专用导出器发布，详见 [MobaSkillPipeline](../../Docs/MobaSkillPipeline.md)、[MobaTriggerAuthoring](../../Docs/MobaTriggerAuthoring.md) 和 [MobaStateMachineAuthoring](../../Docs/MobaStateMachineAuthoring.md)。Unity package 下的 `Resources/moba`、`Resources/vfx`、`Resources/ability`、`Resources/luban`，以及 Console 配置都由对应导出管线发布，不应直接编辑。

`moba-tag-hud.xlsx` 中的 `TagHudPresentations` 是标签表现的唯一生产源。`ChannelMask` 使用 `1=HudStatus`、`2=HeadText`、`4=Vfx`；一个 Tag 可同时开启多个通道。它以游戏性状态事实 Tag 为键，不为单个英雄重复配置；启用 VFX 通道时必须填写已发布的 `VfxId`。

`Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/Moba/Excel/` 中的旧 Excel 副本不参与当前 Luban 导表；正式主源仍是本目录下的 `Production/Datas/`。Unity `Assets` 下没有 Luban 生产表副本。

## 强类型字段

### 头顶血条与多资源面板

`Production/Datas/moba-overhead-indicators.xlsx` 是唯一配置源：`indicator_definitions` 定义只读来源与有限转换，`overhead_layouts` 定义连续条、分段格和数字的行布局，`actor_overhead_bindings` 将内容绑定到布局。七英雄均有生命/蓝量/护盾；烬额外显示普攻或 R 弹药，安妮显示被动四格，维迦显示成长层数。后三类计数默认仅本地角色可见。护盾继续读取独立 ShieldState，不在工作簿创建第二份护盾状态。

后续适配英雄时，先复用 Resource、ActorCounter、有效 HFSM 参数、BuffStack/BuffPresent 来源，声明明确的上限、缺失策略和整数映射，再配置布局与内容绑定。缺失资源不能视为满值；数字可以不显示上限。分段必须有显式整数映射，共槽两行必须以同一 Buff 的相反存在条件互斥。发布器校验资源、Buff、正式 Trigger Counter 和绑定角色的 HFSM 参数，不支持任意脚本或英雄身份分支。超出现有来源/转换的机制仍需先按 MOBA 治理规范确认。

发布沿用 `export_pipeline.py --apply`，随后执行 `--check-published`；不要直接改 Unity/Console 的 JSON、bytes 或生成 C#。对应测试在 `tests/test_overhead.py`、`SevenHeroAlignmentRuntimeTests.Overhead.cs` 和 Unity `MobaOverheadHudTests`。

正式面板结构由 Unity 菜单 `AbilityKit/MOBA/Build Overhead HUD Prefab` 生成，批处理唯一入口为 `AbilityKit.Demo.Moba.Editor.MobaOverheadPrefabBuilder.Build`。该包内工具消费运行时强类型槽位，写入 `com.abilitykit.demo.moba.view.runtime/Resources/ui/battle/hud_overhead.prefab`；只在槽位结构变化时重建，普通数值/布局配置无需重建。批处理日志与测试结果显式输出到 `local/Logs/`。架构和当前验证范围见[头顶当前状态与多资源面板](../../Docs/design/09-ImplementationExamples/MOBA/04-SnapshotPresentationPrediction.md#18-头顶当前状态与多资源面板2026-11-24)。

`J-效果表.xlsx` 的 `修饰器定义` 子 sheet 是 Buff、持续流程修饰器引用的主源。Buff/持续流程通过有序 `ModifierIds` 引用修饰器，并通过 `TagIds` 引用 `J-标签表.xlsx` 中的标签字典。标签规则组、逐行规则节点、持续模板以及结果 flag 映射统一由 `J-标签表.xlsx` 管理，导出二进制、JSON 和结果枚举；详见 [MobaTagAuthoring](../../Docs/MobaTagAuthoring.md)。持续模板 ID 保留，日常只编辑工作簿。

无战斗消费路径的普通 `标签模板`、`演示物品` 和 `旧属性模板` 已退出生产导表；`Ability节点` 中由 Trigger Authoring 接管的旧触发器分文件行也已清除。非触发器 Ability 文档仍由 `Ability节点` 发布。

`效果`、`旧Buff`、`持续效果` 子 sheet 已因无战斗消费路径退出生产导表。旧 `effects` 的伤害数值不会影响技能；伤害动作参数在 Trigger Authoring 中维护。

角色的 `SkillIds`、`PassiveSkillIds` 只在 `J-角色表.xlsx` 的 `角色` 子 sheet 编辑。属性模板只保存数值和普通攻击 ID；导出器从角色表补齐旧 DTO 需要的 `ActiveSkills`、`PassiveSkills`。修饰器定义允许多个 Buff 共用同一个 ID，修改共用定义会影响所有引用它的 Buff。标签定义的 `Namespace` 为 `GameplayState`（`State.*` 和 `Debuff.*`）或 `SkillSelection`（按需配置的 AI 选技分类）；技能表的 `Tags` 默认留空，有筛选需求时才引用 `SkillSelection`。召唤物和玩法表的旧 `Tags` 列也必须留空。标签的 ID 分段、点分层级、预置目录及退休 ID 见 [MobaTagAuthoring](../../Docs/MobaTagAuthoring.md)。`migrations/refine_config_ownership.py`、`migrations/normalize_tag_definitions.py` 和 `migrations/plan_tag_catalog.py` 记录一次性整理，日常改表无需运行。

`J-角色表.xlsx` 的 `属性类型` 子 sheet 按属性逐行定义 `Id/Key/Name/ValueKind/DefaultValue`，ID 与 `BattleAttributeType` 的 1–23 号对应。`ValueKind` 使用 `Flat` 或 `Percent` 枚举；它表示属性值的类型，尚未改变运行时修饰器叠加和公式计算。原表中“战士/法师”两行是横向属性预设，不是属性类型定义；英雄的具体数值继续由 `属性模板` 子 sheet 编辑。属性类型表同时发布到 Luban 与旧 `moba/attr_types.json` 加载路径。新增属性时先扩展运行时枚举和属性定义，再扩展模板与计算规则。`migrations/migrate_attribute_types.py` 是一次性迁移脚本，日常无需运行。

`属性模板` 同时定义 1 级基础值和各属性每级成长值（`*Growth`）。英雄出生属性使用 `基础值 + 每级成长值 × (Level - 1)`，等级来自进入战斗的 `MobaPlayerLoadout.Level`；HP、Mana 的当前值和最大值使用同一等级计算结果初始化。成长字段为 0 时保持旧行为，适合逐英雄渐进填数。百分比属性的 Growth 单位沿用对应属性的现有配置单位。当前成长是出生时计算，不包含对局中的升级事件。

### 固定量资源自动回复

`moba-resource-regeneration.xlsx` 的「回复规则」子表发布 `resource_regeneration_rules`：`ResourceType` 显式绑定非 HP 资源，`FlatPerSecondAttribute` 引用每秒固定点数属性；每个资源只能有一条规则。首批唯一规则为 Mana(2) → `MANA_REGEN_PER_SECOND`(24)。回复量仍配置在 `J-角色表.xlsx` 的属性模板，字段为 float `ManaRegenPerSecond` / `ManaRegenPerSecondGrowth`，出生等级按基础值加成长计算。公开资料的每 5 秒回复量应先除以 5，保留小数；旧 `PerSecondManaR` / `PerSecondBloodR` 回复率字段不承担这项固定量语义。

世界 Execute 阶段在技能决策前执行一次 `min(Max, Current + max(0, EffectiveRate) × DeltaTime)`；动态 Max 读取实时有效属性，无 MaxAttribute 时只使用明确固定上限。死亡、未进入对局、世界暂停或缺资源时不回复，也不补时；移动、施法和引导照常回复。回复通过唯一 ResourceContainer 写入并刷新属性上下文，不走治疗事件。生命回复、百分比回复与脱战延迟不在本批。

后续 Energy/Rage 可由同一表绑定已有正式 Flat/Point 属性，无需按英雄增加执行分支；新速率属性仍须完整配置属性定义、模板/来源与初始化。没有规则不会自动回复，也不会创建资源。发布门禁拒绝 HP/None、未知或重复资源、当前值/上限作为速率、非固定点数属性以及非有限/负基础值或成长。使用现有 `export_pipeline.py --apply` 发布，再以 `--check-published` 核对两端产物。

技能等级表的 `ParamNames` 和各等级 `Params` 按同一列序配对。技能 Trigger Action 的数值引用可使用 `payload:skill.param.<名称>`（例如 `payload:skill.param.damage`）；运行时按本次技能等级读取参数，未定义的名称或该行缺少对应值时解析失败，不会静默回退到其他等级。参数名跨技能复用时应保持同一数值语义。

导出时会检查角色、技能、等级、Buff、持续流程、投射物、区域、召唤、特效、运动组、战斗开局与 Pipeline/Trigger Authoring 产物的 ID 引用。`emitters.TemplateId` 引用特效表现模板。


`Production/Defines/strong_types.xml` 定义了战斗配置的 Luban bean 和 enum。技能等级、Buff/持续流程修饰器、组件操作、目标查询、地图和战斗开局的嵌套对象已在生产表中展开；技能类型、技能按钮瞄准、投射物命中策略和标签行为门禁等有限整数列也使用 enum。`complex_columns.json` 只登记仍按 JSON 文本解析的历史图结构与触发器迁移字段。枚举在 Excel 中填写名称，Luban 输出原有数值；标签行为门禁直接使用 Luban 生成的 `MobaTagBehaviorGate`，工作簿不再重复维护枚举名称。

bean 的 `*字段名` 是列表：第一、二行的字段名与类型横跨成员列并合并，第三行填写成员名，一项占一行，同一主表记录的 ID 只在第一行填写。例如技能等级的 `CooldownMs`、`Cost`、`Params` 各有独立列。目标查询规则与评分器的可选成员留空即可，导出时不会生成该键。地图向量采用 `X,Y,Z` 的 Luban `MapVector3` 值。地图碰撞形状、投射物响应及战斗开局模式在 Excel 中用 enum 名称填写；发布边界会转换回现有 JSON 字符串。地图 `ObjectType` 是可扩展分类，仍使用字符串。

伤害与击退共用的目标查询可以在 `Rules.Kind` 配置 `RequireCombatHealth`，在排序及数量限制前排除投射物等不承载战斗血量的对象。它不改变阵营、存活、无敌或控制免疫规则；应有血量的单位或召唤物缺少必要组件时仍会报错。

`migrations/migrate_strong_types.py` 记录这次一次性迁移步骤，日常改表无需运行。调整 bean/enum 定义时，先用 `python LubanConfig/Moba/export_pipeline.py --check-baseline` 对照运行时 JSON，再发布并检查二进制加载；不要编辑生成的 C# 或发布副本。

## 日常流程

### Projectile hit subtype filter

`J-投射物表.xlsx` 的「投射物」子表可选字段 `HitUnitSubType` 控制投射物对单位子类型的命中筛选。缺省值 `0` 表示不限制，正数按 `UnitSubType` 精确匹配；未匹配单位由既有碰撞响应返回 `Ignore` 并继续飞行，未注册为单位的静态世界碰撞仍然 `Block`。该字段属于投射物配置与其运行时快照，不在英雄代码或 Trigger 中做身份判断。日常调整直接修改已有生产表字段，不复跑历史加列脚本；Riot 未给出配置数值时，可先用可编辑占位值。

1. 编辑 `Production/Datas/` 中对应的 `J-`、`M-` 或 `T-` 工作簿及其中文子 sheet。前两行是 Luban 字段名和类型，数据从第四行开始；不要改动字段名、类型和 `__tables__.xlsx` 中的英文输出名。
2. 先发布 ActionEditor、Pipeline、Trigger Authoring；状态机有改动时先发布状态机。运行各编辑器的 `Check`，其中触发器使用 `TriggerAuthoringMobaMigration.CheckRuntimeBatch`。
3. 运行 `python LubanConfig/Moba/export_pipeline.py` 验证表、ID、跨表引用和 Ability JSON 节点，并审阅 `local/luban-export/` 下的引用与发布报告。
4. 运行 `python LubanConfig/Moba/export_pipeline.py --apply` 发布 JSON、`.bytes` 和 C# 类型到 Unity 与 Console。
5. 运行 `python LubanConfig/Moba/export_pipeline.py --check-published` 检查发布结果。
6. 运行相关 .NET 配置/冒烟测试；正式发布前再运行已有完整内容与 Unity gate。下面是局部过滤复验，不代替完整 gate。

```powershell
# 沿用上文已创建的 $run；失败后先检查日志，不继续下一步发布。
dotnet test src/AbilityKit.Demo.Moba.Tests/AbilityKit.Demo.Moba.Tests.csproj --filter 'FullyQualifiedName~BattleScenarioRealSkillTests.Luban_six_heroes_spawn_and_cast_first_skill|FullyQualifiedName~MobaLubanConfigPipelineTests' --logger 'trx;LogFileName=moba-config.trx' --results-directory (Join-Path $run 'test-results') *> (Join-Path $run 'dotnet-config.log')
if ($LASTEXITCODE -ne 0) { throw 'MOBA config tests failed; see dotnet-config.log' }
powershell -ExecutionPolicy Bypass -File tools/run-unity-editmode-tests.ps1 -TestAssembly AbilityKit.Game.UnitTests -TestFilter MobaProductionConfigReferenceValidationTests -ResultsPath (Join-Path $run 'unity-config.xml') *> (Join-Path $run 'unity-runner.log')
if ($LASTEXITCODE -ne 0) { throw 'Unity config tests failed; see unity-runner.log' }
```

Unity 包装脚本的 Editor 日志仍固定为 `local/Logs/unity-editmode-run.log`，运行后应随本次结果保留，不并行调用同一固定日志入口；上例显式指定了结果 XML 和 runner 输出。本批未运行上面的 MOBA/Unity 命令，不能把它们的存在记为通过。

`--check-baseline` 只用于一次性迁移验收。它逐项比较 Excel 重建的非触发器资源与原始 JSON。`ability_trigger_plans.json` 由 Trigger Authoring 发布，Luban 不再覆盖它。当前检出未发现 `.github/workflows`，本批只确认 gate 配置和实际本地结果，不沿用旧文对 `moba-luban-config` PR/push job 的持续执行声明；其他 CI 接线需以对应配置与 artifact 另验。

### 发布模式与失败责任

无参数验证不会写正式产物，也不执行 `--check-published` / `--apply` 才进入的全部编辑器副本检查。`--check-published` 会重新生成并校验现有发布，**不会帮助修复差异**。`--apply` 验证后写入两端资源与生成代码；`--apply-prerequisites` 即使不带 `--apply` 也会写入前置表和代码，并跳过部分完整跨来源检查，只适用于解决编辑器作者化依赖顺序，不能作为完整发布验收。

PowerShell 包装器的 `-Apply` 与 `-CheckPublished` 互斥；Python 本身未为所有组合建互斥组，不应依赖不明组合的执行顺序。两者遇到原生失败会阻断；PublicationPlan 检查目标范围和重复 owner，并在写入异常时尝试恢复已变更文件。恢复可能失败，且多个目标文件并非同时可见；没有并发运行锁，也不自动清除计划外的历史产物。停在首个失败处查日志，不通过直接改 Resources/Console 或再次 Apply 掩盖原因。

## 表与加载

`Production/luban.conf` 指定 `dataDir: "Datas"`，并通过 `Datas/__tables__.xlsx` 登记生产表；这里是当前唯一生效的 Luban Excel 目录。登记表按逻辑和表现职责分组到下列工作簿，表定义中的 `input` 使用 `子sheet名@工作簿名.xlsx`。中文工作簿名和子 sheet 名只用于编辑定位，英文表名、字段名以及导出的 JSON、`.bytes` 和 C# 类型保持不变。

技能等级参数由 `Production/Datas/moba-skill-level-parameters.xlsx` 逐行维护：`Id` 是行 ID，`SkillId` 和 `Level` 定位技能等级，`ParamIndex` 从 0 连续编号，`ParamName` 在同一技能内固定对应参数序号，`Value` 是该等级的数值。导出前会检查行 ID、复合键、参数序号、命名和等级完整性，并将数据写入临时 `authoring-input` 下 `J-技能表.xlsx` 的旧嵌套 `技能等级` sheet，再按原 Luban DTO 契约生成资源。生产工作簿中的嵌套 sheet 仅保留技能等级元数据（如冷却和消耗）以及导出兼容结构，不再作为技能参数值的编辑入口；无参数技能可不在明细表中出现。

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
| `moba-overhead-indicators.xlsx` | 头顶指示器定义、布局、Actor 内容绑定 |
| `moba-resource-regeneration.xlsx` | 非 HP 资源到固定量回复属性的显式规则 |

新增表时，在对应工作簿新增子 sheet，并在 `__tables__.xlsx` 登记其 `input`；字段名和输出名仍按现有运行时契约命名。`repartition_workbooks.py` 记录了本次分组迁移，日常编辑无需运行。未登记的旧 `skill_flows.xlsx` 已移到 `Production/Archive/`，仅供迁移留档。

`J-战斗场景表.xlsx` 的「副本」子表是战斗副本的权威定义：把玩法、战斗流程 Profile、场景装配、玩法处理器、胜负规则和所需 Feature 端口组收成单条配置，新增副本不需要改 C# 代码。地图属于玩法资源，由「玩法」子表的 `MapId` 绑定并引用 `battle_maps`；副本通过 `GameplayId` 间接选择地图。导表器检查这些表引用，启动阶段还会验证 create-world 请求的 `MapId` 与 Gameplay 绑定一致。`FlowProfileId` 引用的 `battle_flow_profiles.json` 归 Flow 编辑流程管有，不在 Excel 层校验，改由运行时的副本入场校验负责。规则见 `business/gameplays.py` 和 `business/dungeons.py`，设计背景见 `Docs/MobaViewRuntime副本机制与扩展优先级.md`。

`T-特效表.xlsx` 的 `特效资源` 子 sheet 是 `vfx` 的主源，发布时生成两端的 Luban JSON、`.bytes` 和 Unity 使用的 `Resources/vfx/vfx.json`。`特效表现模板` 子 sheet 保存复用特效的挂点、跟随与外观参数；其 `AssetId` 引用特效资源 ID。特效配置随上文的 MOBA 生产管线发布；旧 `VfxTable.asset` 不是生产发布输入，旧 Unity 特效导出菜单已退役。发布后可用 `Tools/AbilityKit/Demos/Moba/Vfx/Inspect Published Templates` 只读检查，不通过该窗口回写或发布。

`MobaLubanConfigGroups.Create(loader, new[] { "characters" })` 可以只将 `characters` 切换到二进制，其余表默认读取同次导出的 Luban JSON；完整生产加载让其他运行时表（包括持续标签模板）走 Luban `.bytes`，`skill_flows` 固定读取 Pipeline 发布的 `moba/skill_flows.json`。显式传入 `useLubanJsonForRemaining: false` 才使用旧 JSON 路径，该路径保留迁移基线，不能作为当前 Excel 修改后的生产数据来源。标签规则和结果映射从二进制编译，标签变化后重算缓存结果位；不会为普通标记标签生成代码枚举。脑配置优先读取二进制，在没有二进制的自定义资源环境下保留 JSON 兼容。

其余 9 份 MOBA JSON 已各自升为 Luban 表。对象根、数组根和迁移时添加的 `RowId` 由 `table_roots.json` 记录，发布时还原原始 JSON 形状，保持现有消费者可用。这些表同时生成 Luban JSON、`.bytes` 和 C# 类型。

`J-资源节点表.xlsx` 的 `Ability节点` 子 sheet 仍保存 96 份迁移时的 Ability JSON 节点，其中 87 份旧 `ability/triggers` 文件只作历史基线；Luban 不再发布它们。其余 Ability 文档仍按 `Path` 和 RFC 6901 `Pointer` 重建。运行时触发器从 Trigger Authoring 项目发布的 `ability/ability_trigger_plans.json` 加载，Unity 和 Console 均不加载旧分文件目录。

`resource_documents.xlsx` 已归档到 `local/moba-config-migration/`，不参与导出。通用 SO/JSON 导出命令不再是发布入口；英雄向导只创建草稿 SO。Console 正式入口读取 `Configs/luban`，其中 Flow 从 Pipeline 发布的 `Configs/moba/skill_flows.json` 读取。
