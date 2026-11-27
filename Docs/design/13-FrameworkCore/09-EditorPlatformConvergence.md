# 09-编辑器平台收敛与统一入口设计

> 文档类型：Canonical 设计 + 演进计划
> 事实基线：2026-11-25（入口与 Hub 源码盘点）；§2–4 的平台采纳与重复实现以 2026-09-07 的历史核查为基线。

## 一、能力定位

本文是 `com.abilitykit.base.editor` 的 **Editor Platform 跨模块收敛策略**。它回答两个问题：

1. pipeline、HFSM、行为树、BattleScenario、Trigger Authoring、Protocol、Trace 等编辑器的导出、加载、界面、基础功能、运行时调试、校验、项目管理里，**哪些该下沉到 Editor Platform、哪些必须留在各模块**；
2. **已有 Hub 如何区分正式工具、项目工具、诊断和一次性操作**，以及菜单、资产上下文与批处理如何共用能力而不重复暴露。

它不重复 `com.abilitykit.base.editor/Document/BaseEditor基础编辑器工具模块开发设计文档.md`（那是 Platform 的 API 面 + Legacy 兼容边界的包内 canonical），只做跨模块的边界判定、采纳地图、重复证据与收敛顺序。

Editor Platform 统一的是**编辑器工程能力**，不统一领域模型、运行时语义或画布技术。行为树继续用 GraphView，HFSM 继续用分层画布，Pipeline Debugger 保持只读观察，Trigger authoring model 仍由 Ability 包拥有。

## 二、现状：底座已存在，采纳不均衡

### 2.1 底座（E0/E1 已具备）

`com.abilitykit.base.editor` 分两个程序集：

| 程序集 | 命名空间 | 定位 | 状态 |
|---|---|---|---|
| `AbilityKit.Base.Editor` | `AbilityKit.Editor.Framework` | ~~`PlugableWindow`/`WindowBuilder`/`WindowExamples`/`IExporter`~~ | **已退役**（2026-09-07 删除 `Editor/Framework/` 与其兼容测试；唯一消费者 trace 已迁为独立窗口） |
| `AbilityKit.Editor.Platform` | `AbilityKit.Editor.Platform.*` | 模块/菜单/面板注册、服务注册、命令、本地化、`EditorExport`/`EditorAtomicFileWriter`、`EditorDocumentSession`、`EditorSourceSync`、`EditorDiagnostic`、用户偏好存储、UI 基元 | **正式底座**，asmdef 零引用（无领域依赖） |

依赖方向固定（与包内 canonical 一致）：

```text
领域 Editor 程序集 -> AbilityKit.Editor.Platform
AbilityKit.Editor.Platform -X-> 任一领域 Editor/Runtime 程序集
```

### 2.2 采纳地图（E2 事实，按 Editor asmdef 核校）

| 模块 | 接入层 | 已接能力 | 未接/自造 |
|---|---|---|---|
| **BehaviorTree** | Platform（最深） | Module/Menu/Panel 注册、`EditorDocumentSession`（薄包装）、`EditorExport`+`EditorAtomicFileWriter`、`EditorSourceSync`、`EditorDiagnostic`、`EditorCommand`、本地化 | 运行时 observation 全套、GraphView、Editor Extension 注册表（自造 3 套） |
| **HFSM** | Platform（部分） | `IEditorModule`、Module/Menu/Panel 注册、`EditorAtomicFileWriter`、`EditorExportReportWindow`、`EditorDiagnosticCollection`、`EditorPrefsUserStateStore`、`EditorSplitter`、`EditorCommandRegistry` | Legacy Archive 仍保留平行 exporter 抽象（`Editor/Export/Interfaces/*` + `Descriptors/*` + `ExtensionRegistry`）；GraphAsset 编辑会话尚未迁入 `EditorDocumentSession` |
| **Ability（Trigger Authoring 新子系统）** | Platform（部分） | `EditorAtomicFileWriter`、`EditorSourceSync`、`EditorDiagnostic` 适配、`EditorCommandRegistry`、本地化 | **旧导出链完全自造**（见 §4）；不用 `EditorExport` 报告模型、不用 `EditorDocumentSession` |
| **Trace** | 无（独立窗口） | — | 2026-09-07 已从 `PlugableWindow` 迁为独立 `EditorWindow`，不再依赖 base.editor |
| **Pipeline** | 无 | — | 全套自造；`PipelineRuntimeDebuggerWindow`（~1794 行 IMGUI）+ 自造 registry/ring buffer |
| **BattleScenario** | 无 | — | 全套自造；`BattleScenarioCodec` 裸写 JSON、`Stack<string>` 重造 undo |
| **Protocol Editor** | 无 | — | 全套自造；`_dirty` + `DisplayDialogComplex` 重造文档切换 |
| **Diagnostics / Network SDK / Excel-Sync / demo.*.editor** | 无 | — | 各自独立窗口 |

> 事实校正：包内 canonical §六 曾把 Pipeline Runtime Debugger 列为 Platform 消费者。按当前源码核校，pipeline 的 Editor asmdef 只引用 `AbilityKit.Pipeline`（`Editor/AbilityKit.Pipeline.Editor.asmdef`），其"状态/模型拆分"是包内自造，**并未引用 Editor Platform**。本文以 asmdef 引用为准。

## 三、七个关注点的共享/域内边界（可行性判定）

判定原则沿用仓库"稳定原语下沉、易变策略上留"：**可下沉的是稳定的工程原语，域内保留的是领域 schema、路径解析、画布与校验规则**。BT（`07-BehaviorTreePackageDesign.md` §10.1）与 HFSM（`08-HfsmDeterministicRuntimeEvolution.md` "Editor Platform 渐进接入"）已各自写下同一套边界，本文把它们合并为一张总表：

| 关注点 | 下沉到 Platform（稳定原语） | 留在领域（易变策略） | 可行性 |
|---|---|---|---|
| **导出管线** | `EditorExport` Job/Report/Executor（Exported/Unchanged/Skipped/Failed）、`EditorAtomicFileWriter`（原子写+内容不变跳过）、扇出多目标、增量、导出前门禁、报告汇总 | 导出 schema、IR 内容、路径解析、校验规则、产物语义 | **高**——骨架已建并被 BT 全量验证 |
| **加载管线** | `EditorDocumentSession`（undo/redo/dirty/switch）、`EditorSourceSync`（三方分类+覆盖策略） | codec、hash、`AssetDatabase` 路径、资产 baseline、导入 IO | **中**——"加载"本体很薄，值钱的是文档会话与源同步 |
| **界面排版** | 列表/详情/工具栏/搜索/状态栏壳 + UI 基元（诊断列表、命令工具栏、Source Sync 卡片） | **画布/GraphView/节点视图/图层**（明确不造万能图编辑器） | **中**——只下沉壳，画布必须留域内 |
| **基础功能** | `EditorSearchState`、`EditorPrefsUserStateStore`（命名空间隔离）、`EditorCommand`/`EditorCommandRegistry`、本地化 | 选择模型、具体字段编辑 | **高** |
| **运行时调试流程** | ~~编辑器侧实例注册表 + 快照 + 时间轴骨架~~ | 领域快照模型、运行时 registry、可视化投影 | **判定不下沉**（2026-09-07）：四套机制/快照各异、无稳定契约，见 §6.2 步骤 4 |
| **校验功能** | `EditorDiagnostic`/`EditorDiagnosticCollection`（stable code/severity/path/locate/fix + 过滤/搜索） | 校验引擎、规则、领域路径解析 | **高**——展示/出口统一，引擎留域内 |
| **项目管理** | `IEditorModule` + 菜单/面板/命令/服务注册表 | 资产类型、目录结构、批量操作 | **高**——正是统一入口的载体 |

