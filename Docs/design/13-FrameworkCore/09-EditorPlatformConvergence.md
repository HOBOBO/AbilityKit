# 09-编辑器平台收敛与统一入口设计

> 文档类型：Canonical 设计 + 演进计划
> 事实基线：2026-09-07

## 一、能力定位

本文是 `com.abilitykit.base.editor` 的 **Editor Platform 跨模块收敛策略**。它回答两个问题：

1. pipeline、HFSM、行为树、BattleFlow、Trigger Authoring、Protocol、Trace 等编辑器的导出、加载、界面、基础功能、运行时调试、校验、项目管理里，**哪些该下沉到 Editor Platform、哪些必须留在各模块**；
2. 各窗口 `[MenuItem]` 目前散在 `Window/AbilityKit/*`、`Tools/AbilityKit/Framework/*`、`Tools/AbilityKit/Demos/*`、`Assets/AbilityKit/*` 四处，**统一入口如何补上**。

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
| **BattleFlow** | 无 | — | 全套自造；`BattleFlowCodec` 裸写 JSON、`Stack<string>` 重造 undo |
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

1. **三个包完全没接底座**：`pipeline`/`battleflow`/`protocol.editor` 的 Editor asmdef 都不引用 `AbilityKit.Editor.Platform`，导出/加载/窗口/调试/校验全套自造。三处典型：`BattleFlowCodec.Save` 在 runtime 里 `File.WriteAllText` 写 JSON（本应走 `EditorAtomicFileWriter`）；`BattleFlowWindow` 用 `Stack<string>` 快照重造 undo（`BattleFlowWindow.cs:31,248-266`，本应走 `EditorDocumentSession`）；`ProtocolEditorWindow` 用单个 `_dirty` + `DisplayDialogComplex` 重造文档切换（`ProtocolEditorWindow.cs:932-952`）。

2. **Ability 包两代人并存**：旧 Trigger 导出链约 7 个手写 `File.WriteAllText`/`JsonConvert` 的 writer（`AbilityTriggerJsonExporter`、`ReadableTriggerPlanExporter`、`TriggerPlanJsonSplitter`、`SourceJsonExporter`、`AbilityTriggerJsonImporter` 等），把同一份 trigger plan 图序列化成 **5 种 JSON 表示**、写进同一目录 `Resources/ability/`；新 Trigger Authoring 子系统才改用 `EditorAtomicFileWriter` + `EditorSourceSync`。这是全库单点重复最重的地方。

3. **HFSM 手写平行导出抽象**：`Editor/Export/Interfaces/*`（`IGraphExporter`/`IGraphDataExtractor`/`IJsonSerializer`/`ExportResult`）+ `Editor/Export/Descriptors/*`（10 个 DTO）+ 自造 `ExtensionRegistry`，等价于把 `EditorExport` 的 Job/Report 又造一遍——只复用了 `EditorAtomicFileWriter`。

4. **Trace 挂在弃用层**：`TraceTreeWindow : PlugableWindow<...>`（`trace/Editor/Windows/TraceTreeWindow.cs:12`）；且 Framework 示例 `WindowExamples.cs:160` 注册了 `Window/AbilityKit/Ability List`，与 `ability/Editor/Windows/AbilityListWindow.cs:22` **菜单路径撞车**。

5. **模块内部自重复**：pipeline 的 `EditorPipelineTraceRecorder`（`EditorPipelineTraceRecorder.cs:14`）是独立 ring buffer，与 registry 内嵌 trace 存同一份数据，registry 不用它。

6. **BT 虽是标杆也有尾巴**：menu/panel 既注册进 Platform，又用裸 `[MenuItem]` 重复声明（`DebugObservationWindow.cs:62`、`AuthoringProjectAssetInspector.cs:171`）；项目级 "Validate All" 不走 `EditorDiagnostic`、直接弹字符串对话框。

## 五、统一入口：当前真正缺的那块

### 5.1 现状（缺口）

注册机制已建好，但**没有任何窗口消费它**：

- `AbilityKitEditorPlatform` 暴露 `Modules`/`Menus`/`Panels`/`Commands`/`Services`（`Platform/Core/AbilityKitEditorPlatform.cs:44-49`）。
- BT 的 `Bootstrap/EditorModule.cs:116` 把自己注册进 `Modules`，并在 `OnRegister` 注册两个 menu 两个 panel（`:43-68`）。
- 全库 grep 表明：**除了 BT 的 Register 调用、底座自身定义和测试，没有第二个消费者去遍历 `Menus.Items` / `Modules.Modules` 渲染任何 UI**。

因此 130+ 个窗口靠 `[MenuItem]` 散在 4 个菜单根下，互相无索引、无跳转、无"框架有哪些能力"的总览。这是"窗口分布乱"的直接根因。

### 5.2 目标设计

新增 **AbilityKit Hub（统一入口窗口）**，放在 Platform 层，读 `Modules`/`Menus`/`Panels`/`Commands`/`Services`，渲染：

1. **已注册模块**（`Modules.Modules`）：按 `Descriptor.Order` 排序，展示模块名（本地化）与它的 menu/panel 贡献；
2. **已注册菜单/面板**（`Menus.Items`/`Panels.Items`）：按菜单根分组，可点击打开；
3. **已注册命令**（`Commands.Commands`）：展示 label + `CanExecute` 状态；
4. **散落窗口（待迁移）**：反射扫描已加载程序集里 `[MenuItem]` 且路径属于 `Window/AbilityKit/`、`Tools/AbilityKit/`、`Assets/AbilityKit/` 的静态方法，去重已注册路径后列出，标记"待迁移"，通过 `EditorApplication.ExecuteMenuItem(path)` 打开。

