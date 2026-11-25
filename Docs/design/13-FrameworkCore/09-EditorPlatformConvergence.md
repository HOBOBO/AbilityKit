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

本轮在 `Unity/Packages/com.abilitykit.*`（不含第三方）和 `Unity/Assets` 中静态筛查可解析的 `[MenuItem]` 路径，拆开执行与 validate 声明并另计 `Samples~`：

- 主体源码有 **128 条执行声明**：`demo.*` 为 59，其他 AbilityKit 包为 65，编辑器宿主 `Unity/Assets` 为 4；另有 19 条校验声明和 `Samples~` 中 6 条执行声明。包名是归属线索；65 不是“正式工具数”。
- 主体路径分布：`Tools/AbilityKit` 88、`Window/AbilityKit` 20、`Assets/AbilityKit` 12、`Assets/Create/AbilityKit` 3，另有 `GameObject/AbilityKit` 1、独立的 `AbilityKit` 根 3、其他 `Assets` 路径 1。静态声明数不是注册后的菜单数，不统计 `CreateAssetMenu`、Inspector 按钮、资产打开回调、Hub 注册或 `-executeMethod` 入口。

| 源码证据 | 问题与处置方向 |
|---|---|
| [HFSM 工作台](../../../Unity/Packages/com.abilitykit.hfsm/Editor/StateMachineEditorWindow.cs)同时有独立窗口入口与资产上下文入口 | 上下文打开目标不同，不按菜单数判定重复。保留有意义的入口，收敛到同一实现；登记旧菜单为兼容路径后再决定去留。 |
| [BattleScenario 组件创建](../../../Unity/Packages/com.abilitykit.battlescenario/Editor/BattleScenarioRootEditor.cs)走 `GameObject/AbilityKit` | 本来依赖场景上下文，不必为了被 Hub 查到而另开一条全局命令。 |
| [Trigger 项目配置菜单](../../../Unity/Packages/com.abilitykit.ability/Editor/Utilities/TriggerAuthoringProjectSetup.cs)位于 Ability 包，却构建 MOBA 默认目录并指向 MOBA 运行时输出 | 标为项目模板/发布接入待核；核对正式消费者和发布链后再定归属，不能因放在框架包就认定为框架能力。 |
| [临时表现 Prefab 生成器](../../../Unity/Assets/Scripts/Editor/MobaTemporaryPresentationPrefabGenerator.cs)、[飞艇占位资源生成器](../../../Unity/Assets/Scripts/Editor/MobaLubanAirshipPrefabGenerator.cs)、[Jinx 发布修补](../../../Unity/Packages/com.abilitykit.demo.moba.editor/Editor/Moba/JinxLivePresentationSync.cs)暴露为菜单，后者还带快捷键 | 写资产/发布类优先复核。先查 Console、Editor、文档、批处理与外部维护调用；再区分正式项目命令和历史修补。盘点未执行这些动作，未确认可删除。 |
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

**正式准入与验收：**

1. **归属和契约**：指出真实消费者、所属包、唯一实现、入口上下文、依赖及当前支持环境。正式包入口在包内文档说明，跨包规则留在本页。若已有注册能力满足需要则复用，不为分类另建 Hub 或全局服务。
2. **读写与回退**：打开窗口、发现、校验及 `CanExecute` 不写资产。普通编辑沿用领域的 Undo／保存契约；批量导入、覆盖、迁移、发布先展示输入、明确目标范围和差异，显式 Apply；批处理须等价地显式传写入意图。分清可撤销操作和非事务写入，后者说明失败恢复、版本控制和可重跑方式。报告输出遵循 [test-artifacts](../../../.claude/skills/test-artifacts/SKILL.md)；正式生成物仍按原发布契约落盘。
3. **加载与隔离**：模块注册和订阅需对称清理、可重复加载；导航不在每次重绘做全量反射或资产扫描。验证缺少可选包、无选中资产、编辑/播放切换和 Domain Reload 时的行为，确保 Player 不引用 Editor 程序集。共享底座不得为某个项目或英雄添加分支。
4. **最小证明**：纯分类/去重规则覆盖默认可见性与别名；窗口迁移在 Unity 验证真实菜单、有效和无效上下文、重载行为；写入与批处理增加预检不写、失败不静默和产物等价用例。变更前查 `MenuItem`、`ExecuteMenuItem`、`CreateAssetMenu`、Inspector／资产打开回调、快捷键、`-executeMethod`、测试、文档与外部承诺。测试只覆盖到哪一层，就只报告哪一层。

### 5.3 存量迁移批次

1. **已实现：隔离未分类入口**。行为树的编辑器和运行时观察、HFSM 的编辑器和运行时监视器显式进入导航；示例目录、面板和资产创建贡献不默认展示，已有注册及 Unity 菜单保持可用。反射清单默认折叠、展开才扫描、路径可复制但不可执行。仍需在完整工程验收工作台打开、无选中资产和 Domain Reload。
2. **再梳理写入项与 Demo**：按 §5.1 的项目模板、占位生成和发布修补逐条标注目标资产、现行调用和候选替代；保留被消费的正式入口，确认无调用才退役。涉及 MOBA 机制或生产/发布语义的变更按[领域治理](../../MobaMechanismGovernance.md)单独确认，本规范不构成批准。
3. **最后收敛兼容入口**：优先验证 BT/HFSM 现有登记与资产上下文；再按实际消费者处理其他包。每批记录旧入口 → 新入口、受影响调用、复验方式和回退方法；只对新增或迁移项约束，不以全仓命名改动作为前置条件。

验证范围：Unity `2022.3.62f1` 隔离工程运行 `AbilityKit.Editor.Platform.Tests`，准入默认隐藏／显式准入、发现路径排除 3 个新增用例通过；全套 25/28，通过同一工程重跑修改前源码复现了相同的 3 个既有断言失败。此证据只覆盖底座编译与测试，不代表在主工程实点过 Hub 或验证过 BT/HFSM 工作台。结果路径见本地 `local/Logs/editor-hub-20261125-071232535/`。

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

*文档版本：v1.4 | 最后更新：2026-11-25*