## 四、重复实现证据（必要性）

这不是潜在重复，而是已存在的、可定位的重复：

1. **三个包完全没接底座**：`pipeline`/`battlescenario`/`protocol.editor` 的 Editor asmdef 都不引用 `AbilityKit.Editor.Platform`，导出/加载/窗口/调试/校验全套自造。三处典型：`BattleScenarioCodec.Save` 在 runtime 里 `File.WriteAllText` 写 JSON（本应走 `EditorAtomicFileWriter`）；`BattleScenarioWindow` 用 `Stack<string>` 快照重造 undo（`BattleScenarioWindow.cs:31,248-266`，本应走 `EditorDocumentSession`）；`ProtocolEditorWindow` 用单个 `_dirty` + `DisplayDialogComplex` 重造文档切换（`ProtocolEditorWindow.cs:932-952`）。

2. **Ability 包两代人并存**：旧 Trigger 导出链约 7 个手写 `File.WriteAllText`/`JsonConvert` 的 writer（`AbilityTriggerJsonExporter`、`ReadableTriggerPlanExporter`、`TriggerPlanJsonSplitter`、`SourceJsonExporter`、`AbilityTriggerJsonImporter` 等），把同一份 trigger plan 图序列化成 **5 种 JSON 表示**、写进同一目录 `Resources/ability/`；新 Trigger Authoring 子系统才改用 `EditorAtomicFileWriter` + `EditorSourceSync`。这是全库单点重复最重的地方。

3. **HFSM 手写平行导出抽象**：`Editor/Export/Interfaces/*`（`IGraphExporter`/`IGraphDataExtractor`/`IJsonSerializer`/`ExportResult`）+ `Editor/Export/Descriptors/*`（10 个 DTO）+ 自造 `ExtensionRegistry`，等价于把 `EditorExport` 的 Job/Report 又造一遍——只复用了 `EditorAtomicFileWriter`。

4. **Trace 挂在弃用层**：`TraceTreeWindow : PlugableWindow<...>`（`trace/Editor/Windows/TraceTreeWindow.cs:12`）；且 Framework 示例 `WindowExamples.cs:160` 注册了 `Window/AbilityKit/Ability List`，与 `ability/Editor/Windows/AbilityListWindow.cs:22` **菜单路径撞车**。

5. **模块内部自重复**：pipeline 的 `EditorPipelineTraceRecorder`（`EditorPipelineTraceRecorder.cs:14`）是独立 ring buffer，与 registry 内嵌 trace 存同一份数据，registry 不用它。

6. **BT 虽是标杆也有尾巴**：menu/panel 既注册进 Platform，又用裸 `[MenuItem]` 重复声明（`DebugObservationWindow.cs:62`、`AuthoringProjectAssetInspector.cs:171`）；项目级 "Validate All" 不走 `EditorDiagnostic`、直接弹字符串对话框。

## 五、统一入口与当前盘点

### 5.1 当前实现与风险

`Window/AbilityKit/Hub` **已经存在**。[Hub 窗口](../../../Unity/Packages/com.abilitykit.base.editor/Editor/Platform/UI/EditorHubWindow.cs)只展示显式准入（`showInHub: true`）的模块、菜单和面板，不展示命令注册表。默认不启动反射扫描；展开“迁移清单（只读）”时，[菜单发现](../../../Unity/Packages/com.abilitykit.base.editor/Editor/Platform/UI/EditorMenuItemDiscovery.cs)按 `Window/AbilityKit/`、`Tools/AbilityKit/`、`Assets/AbilityKit/` 前缀发现，过滤校验声明并排除已经准入的路径与 Hub 本身。该清单只列路径供筛选和复制，不执行菜单。此前反射结果可通过 `ExecuteMenuItem` 执行，使写入类工具可能被当作普通窗口打开；此风险已在 Hub 导航层隔离，未改变原 Unity 菜单。

首轮在 `Unity/Packages/com.abilitykit.*`（不含第三方）和 `Unity/Assets` 中静态筛查可解析的 `[MenuItem]` 路径，拆开执行与 validate 声明并另计 `Samples~`。以下是清理前的盘点基线，不是实时菜单数：

- 主体源码有 **128 条执行声明**：`demo.*` 为 59，其他 AbilityKit 包为 65，编辑器宿主 `Unity/Assets` 为 4；另有 19 条校验声明和 `Samples~` 中 6 条执行声明。包名是归属线索；65 不是“正式工具数”。
- 主体路径分布：`Tools/AbilityKit` 88、`Window/AbilityKit` 20、`Assets/AbilityKit` 12、`Assets/Create/AbilityKit` 3，另有 `GameObject/AbilityKit` 1、独立的 `AbilityKit` 根 3、其他 `Assets` 路径 1。静态声明数不是注册后的菜单数，不统计 `CreateAssetMenu`、Inspector 按钮、资产打开回调、Hub 注册或 `-executeMethod` 入口。

| 源码证据 | 问题与处置方向 |
|---|---|
| [HFSM 工作台](../../../Unity/Packages/com.abilitykit.hfsm/Editor/StateMachineEditorWindow.cs)同时有独立窗口入口与资产上下文入口 | 上下文打开目标不同，不按菜单数判定重复。保留有意义的入口，收敛到同一实现；登记旧菜单为兼容路径后再决定去留。 |
| [BattleScenario 组件创建](../../../Unity/Packages/com.abilitykit.battlescenario/Editor/BattleScenarioRootEditor.cs)走 `GameObject/AbilityKit` | 本来依赖场景上下文，不必为了被 Hub 查到而另开一条全局命令。 |
| [Trigger 项目配置菜单](../../../Unity/Packages/com.abilitykit.ability/Editor/Utilities/TriggerAuthoringProjectSetup.cs)位于 Ability 包，却构建 MOBA 默认目录并指向 MOBA 运行时输出 | 标为项目模板/发布接入待核；核对正式消费者和发布链后再定归属，不能因放在框架包就认定为框架能力。 |
| 占位 Prefab 生成器、项目资源修补和正式发布原先都以普通菜单出现 | 按 §5.4 区分已退役的临时工具和继续保留的项目入口；不根据名称或所在目录决定去留。 |
| [MOBA 配置同步脚本](../../../tools/run-moba-config-sync.ps1)明确传入 `-executeMethod` | 批处理是独立入口契约，不能靠菜单扫描估计；不因 UI 整理而改入口方法、退出码和报告路径。 |