第 4 项让 Hub **立刻可用**（现在就能跳转所有窗口），同时把迁移积压可视化——这既是导航器，也是迁移进度的看板。

## 六、目标架构与收敛顺序（演进计划）

### 6.1 目标端态

所有提供编辑/调试能力的框架包都成为一个 `IEditorModule`，对称注册 menu/panel/command/localization/diagnostic；导出走 `EditorExport`+`EditorAtomicFileWriter`；文档会话走 `EditorDocumentSession`；源同步走 `EditorSourceSync`；校验适配 `EditorDiagnostic`；个人偏好走 `EditorPrefsUserStateStore`。画布、IR schema、校验规则、快照模型留在各模块。Hub 是唯一导航入口，`[MenuItem]` 只作为注册的退化回退存在。

### 6.2 收敛顺序（按风险从低到高、收益从高到低）

1. **做统一入口（Hub）**——消费已有注册表 + 反射发现散落窗口。零新概念，直接解决"乱"，并立下"新窗口必须注册"的规约。
2. **BT 收尾 + 导出报告模型进 HFSM/Ability**——HFSM 已接入统一入口、导出报告和运行时调试导航；后续在 golden/roundtrip 测试保护下删除 Legacy Archive 平行 exporter 抽象，并收敛 Ability 旧导出链。BT 清掉重复 `[MenuItem]` 和绕过 `EditorDiagnostic` 的 Validate All。
3. **Pipeline/BattleFlow/Protocol 接基础件**——至少接 `EditorAtomicFileWriter` + `EditorDocumentSession`（替换 `BattleFlowCodec` 裸写、`_undoStack`、`_dirty` 对话框）+ `EditorDiagnostic`。纯减重复，不动各自画布和 IR。
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
| 未接底座样例 | `com.abilitykit.pipeline/Editor/Debug/`、`com.abilitykit.battleflow/Editor/`、`com.abilitykit.protocol.editor/Editor/` |
| 包内 canonical | `com.abilitykit.base.editor/Document/BaseEditor基础编辑器工具模块开发设计文档.md` |
| 领域边界既有定义 | `Docs/design/13-FrameworkCore/07-BehaviorTreePackageDesign.md` §10.1、`08-HfsmDeterministicRuntimeEvolution.md` "Editor Platform 渐进接入" |

## 八、事实状态与证据等级

- **规范约束**：依赖方向 `领域 Editor -> Platform`、`Platform -X-> 领域`；导出/校验/同步/会话/命令/本地化的共享边界见 §三。
- **当前实现**：Platform Hub 统一入口已实现；BT 与 HFSM 已注册 Module/Menu/Panel，Ability 部分接入，Pipeline/BattleFlow/Protocol/Trace 仍主要通过散落窗口发现进入（§2.2、§5）。
- **示例策略**：HFSM 的 `ExtensionRegistry` 导出抽象、Ability 旧导出链，都是"尚未收敛"的领域自造实现，不是底座能力。
- **已知限制**：`dotnet build` 只证明可编译，不等于 Unity Test Runner 已执行；Domain Reload、语言切换、布局恢复、诊断定位、真实 AssetDatabase 导入仍需 Unity 侧验收。

| 等级 | 状态 | 说明 |
|---|---|---|
| E0 | 已具备 | Platform/Legacy 源码、asmdef、包内 canonical 存在 |
| E1 | 已具备 | Platform 服务/命令/诊断/状态/会话/同步/导出 API 可被调用 |
| E2 | 部分具备 | Hub 与 BT/HFSM 正式入口已接；Ability 部分接入，Pipeline/BattleFlow/Protocol/Trace 未接 |
| E3 | 部分具备 | Platform/领域 Editor 测试源码 + 定向编译存在；本轮未跑 Unity Test Runner |
| E4 | 待建立 | Hub 与各领域导出/同步/诊断的 Unity 侧验收矩阵未建立 |
| E5 | 待建立 | 统一入口与迁移门禁未挂 CI |

## 九、风险与约束

- **画布不能下沉**：Hub 与 Platform 只做壳，不提供"万能图编辑器"；强行统一 GraphView/分层画布会让领域编辑器丧失表达力并产生大量迁移成本。
- **运行时调试最易过度抽象**：四个 debugger 的快照模型与可视化各不相同，先做骨架、后谈统一；在下沉前必须有两个以上非同构消费者证明骨架稳定（对齐仓库"能力下沉五条"）。
- **Legacy 兼容**：`PlugableWindow`/`WindowBuilder` 在兼容期内保留，但新功能不得再扩展；`trace` 迁移前不得触碰 `WindowExamples` 撞车菜单以外的 Legacy 语义。
- **迁移顺序不可逆**：删 HFSM 平行导出抽象、收敛 Ability 旧导出链前，必须有 golden/roundtrip 迁移测试锁定产物等价，避免"减重复"变成"丢格式"。

---

*文档类型：Canonical 设计 + 演进计划 | 事实基线：2026-09-07 | 证据等级：E0-E2（局部 E3 源码编译，Unity 未重跑）*

*文档版本：v1.2 | 最后更新：2026-09-07*