### 5.2 编辑器工具准入规范

**一项能力只有一个权威实现和一个维护归属。** Hub 是全局导航，不是所有命令的执行面；窗口、上下文和批处理可以是指向同一能力的不同入口。菜单路径不是能力 ID，`Framework`／`Demos` 路径不是成熟度证明。

| 类别 | 归属与默认可见性 | 入口规则 |
|---|---|---|
| 框架正式工具 | 领域包 `Editor/`；共用稳定原语才进 Editor Platform，底座不反向引用领域或 Demo。 | 可全局发现的工作台显式登记到 Hub；需要兼容时保留 `Window/AbilityKit/<领域>` 薄入口。项目无关的独立命令才放 `Tools/AbilityKit/Framework/<领域>`。 |
| 项目正式 / Demo 工具 | 项目或 `demo.*.editor` 包；随包示例安装器放 `Samples~`，仅在导入样例后服务样例。 | 项目正式工作流进入项目工作台；示例以 `Tools/AbilityKit/Demos/<项目>` 或样例自身入口呈现。不混进框架默认列表，不让框架包反向依赖示例。 |
| 诊断 / 运行时观察 | 归所属领域 Editor 模块；稳定、有人维护的调试视图可保留。 | 放领域工作台诊断页或显式诊断区，默认只观察。需要启动测试宿主、注入状态或导出时单独标出副作用。 |
| 批处理 / 内部支撑 | 对应模块拥有执行与结果契约；仓库包装器由 `tools/README.md` 导航。 | `-executeMethod`、门禁步骤、私有辅助 API 不自动变成菜单，不从 UI 或 `Selection` 猜运行参数。 |
| 临时修补 / 待退役 | 一次性脚本按仓库规范放 `local/tools/<主题>/`；确实依赖 Editor API 的探针在隔离工程使用。 | 不新增常驻菜单、热键、自动初始化或默认 Hub 入口。历史写操作先核对消费者；需要兼容时标明替代入口与退役条件，不复制实现。 |

`Assets/Create/AbilityKit/...`、`GameObject/AbilityKit/...`、`Assets/AbilityKit/...` 分别服务资源创建、场景创建和选中资产操作；可保留相应上下文入口，不机械迁移到 Hub。新增全局窗口和快捷键须说明使用频率与上下文不足之处，并核查冲突；优先在已有工作台加页签，不增加平行工作台。显示名称可本地化，稳定工具标识、批处理方法和兼容映射由所属模块维护，不建手抄全量索引。

**可见性、启停和写入授权分开。** 若在 Hub 增加「显示示例导航」，应默认关闭并作为个人偏好，只影响已显式登记的示例导航，不改变原菜单、包加载、批处理或正在运行的会话。功能启停由项目工作台负责，已有设置复用同一状态来源；打开或显示入口不得自动启动预览、测试或热重载。写入类操作仍须单独确认，不能以启用示例或诊断作为写入授权。

**正式准入与验收：**

1. **归属和契约**：指出真实消费者、所属包、唯一实现、入口上下文、依赖及当前支持环境。正式包入口在包内文档说明，跨包规则留在本页。若已有注册能力满足需要则复用，不为分类另建 Hub 或全局服务。
2. **读写与回退**：打开窗口、发现、校验及 `CanExecute` 不写资产。普通编辑沿用领域的 Undo／保存契约；批量导入、覆盖、迁移、发布先展示输入、明确目标范围和差异，显式 Apply；批处理须等价地显式传写入意图。分清可撤销操作和非事务写入，后者说明失败恢复、版本控制和可重跑方式。报告输出遵循 [test-artifacts](../../../.claude/skills/test-artifacts/SKILL.md)；正式生成物仍按原发布契约落盘。
3. **加载与隔离**：模块注册和订阅需对称清理、可重复加载；导航不在每次重绘做全量反射或资产扫描。验证缺少可选包、无选中资产、编辑/播放切换和 Domain Reload 时的行为，确保 Player 不引用 Editor 程序集。共享底座不得为某个项目或英雄添加分支。
4. **最小证明**：纯分类/去重规则覆盖默认可见性与别名；窗口迁移在 Unity 验证真实菜单、有效和无效上下文、重载行为；写入与批处理增加预检不写、失败不静默和产物等价用例。变更前查 `MenuItem`、`ExecuteMenuItem`、`CreateAssetMenu`、Inspector／资产打开回调、快捷键、`-executeMethod`、测试、文档与外部承诺。测试只覆盖到哪一层，就只报告哪一层。

### 5.3 存量迁移批次

1. **已实现：隔离未分类入口**。行为树的编辑器和运行时观察、HFSM 的编辑器和运行时监视器显式进入导航；示例目录、面板和资产创建贡献不默认展示，已有注册及 Unity 菜单保持可用。反射清单默认折叠、展开才扫描、路径可复制但不可执行。仍需在完整工程验收工作台打开、无选中资产和 Domain Reload。
2. **已完成首批写入项核对**：见 §5.4。继续按目标资产、现行调用和候选替代梳理存量；保留被消费的正式入口，确认无调用才退役。涉及 MOBA 机制或生产/发布语义的变更按[领域治理](../../MobaMechanismGovernance.md)单独确认，本规范不构成批准。
3. **最后收敛兼容入口**：优先验证 BT/HFSM 现有登记与资产上下文；再按实际消费者处理其他包。每批记录旧入口 → 新入口、受影响调用、复验方式和回退方法；只对新增或迁移项约束，不以全仓命名改动作为前置条件。

验证范围：Unity `2022.3.62f1` 隔离工程运行 `AbilityKit.Editor.Platform.Tests`，准入默认隐藏／显式准入、发现路径排除 3 个新增用例通过；全套 25/28，通过同一工程重跑修改前源码复现了相同的 3 个既有断言失败。此证据只覆盖底座编译与测试，不代表在主工程实点过 Hub 或验证过 BT/HFSM 工作台。结果路径见本地 `local/Logs/editor-hub-20261125-071232535/`。

### 5.4 首批写入入口核对

| 入口 | 证据 | 处理 |
|---|---|---|
| `Generate Temporary Presentation Prefabs` | 宿主旧脚本将 primitive 占位直接写入 `character3`–`character7` 和 6 个效果 Prefab；11 个目标 Prefab 均已入库。按类型、菜单路径和脚本 GUID 未找到仓库内现行调用或序列化引用。另一个动画生成器写入同批角色路径，两个入口并存会覆盖同一目标。 | **退役** `Unity/Assets/Scripts/Editor/MobaTemporaryPresentationPrefabGenerator.cs` 及 `.meta`，保留已有 Prefab 和材质。后续维护以现有资产与配置为准，不重跑旧占位生成器。脚本和 GUID 可从 `baaa2c985` 查回。 |
| [Jinx Publish Live Repair](../../../Unity/Packages/com.abilitykit.demo.moba.editor/Editor/Moba/JinxLivePresentationSync.cs) | 写入 MOBA `Resources/MobaVfx/Jinx/`；[发布验收记录](../../../plans/JinxLiveGameplayRepairAcceptance.md)明确使用完整及聚焦批处理方法，不能当成无消费者的一次性脚本。 | **保留项目发布入口**及 `-executeMethod` 方法；仅移除 Ctrl+Alt+J（macOS 为 Cmd+Option+J）快捷键，保留显式菜单，避免误触发布。未改变生成或发布语义。 |
| [创建 MOBA 项目配置](../../../Unity/Packages/com.abilitykit.ability/Editor/Utilities/TriggerAuthoringProjectSetup.cs) | [触发器工作台](../../../Unity/Packages/com.abilitykit.ability/Editor/Windows/TriggerAuthoringWorkspaceWindow.cs)有两处按菜单路径调用，写项目、目录资产和 MOBA 输出配置。 | **保留**现行项目模板入口。它不是通用初始化工具；包归属整理前先明确工作台如何获取项目模板，不能直接移除菜单。 |
| `Generate Animated Character Prefabs` / `Generate Luban Airship Placeholder` | 用户确认是一次性项目工具。按类型名、菜单路径和脚本 GUID 未发现现行调用或序列化引用。角色 Prefab、材质、共享 Animator Controller 和飞艇资产共 19 个写入目标均已入库；MOBA 模型配置仍消费飞艇 Prefab，不依赖生成器。 | **退役** `Unity/Assets/Scripts/Editor/` 下的 `MobaAnimatedPresentationPrefabGenerator.cs`、`MobaLubanAirshipPrefabGenerator.cs` 及各自 `.meta`。未跟踪源码按治理规范归档到本机 `local/tools/editor-tool-retirement/moba-presentation-generators/`，不提交；如需回退，按归档内 `sha256.json` 校验后还原原相对路径。后续直接维护现有资产和配置，不重跑临时工具。 |

本批核查使用源码、资产引用与 Git 历史，未执行生成或发布，也未改动 Prefab、材质、机制或配置。

### 5.5 第二批入口盘点与迁移建议

场景叠加收敛前，对工作区 `Unity/Assets` 和 `Unity/Packages/com.abilitykit.*` 的 C# 源码按 `[MenuItem]` 文本复扫：主体有 128 条执行候选（Demo 包 59、其他包 68、宿主 1），另有 19 条校验声明；`Samples~` 另有 6 条执行候选。仅用于核对存量，不代表 Unity 实际注册数、独立工具数或生产写入数；不把 CLI、上下文按钮或批处理遗漏视为无调用。仓库级脚本仍从 [工具索引](../../../tools/README.md) 进入。

| 入口与证据 | 建议归属与处置 |
|---|---|
| MOBA Scene Gizmos 原有 7 个绘制菜单和重置、清缓存两个操作；[持久化](../../../Unity/Packages/com.abilitykit.demo.moba.editor/Editor/SceneGizmos/MobaGizmoSettingsPersistence.cs)已有状态迁移 | **首批已收敛。** 集中到 [MOBA 战斗调试](../../../Unity/Packages/com.abilitykit.demo.moba.editor/Editor/BattleDebug/BattleDebugWindow.cs)的「场景叠加」页，详见 §5.6。 |
| [MOBA Flow Pump](../../../Unity/Packages/com.abilitykit.demo.moba.editor/Editor/Preview/EditorGameFlowPumpWindow.cs)启动编辑态驱动并清理预览；[FrameSync Test](../../../Unity/Packages/com.abilitykit.demo.moba.editor/Editor/FrameSync/FrameSyncTestWindow.cs)操作场景里的测试 harness | **已归并导航。** 使用战斗调试的「预览」和「同步诊断」入口，各自执行链保留，见 §5.8。 |
| [Shooter Demo](../../../Unity/Packages/com.abilitykit.demo.shooter.editor/Editor/Windows/ShooterDemoWindow.cs)已有验收区；[矩阵执行器](../../../Unity/Packages/com.abilitykit.demo.shooter.editor/Editor/Windows/ShooterAcceptanceAutomation.cs)仍有文档化批处理调用 | **已合并 UI。** `Run Catalog Matrix` 已移入窗口验收区；执行器和批处理名称保留，见 §5.7。 |
| [MOBA P1](../../../Unity/Packages/com.abilitykit.demo.moba.editor/Editor/Moba/MobaP1SkillFlowShowcaseSync.cs)／[P2](../../../Unity/Packages/com.abilitykit.demo.moba.editor/Editor/Moba/MobaP2SkillProgrammingShowcaseSync.cs)样例同步会写资产，[样例测试](../../../Unity/Packages/com.abilitykit.demo.moba.editor/Tests/SkillFlowDefTests.cs)引用常量、生成资产及恢复命令 | **已归并交互入口。** 使用 MOBA「示例维护」预检写入范围并确认；原 `SyncBatch`、测试和资产落点保留，见 §5.9。 |
| [旧协议迁移](../../../Unity/Packages/com.abilitykit.protocol.editor/Editor/ProtocolEditor/UI/ProtocolEditorWindow.cs)、[头顶 HUD Prefab 构建](../../../Unity/Packages/com.abilitykit.demo.moba.editor/Editor/Moba/MobaOverheadPrefabBuilder.cs)和英雄发布修补（§5.4） | **不按命名删除。** 协议迁移仍是已文档化的兼容入口，并有[冻结测试](../../../src/AbilityKit.Protocol.Tests/ProtocolEditorLegacyTrackFreezeTests.cs)；HUD 是正式资产的结构生成入口；英雄发布有验收或批处理证据。支持期未结束或写入契约未迁完时，保持现状。 |

**实施顺序**：先做 MOBA Gizmos（§5.6），再合并 Shooter 验收 UI 和 MOBA 预览导航，最后处理示例同步与项目发布等写入入口。[现有示例目录](../../../Unity/Packages/com.abilitykit.samples/Editor/SampleCatalogWindow.cs)保留为通用样例入口；如需在 Hub 集中发现项目 Demo，按 §5.2 增加可选导航，不为此新增第二个 Hub。每批核对旧菜单调用、上下文、Domain Reload 后的设置和启停行为。真正的一次性工具按 Skill 退役，不放进「隐藏」菜单继续维护。

### 5.6 首批落地：MOBA 场景叠加

- **唯一设置入口**：`Tools/AbilityKit/Demos/Moba/Battle/战斗调试` →「场景叠加」。[面板](../../../Unity/Packages/com.abilitykit.demo.moba.editor/Editor/BattleDebug/Panels/BattleDebugSceneOverlayPanel.cs)不要求存在实时会话或选中角色；七个独立开关仍使用原读取和设置方法。重置、清缓存是独立按钮。旧设置键、默认值、迁移、绘制器及战斗运行逻辑均不变。
- **旧入口退役**：`Tools/AbilityKit/Demos/Moba/Gizmos/` 的九个执行菜单与七个校验声明均已移除。旧 `Editor/SceneGizmos/SceneGizmoSettingsMenu.cs` 及 `.meta` 可从 `3b1cc05d5` 恢复；未找到现行菜单调用或序列化引用。
- **验收边界**：逐项核对新旧开关、重置及清缓存均调用相同设置／缓存接口；按 Unity 编译参数局部编译 MOBA Editor 程序集。未在 Unity 界面验证交互、Scene View 绘制和 Domain Reload；不将静态核对或编译视作 Play Mode 验收。

### 5.7 第二批落地：Shooter 验收矩阵

- **交互入口**：`Tools/AbilityKit/Demos/Shooter/Shooter Demo` →「验收规格」→「运行完整验收矩阵」。保留 BasicCombat 基线按钮。编辑模式下显式运行，活跃演示或启动中禁用；结果摘要在同区显示。不复用窗口的远程连接配置。
- **旧菜单退役**：未找到 `Tools/AbilityKit/Demos/Shooter/Shooter Demo/Acceptance/Run Catalog Matrix` 的仓库内现行调用；旧菜单声明可从 `6a7399a11` 查回。[批处理说明](../../Shooter验收外壳绑定说明.md#unity-自动化验收)仍使用原 `-executeMethod` 方法名。增加真正的零参重载；参数化签名、矩阵执行、结果及失败判定不变。
- **验收边界**：隔离 Unity 2022.3.62f1 探针验证只有可选参数的入口会被拒绝、同名零参重载可调用；Shooter Editor 定向编译及现有矩阵覆盖用例通过。未在 Unity 主工程运行完整批处理或点击新窗口按钮。

### 5.8 第三批落地：MOBA 预览与同步诊断

- **统一入口**：`Tools/AbilityKit/Demos/Moba/Battle/战斗调试` →「数据与工具」。其中「预览 → 编辑器驱动（Flow Pump）」和「同步诊断 → 帧同步测试」均可在无实时战斗会话时打开，只导航到原窗口，不启动模拟、不创建对象或建立连接。Demo 场景命令不在本批迁移范围。
- **兼容与退役**：未发现两条旧 `MenuItem` 的仓库内现行调用，已移除 `Tools/AbilityKit/Demos/Moba/Preview/编辑器驱动(Flow Pump)`、`Tools/AbilityKit/Demos/Moba/FrameSync Test`；原菜单声明可从 `241003997` 查回。`FrameSyncTestWindow.Open()` 保留，两个窗口类型与 `.meta` 不变。Flow Pump 的编辑模式检查、显式启动/停止及关闭清理、FrameSync 窗口的 Harness 查找和会话按钮均未改动。
- **验收边界**：MOBA Editor 定向编译通过，静态对照确认两个窗口的打开方法体及其后续实现不变。仅变更导航和文档，不验证运行模拟、帧同步/状态同步链路或外部连接。窗口打开、重载及随后启动/停止仍需 Unity 交互验收。

### 5.9 第四批落地：MOBA P1/P2 示例维护

- **唯一交互入口**：`Tools/AbilityKit/Demos/Moba/Battle/战斗调试` →「数据与工具 → 示例维护」。[预检](../../../Unity/Packages/com.abilitykit.demo.moba.editor/Editor/Moba/MobaShowcaseMaintenance.cs)列出源 JSON、新建或覆盖目标，以及 P2 可能更新的触发器项目；不比较字段。遇到缺失输入、错误目标、脏目标或跨项目模块时不提供写入按钮；取消不执行同步。
- **旧菜单退役**：移除 `AbilityKit/Moba/Skill Flow/Sync P1 Economy Showcase` 和 `AbilityKit/Moba/Skill Flow/Sync P2 Skill Programming Showcase`，未找到仓库内菜单调用，可从 `7a53ce362` 查回。保留两个 `Sync` 和 `SyncBatch` 方法、默认行为和目标路径；预检只约束交互入口。现有保存范围不变（`SaveAssets` 可能保存其他脏资产），失败不是事务回滚，应依版本控制检查和恢复。
- **验证边界**：MOBA Editor 定向编译通过；替身探针验证打开菜单不访问资产、缺输入和脏目标阻断、取消不调用、确认只派发选中的同步。不运行真实示例同步或生产发布，不改 JSON、资产或执行机制；确认弹窗和 Unity 导入仍需交互验收。

### 5.10 第五批落地：MOBA 状态机发布与校验

- **唯一交互入口**：项目正式工具留在 [`MobaStateMachineAuthoringWindow`](../../../Unity/Packages/com.abilitykit.demo.moba.editor/Editor/Moba/MobaStateMachineAuthoring.cs)，不并入战斗诊断或示例维护。保存源文件、只读校验与发布分开；有未保存修改时禁用发布。交互发布先对已保存源文件预检，按目录展示新建、覆盖、旧文件清理数量，再明确确认；实际写入仍走原发布器。
- **旧菜单退役**：`Tools/AbilityKit/MOBA/Check State Machines` 和 `Tools/AbilityKit/MOBA/Publish State Machines` 分别由 `Window/AbilityKit/MOBA 状态机编辑器` 中的「校验」和「发布」替代。未发现仓库内菜单调用，旧注册可从 `34595f4fd` 查回。保留 `MobaStateMachinePublisher.Check` / `Publish` 方法名、现有批处理调用方式和写入范围；组合发布及英雄同步不改线。
- **验证边界**：MOBA Editor 定向编译通过。使用隔离文件副本及 Unity API 替身验证校验与预检不写入、无效源阻断、发布结果与预检一致。未运行生产发布，也未在真实 Unity 窗口点击确认或验证资源刷新。

### 5.11 第六批落地：MOBA 生产 Skill Flow

- **唯一交互入口**：生产 `SkillFlowCO.asset` 的 [Inspector](../../../Unity/Packages/com.abilitykit.demo.moba.editor/Editor/Moba/MobaConfigTableAssetSOEditor.cs)。未保存或处于播放流程时禁用；发布预检只读，确认后仅写入预检发现的新建与覆盖项。校验只写本地诊断预览，统一归入 `local/Logs/`，每次运行隔离并区分 Unity 和 Console。
- **旧菜单退役**：移除 `Tools/AbilityKit/Demos/Moba/Pipeline/Validate Published Flows`、`Tools/AbilityKit/Demos/Moba/Pipeline/Publish Flows` 和 `Tools/AbilityKit/Demos/Moba/ActionEditor/Sync XiaoQiao Skill 1 Flow`。未发现仓库内自动化调用这些菜单，旧注册分别可从 `1345f951f` 和 `7a53ce362` 查回。保留 `Check`、`Apply` 和历史 `Sync` 方法，保留原输出目标、内容比较和流水线调用方式；不修改时间轴导出或玩法机制。
- **验证边界**：MOBA Editor 定向编译通过；隔离文件与 Unity API 替身验证取消不写、差异比较、确认写入范围、无效引用阻断、报告隔离及批处理等价。未运行生产发布；真实 Unity Inspector 的可见性与交互仍需手动验收。

### 5.12 第七批落地：MOBA 角色动画同步

- **交互归属**：正式动画绑定维护、扫描和漂移检查集中在 [角色动画同步工作台](../../../Unity/Packages/com.abilitykit.demo.moba.editor/Editor/Moba/MobaCharacterAnimationSyncWindow.cs)，保留状态机编辑器携带角色打开的入口；不与一次性动画或表现生成器混同。写入前已有扫描差异，完整 Luban 发布另需明确确认。扫描报告沿用 `local/Logs/character-animation-sync/` 的按目标覆盖规则。
- **旧菜单退役**：`Tools/AbilityKit/MOBA/Check Bound Character Animations` 改用工作台「仅检查已绑定动画漂移」；除用法说明外未找到仓库内菜单消费者，旧注册可从 `34595f4fd` 查回。`Check`、`CheckIfConfigured`、批处理、源写入和发布调用链均保留。
- **验证边界**：MOBA Editor 定向编译通过；替身探针覆盖模型和旧节点的写入提示、取消不执行同步或重扫、源更新与确认发布的分派。未运行生产同步、Luban 发布或真实 Unity 窗口交互验收。

### 5.13 第八批落地：MOBA 地图工具

- **集中导航，分开写入**：四个入口统一归入 `Tools/AbilityKit/Demos/Moba/Map`，不新建工作台。校验只读已发布地图和兵线；Prefab 刷新预览活动场景、同名生成根替换及 Prefab 路径。兵线与波次、地图建筑仍在各自窗口编辑与发布；预检后明确确认，取消不写，不隐式刷新 Prefab。
- **旧菜单迁移**：`Tools/AbilityKit/Demos/MOBA` 下的 `Validate Map And Minion Routes`、`Refresh Generated Map (Map 1)`、`Edit Minion Routes`、`Edit Map Structures` 分别改为 `Map` 下的「校验地图与兵线（Map 1）」「预检并刷新地图 Prefab（Map 1）」「编辑兵线与波次」「编辑地图建筑」。未找到仓库自动化消费旧菜单，可从 `a7feba7dd` 查回。保留场景装配的共享校验、`ValidateMapAndMinionRoutesBatch` 和 `RegenerateMapPrefabBatch`，不改正式资产路径或玩法机制。
- **验证边界**：MOBA Editor 定向编译通过；Unity 批处理实际执行 Map 1 的 `ValidateMapAndMinionRoutesBatch` 成功，退出码 `0`。未运行任何地图写入型操作；窗口取消、确认及 Prefab 覆盖尚需用可丢弃场景验收。

### 5.14 第九批落地：示例场景启动与装配分离

- **读写分开**：`Tools/AbilityKit/Demos/Moba/Open Demo Scene` 和 `Play Existing Demo Scene` 复用现有打开逻辑，不隐式生成装配；找不到场景时提示写入入口而不是兜底生成。Battle Scenario 继续调用同一打开及启动逻辑。唯一交互生成入口为 `Tools/AbilityKit/Demos/Build Package-Owned Gameplay Composition`，先只读预检，再展示两套装配、Starter、Build Settings 和旧资产清理范围，明确确认后才生成。
- **旧菜单退役**：MOBA 的 `Create Or Refresh Demo Scene` 改用全局装配入口；旧 `Play Demo Scene` 拆为按需显式生成和 `Play Existing Demo Scene`。未找到仓库自动化消费旧菜单，旧实现可从 `1345f951f` 查回。保持 `GenerateAll`、`CreateOrRefreshDemoSceneBatch`、正式资产路径和启动请求契约不变。
- **验证边界**：MOBA Editor 定向编译通过；替身探针覆盖场景缺失、打开或播放不生成、预检错误阻断、取消不生成、确认与批处理分派。未运行真实装配生成、场景切换或 Play Mode。

### 5.15 第十批落地：MOBA 特效检查与发布入口收口

- **保留各自权威入口**：项目专用的 `Tools/AbilityKit/Demos/Moba/Vfx/Inspect Published Templates` 继续只读检查已发布模板和运行时实例；不变更 ActionEditor 联动。`T-特效表.xlsx` 仍是生产源，复用 [Luban MOBA 发布链](../../../LubanConfig/Moba/README.md)，不在 Unity 新增发布入口。
- **退役空壳**：移除 `Tools/AbilityKit/Demos/Moba/Vfx/` 下的 `Export Vfx Json` 和 `Export Vfx Json (Assets)`，及包内 `Editor/Moba/VfxJsonExporter.cs` 和 `.meta`。两条菜单均只报错提示使用 Luban，不扫描也不写入；未发现仓库内的方法、菜单自动化或 GUID 消费者。旧脚本及元数据可从 `66c94e1fc` 查回。
- **验证边界**：旧入口只在退役记录中保留；Unity 2022.3.62f1 的 Roslyn 定向编译通过，使用当前源码和 asmdef 引用、复用已编译依赖。未在 Unity UI 验收，也未执行生产发布；检查器、工作簿、生成物和运行时契约不变。

### 5.16 第十一批落地：MOBA 单英雄菜单退出日常发布

- **退役菜单**：移除 `Tools/AbilityKit/Demos/Moba` 下 Annie 的 `Publish Passive Q W E`／`Check Passive Q W E`，Jinx 的 Q/W/E/R `Publish`／`Check`、`Publish Q Presentation`、`Publish Live Repair`，以及 Veigar 的 `Publish Live Presentation`，共 13 项。发布方法可以覆盖源资产或派生 Prefab 并调用全量发布；检查方法复用全量 Flow 或 Trigger 检查，并不提供独立的单英雄检查范围。未发现依赖这些菜单路径的仓库自动化；旧登记可从 `1227f8f34` 查回。
- **保留调用**：只移除 `MenuItem`，不删 `AnnieSkillSync`、`Jinx[QWER]SkillSync`、`JinxLivePresentationSync`、`VeigarLivePresentationSync` 的方法或改其副作用。保留历史迁移记录及直接调用，也保留 `JinxLivePresentationSync.Publish` 对 W 同步、`MobaAreaPresentationResourceSync.Publish` 对维迦区域资源派生的调用。不把这些修补方法移入正式 Hub 或测试门禁；资源派生与正式配置发布不是同一操作。
- **替代与边界**：日常根据改动选择生产 Flow Inspector、Trigger Authoring、ActionEditor、HFSM 或 Luban 的正式入口，按 [MOBA 技能管线发布顺序](../../MobaSkillPipeline.md#发布顺序) 执行。本批不改配置、资源或机制，也不执行发布或修补。
- **验证**：逐文件比对确认 7 个脚本只移除 13 个菜单特性，方法源码不变；Unity 2022.3.62f1 Roslyn 定向编译通过，复用已编译依赖。未验证 Unity 界面刷新或运行英雄验收。

### 5.17 第十二批落地：头顶 HUD 生成与 Lux 表现审计分责

- **正式结构生成**：旧 `AbilityKit/MOBA/Build Overhead HUD Prefab` 改为 `Tools/AbilityKit/Demos/Moba/HUD/预检并重建头顶 HUD Prefab`；未发现旧路径的自动化消费者。只读预检给出新建或覆盖目标，拒绝繁忙编辑器、Prefab Mode、错误类型和有未保存修改的目标；取消不调用生成。保留 `MobaOverheadPrefabBuilder.Build` 批处理及正式资源路径，保存失败显式报错。旧入口可从 `f1e513eb8` 查回。
- **仅保留审计批处理**：`LuxPresentationAudit.Capture` 有历史验收复现用途，不登记菜单或默认门禁；采样目录改为显式绝对路径且必须新建或为空，在刷新、切场景或重建资源前校验。保留 `-luxRebuild` 的显式写入契约和 PNG、`bounds.json` 格式，不改变实际采样或玩法机制。
- **范围**：只调整入口防护、产物路径和指引；不执行真实 Prefab 覆盖、Lux 资源重建或生产发布。
- **验证**：Unity 2022.3.62f1 Roslyn 对两个改动文件的定向编译通过；本地探针覆盖 HUD 取消、阻断、确认和保存失败，以及 Lux 输出参数和防覆盖，共 12 项。复用旧依赖的全编辑器程序集编译受并行回放改动阻断；未做 Unity 界面、实际保存或图形采样验收。

### 5.18 第十三批落地：MOBA 无菜单批处理核对

- **保留正式及复现入口**：`MobaActionTimelineBatch.Apply` 有作者工作流和清单，`MobaMechanismPublisher.PublishAndCheck` 有发布说明，按现有契约保留并不新增菜单。Jinx W 同步有内部调用；Jinx 表现修补和区域资源派生有复现记录；小乔旧方法有兼容说明，继续保留，不按「没有菜单」批量删除。
- **退役无消费者的导入包装**：移除 `Editor/Moba/JinxRSkillSync.cs` 及 `.meta`。未发现仓库内方法、菜单、GUID 消费者或已登记批处理；日常编辑生产 Flow 并走 Flow、Trigger Authoring 正式发布及检查，不再从示例文件覆盖生产条目。保留 `jinx-r-flow.json`（仍由四英雄时间轴批量清单引用）、生产 Flow `30020401` 和独立的 Jinx R 表现派生方法。旧脚本可从 `34595f4fd` 恢复。
- **归并单次审计产物**：保留 `MobaCharacterAnimationTimingAudit.Capture` 的模型与可选姿态参数，新增显式输出路径；不再写共享固定文件，历史调用自动分配运行目录。指定非空目录时在资源读取和采样前拒绝；文件名和采样含义不变。用法只在包内维护，不把历史报告路径当作现行契约。
- **验证边界**：动画审计源码通过 Unity 2022.3.62f1 Roslyn 定向编译；本地探针通过 7 项输出路径及防覆盖验证，保留的清单与生产 Flow 已核对。未运行 Unity 采样、正式发布或玩法测试。

### 5.19 第十四批落地：历史英雄资源检查分流

- **历史复现，不转正**：Karthus、Lux、Gragas 的三个 `Inspect` 只采集报告；当前工作树的未跟踪 `integrate_karthus.py`、`integrate_lux.py`、`integrate_gragas.py` 仍按固定路径读取对应报告，暂不删除方法或改写这些脚本。`GragasSkillSync.InspectQAnimationSegments` 有 Q 资源验收复现记录，但未发现固定报告路径的程序消费者。
- **只收口报告**：四个方法共用 `-mobaResourceFactsOutput`，指向 `local/Logs/` 下的绝对目录，显式输出拒绝覆盖同名文件。三个 `Inspect` 保留无参数固定路径作为迁移兼容，Q 分段检查改为默认独立运行目录。保留已有报告、方法名、采集数据和 JSON 结构，不执行迁移、资源重建或发布。固定路径兼容在迁移消费者改用显式输入或退役后再移除。
- **验证**：四个改动源码文件通过 Unity 2022.3.62f1 Roslyn 定向编译；本地报表探针通过 9 项固定路径兼容、隔离、防覆盖和参数验证。未运行 Unity 资源扫描或历史迁移。

### 5.20 第十五批落地：历史迁移与资源报告解除固定路径依赖

- **保留本地复现链，不升级为正式工具**：当前工作树中的三个 `integrate_*` 英雄迁移均未跟踪、不属于门禁或日常发布；Gragas 的表格辅助函数还被其他本地迁移导入，Lux 也复用 Karthus 的辅助方法。因此不移动、不删、不暂存这些脚本。Karthus、Lux、Gragas 改为用 `--resource-facts` 显式读入对应报告，并在进入工作簿操作前解析报告；原有共享辅助入口不变。
- **撤销固定路径兼容**：四个 `Inspect` 批处理均可显式指定报告目录，未指定时分配 `local/Logs/moba-resource-facts/` 下的独立目录。报告结构、文件名及历史文件不变，不再覆盖 `local/` 下的旧报告，不新增生产发布入口。
- **验证**：四个 C# 文件定向编译通过；三个 Python 入口以替身验证 `--help`、缺参/无效报告阻断和显式输入传递，报告探针验证默认隔离与显式路径防覆盖。未运行 Unity 实际采集、历史迁移或发布。

### 5.21 第十六批落地：归档 MOBA 历史只读探针

- **本地归档**：盘点 `LubanConfig/Moba/migrations/` 下 36 个未跟踪脚本，只将 4 个仅打印工作簿行、未发现调用者的临时探针移入 `local/tools/moba-migration-probes-archive/`。这是保留原字节的历史快照，不是可移植的工具入口；恢复映射与 SHA-256 只留本地。
- **保留依赖**：其余 32 个未跟踪脚本不动。英雄迁移和表现修补存在同目录导入，也有本地调用方按文件路径加载迁移脚本；单有历史文档引用不证明它仍在执行。能写生产表、归属未明或仍有引用的存量须逐项复核，不因脚本未跟踪而迁移。
- **验证（静态）**：归档前后哈希一致、原路径不再存在、归档路径被 Git 忽略；未执行探针、迁移、导出或发布。日常入口不变，见 [MOBA 生产配置说明](../../../LubanConfig/Moba/README.md)。

## 六、目标架构与收敛顺序（演进计划）

### 6.1 目标端态

需要全局发现的框架工作台显式贡献导航；领域按实际需求复用共用原语：导出 `EditorExport`+`EditorAtomicFileWriter`、文档会话 `EditorDocumentSession`、源同步 `EditorSourceSync`、校验 `EditorDiagnostic`、个人偏好 `EditorPrefsUserStateStore`。画布、IR schema、校验规则、快照模型留在各模块。Hub 是统一发现入口；资产上下文、批处理和有消费者的兼容菜单仍按 §5.2 保留，不为登记强制引入无用途的依赖。

### 6.2 收敛顺序（按风险从低到高、收益从高到低）

1. **统一入口**——Hub 基础已实现；入口准入与存量迁移改按 §5 执行，不再以反射发现直接作为正式导航资格。
2. **BT 收尾 + 导出报告模型进 HFSM/Ability**——HFSM 已接入统一入口、导出报告和运行时调试导航；后续在 golden/roundtrip 测试保护下删除 Legacy Archive 平行 exporter 抽象，并收敛 Ability 旧导出链。BT 清掉重复 `[MenuItem]` 和绕过 `EditorDiagnostic` 的 Validate All。
3. **Pipeline/BattleScenario/Protocol 接基础件**——至少接 `EditorAtomicFileWriter` + `EditorDocumentSession`（替换 `BattleScenarioCodec` 裸写、`_undoStack`、`_dirty` 对话框）+ `EditorDiagnostic`。纯减重复，不动各自画布和 IR。
4. **下沉运行时调试骨架**——✅ 分析完成（2026-09-07），**决定不下沉**。四个 debugger 的运行时桥接机制、快照模型、订阅方式各不相同：hfsm `LiveRegistry`+`IVisualizationProvider`（注册表+provider 抽象）、BT `DebugRegistry`（薄 id 注册表、纯轮询）、pipeline `PipelineDebugHooks`（事件总线 push）、trace `TraceRegistryDirectory`（目录+事件）。共享部分只剩「观察运行时实例」这一抽象概念，无稳定契约；按「能力下沉五条」判定（语义不稳、无交叉验证）不下沉。四个 debugger 保持各自实现，已由 Hub 在导航层统一。落地动作仅是删除 pipeline 死代码 `EditorPipelineTraceRecorder`（未使用单例 + 重复 ring buffer，保留被 registry 使用的 `EditorPipelineRunTrace`）。
5. **退役 Framework 层**——✅ 已完成（2026-09-07）：`trace` 迁为独立 `EditorWindow`（`TreeVisualizationPlugin`/`NodeDetailPlugin` 去 `BaseWindowPlugin` 基类），删除 `Editor/Framework/`（`PlugableWindow`/`WindowBuilder`/`WindowExamples`）与 `Tests/Framework/` 兼容测试。

## 七、源码入口

| 项 | 位置 |
|---|---|
| Platform 底座 | `Unity/Packages/com.abilitykit.base.editor/Editor/Platform/` |
| Platform 程序集边界 | `.../Editor/Platform/AbilityKit.Editor.Platform.asmdef` |
| Legacy 层 | `Unity/Packages/com.abilitykit.base.editor/Editor/Framework/` |
| BT 接入样例（最完整） | `com.abilitykit.behaviortree/Editor/Bootstrap/EditorModule.cs`、`Authoring/Documents/`、`Export/`、`Synchronization/` |
| HFSM 平行导出抽象（待收敛） | `com.abilitykit.hfsm/Editor/Export/` |
| Ability 新旧两代（待收敛） | `com.abilitykit.ability/Editor/Utilities/`（旧导出链）vs `TriggerAuthoring*`（新子系统） |
| 未接底座样例 | `com.abilitykit.pipeline/Editor/Debug/`、`com.abilitykit.battlescenario/Editor/`、`com.abilitykit.protocol.editor/Editor/` |
| 包内 canonical | `com.abilitykit.base.editor/Document/BaseEditor基础编辑器工具模块开发设计文档.md` |
| 领域边界既有定义 | `Docs/design/13-FrameworkCore/07-BehaviorTreePackageDesign.md` §10.1、`08-HfsmDeterministicRuntimeEvolution.md` "Editor Platform 渐进接入" |

## 八、事实状态与证据等级

- **规范约束**：依赖方向 `领域 Editor -> Platform`、`Platform -X-> 领域`；导出/校验/同步/会话/命令/本地化的共享边界见 §三。
- **当前实现**：Platform Hub 统一入口已实现；BT 与 HFSM 已注册 Module/Menu/Panel，Ability 部分接入，Pipeline/BattleScenario/Protocol/Trace 仍主要通过散落窗口发现进入（§2.2、§5）。
- **示例策略**：HFSM 的 `ExtensionRegistry` 导出抽象、Ability 旧导出链，都是"尚未收敛"的领域自造实现，不是底座能力。
- **已知限制**：`dotnet build` 只证明可编译，不等于 Unity Test Runner 已执行；Domain Reload、语言切换、布局恢复、诊断定位、真实 AssetDatabase 导入仍需 Unity 侧验收。

| 等级 | 状态 | 说明 |
|---|---|---|
| E0 | 已具备 | Platform/Legacy 源码、asmdef、包内 canonical 存在 |
| E1 | 已具备 | Platform 服务/命令/诊断/状态/会话/同步/导出 API 可被调用 |
| E2 | 部分具备 | Hub 与 BT/HFSM 正式入口已接；Ability 部分接入，Pipeline/BattleScenario/Protocol/Trace 未接 |
| E3 | 局部具备 | §5.3 的 Platform 隔离 EditMode 证据；未覆盖完整工程的工作台与导航集成 |
| E4 | 待建立 | Hub 与各领域导出/同步/诊断的 Unity 侧验收矩阵未建立 |
| E5 | 待建立 | 统一入口与迁移门禁未挂 CI |

## 九、风险与约束

- **画布不能下沉**：Hub 与 Platform 只做壳，不提供"万能图编辑器"；强行统一 GraphView/分层画布会让领域编辑器丧失表达力并产生大量迁移成本。
- **运行时调试最易过度抽象**：四个 debugger 的快照模型与可视化各不相同，先做骨架、后谈统一；在下沉前必须有两个以上非同构消费者证明骨架稳定（对齐仓库"能力下沉五条"）。
- **Legacy 兼容**：`PlugableWindow`/`WindowBuilder` 已按 §6.2 退役；仍有消费者的菜单路径、快捷键和批处理方法是另一类兼容契约，不随旧框架退役自动删除。
- **迁移顺序不可逆**：删 HFSM 平行导出抽象、收敛 Ability 旧导出链前，必须有 golden/roundtrip 迁移测试锁定产物等价，避免"减重复"变成"丢格式"。

---

*文档类型：Canonical 设计 + 演进计划 | 事实基线：2026-11-25（§5 入口盘点及局部 EditMode，E0–E3）；其余历史证据不晋级*

*文档版本：v1.23 | 最后更新：2026-11-25*
