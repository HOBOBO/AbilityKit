# Ability-Kit BaseEditor 基础编辑器工具模块开发设计文档

## 一、文档定位

本文是 `com.abilitykit.base.editor` 的 package canonical 文档。该包现在包含两条必须区分的编辑器基础设施路线：

1. `Editor/Platform`：新建的统一 Editor Platform/Core，是行为树、HFSM、Pipeline、Trigger 等编辑器共享基础设施的正式入口；
2. `Editor/Framework` 及其他历史目录：保留 `PlugableWindow<TData, TConfig>`、`WindowBuilder<TData, TConfig>`、旧导出器、Pool Monitor、ActionEditor 示例和 GameplayTag 遗留工具，用于兼容既有消费者。

Platform 统一的是编辑器工程能力，不统一领域模型、运行时语义或画布技术。行为树可继续使用 GraphView，HFSM 可继续使用既有分层画布，Pipeline Runtime Debugger 保持只读观察，Trigger authoring model 仍由 Ability 包拥有。

## 二、程序集与依赖边界

`AbilityKit.Editor.Platform.asmdef` 是独立 Editor-only 程序集。它可以依赖 Unity Editor/UI 基础能力，但不得引用 BehaviorTree、HFSM、Pipeline、Trigger 或 Ability 的领域程序集。

依赖方向固定为：

```text
领域 Editor 程序集 -> AbilityKit.Editor.Platform
AbilityKit.Editor.Platform -X-> 任一领域 Editor/Runtime 程序集
```

领域诊断路径、节点类型、运行时快照和资产结构必须由消费方通过 adapter、callback 或贡献对象接入。Platform 不解析 `project.modules[i]`、BT NodeId、HFSM transition path 或 Pipeline PhaseKey 等领域协议。

历史 `Editor/Framework` 不等同于 Platform。新功能优先采用 Platform；Legacy API 在兼容期内保留，但不再作为跨编辑器治理的扩展中心。

## 三、Editor Platform 能力

### 3.1 Core 与模块注册

Platform 提供服务注册表、模块描述符、模块注册表和显式贡献模型。模块可以注册服务、命令、菜单或面板贡献，并通过 `IDisposable` registration handle 对称注销。

生命周期约束：

- 重复注册必须显式失败或由调用方先注销；
- Domain Reload、窗口关闭和模块禁用时必须释放 registration handle；
- Platform 只编排贡献，不反向了解领域对象；
- 消费方负责 Unity 事件、运行时 registry 和窗口资源的完整退订。

### 3.2 Localization

Localization 使用稳定文本 key 和模块化资源源，支持：

- 用户语言覆盖；
- 项目默认语言；
- 英文 fallback；
- key fallback；
- 参数格式化；
- `LanguageChanged` 即时刷新通知；
- registration handle 注销资源源。

语言偏好属于用户态或项目设置，不能写入运行时定义资产。消费窗口订阅 `LanguageChanged` 后必须在销毁时退订。

### 3.3 Diagnostics

通用诊断模型包含：

- stable code；
- severity；
- message；
- domain path；
- 可选 Unity target；
- 可选 locate/fix action；
- collection 计数、过滤和搜索。

Platform 提供 IMGUI/UI Toolkit 的诊断呈现基础能力。领域 validator 仍是诊断真相来源，领域 Editor 负责把 validator 结果适配成 Platform diagnostics，并决定路径如何定位到节点、资产或 Inspector。

### 3.4 UserState 与 ProjectSettings

状态分为两层：

- 用户态：使用命名空间化 `EditorPrefs`，保存窗口选择、折叠、筛选、布局等个人偏好；
- 项目态：使用 `ProjectSettings` 资产保存团队应共享和版本控制的默认值，例如默认语言或 catalog 选择。

运行时定义、authoring 内容和导出目标不能偷渡进用户态存储。

### 3.5 Commands

命令系统使用稳定 command id、label key、执行回调和 `CanExecute`。Toolbar、菜单和快捷键应复用同一命令，而不是各自复制业务逻辑。

命令 id 必须跨同一 registry 唯一；窗口销毁时释放所有 command registrations。命令层只表达用户意图，实际保存、导出、定位和运行时操作仍由领域服务完成。

### 3.6 UI 基元

Platform 提供可组合而非强制统一的 UI Toolkit/IMGUI 基元，包括 Toolbar、Search、Splitter、Tabs、EmptyState、StatusBadge、DiagnosticsList 和 Source Sync Card model。

这些控件不规定画布技术，也不要求领域窗口继承统一基类。窗口可以逐步接入，而无需重写现有 GraphView、IMGUI 或 UI Toolkit 交互层。

### 3.7 DocumentSession

通用 DocumentSession 管理：

- 当前文档与 serializer；
- dirty 生命周期；
- 只读保护；
- 有界 undo/redo；
- replace/load/save 后的 baseline；
- 文档切换前的状态判断。

领域层负责定义文档复制、序列化和持久化语义。运行时观察会话必须设为只读，不能因为复用 DocumentSession 而获得 authoring 写权限。

### 3.8 Source Sync

Source Sync 提供规范化状态分类和操作策略，包括 InSync、LocalChanged、SourceChanged、Conflict、Untracked、SourceMissing、InvalidSource，以及 import/export 的 force 判定。

Platform 不读取领域 JSON，也不写 Unity 资产。消费方负责 hash、codec、Undo、dirty、资产 baseline 和刷新；Coordinator/Policy 只统一冲突决策，避免 BT、Trigger 等编辑器各自发明状态机。

### 3.9 Export、Report 与原子写盘

Export 基础设施提供 job/result/report 状态，用于统一 Exported、Unchanged、Skipped/Error 等结果表达。`EditorAtomicFileWriter` 是 canonical Editor-only 文件写入原语，提供：

- UTF-8 no BOM 默认编码；
- 内容一致时不改写并返回 `Unchanged`；
- 临时文件写入；
- `File.Replace`；
- 不支持 replace 或发生 IO 异常时的 move + backup fallback；
- finally 清理 `.abilitykit.tmp.*` 和 `.abilitykit.bak.*`。

领域 exporter 负责 schema、路径、校验和产物语义。Legacy Json/Csv Exporter 不会自动获得上述 export pipeline 能力。

## 四、Legacy 可插拔窗口框架

### 4.1 PlugableWindow

`PlugableWindow<TData, TConfig>` 提供列表、搜索、配置筛选、选择、详情、插件回调和 Json/Csv Exporter 扩展点。

窗口实际数据来源仍是可重写的 `LoadData()`。`Initialize(dataSource, plugins)` 的 `dataSource` 当前没有成为窗口的 canonical 数据源；外部数据消费者必须重写 `LoadData()` 或使用自己的适配层。

窗口销毁会通知插件 `OnDestroy()`，但没有与之对称的完整 Initialize/Enable/Disable 生命周期契约。插件订阅 Editor 事件或持有资源时仍需自行清理。

### 4.2 WindowBuilder

`WindowBuilder<TData, TConfig>` 保留链式 API 兼容，但标记为 Experimental/Legacy。其数据加载回调仍是 `Action<IEnumerable<TData>>`，不是返回数据的 `Func<IEnumerable<TData>>`；`Build()` 传入的局部数据无法形成可靠的数据注入链。

当前兼容测试只锁定：

- Fluent API 返回原 builder；
- `Build()` 保持窗口泛型类型；
- Config 创建与 Validate；
- `PlugableWindow.Initialize()` 后仍由 `LoadData()` 提供数据；
- 插件按 priority 收到数据加载通知。

这些测试不代表 Data、DrawDetail、Filter 等链式语义已经生产化。新编辑器不得基于未验证语义扩展 WindowBuilder；若未来修复，必须保持兼容或提供显式迁移版本。

### 4.3 Legacy 导出器

`JsonExporter<T>` 和 `CsvExporter<T>` 是格式转换工具，不负责稳定文件名、schema migration、原子写入、失败回滚或多态序列化证明。需要正式内容管线的消费者应采用 Platform export/report/atomic writer，并保留领域 validator 和 codec。

## 五、其他历史工具边界

### 5.1 Pool Monitor

`PoolMonitorWindow` 从 `Pools.GetDebugSnapshots()` 拉取只读快照，在 Play Mode 展示池状态。它具备对称的 Editor update 订阅/退订和节流刷新，但不改变 Pool 所有权，也不负责释放池内对象。

### 5.2 ActionEditor 示例

`Editor/ActionEditorImpl` 是项目侧示例和预览接入，不是 Platform 生命周期协议。其场景切换、Sampler 支持范围和第三方 ActionEditor 回调必须逐项验证，不能从目录存在推导为完整生产能力。

### 5.3 GameplayTag 遗留所有权

`Editor/GamplayTag` 是历史兼容目录。独立 `com.abilitykit.gameplaytags/Editor` 拥有 authoritative model 和正式编辑器职责；新功能不得同时修改两套实现，退役前需明确菜单、资产和导出格式兼容策略。

## 六、已接入消费者与治理结论

当前 Platform 的渐进消费者包括：

- BehaviorTree：Localization、Commands、Diagnostics、DocumentSession、Source Sync、Export Report 和 atomic runtime export；
- HFSM：UserState、Commands、Platform Diagnostics adapter、Export Action Registry；
- Pipeline Runtime Debugger：通用编辑器状态/模型拆分能力，仍保持只读运行时观察；
- Trigger Workspace：Commands、Localization、Diagnostics、Source Sync UI 和 atomic source/runtime export。

统一基础设施不改变领域所有权：

- BT 保持 descriptor-driven GraphView 和 child → parent 边语义；
- HFSM Next Definition/Legacy importer 仍由 HFSM 拥有；
- Pipeline Authoring Graph 在稳定定义和 round-trip 协议出现前不实施；
- Trigger authoring assets、validator、codec 和 runtime exporter 当前仍由 Ability 包拥有，模型下沉前不把完整 UI 强迁到 Triggering Editor。

## 七、测试证据与限制

当前已有：

- `AbilityKit.Editor.Platform.Tests`：覆盖服务/模块注册、Localization fallback、Diagnostics、Commands、State、UI model、DocumentSession、Source Sync、Export/atomic writer 等源码；
- `AbilityKit.Base.Editor.Tests`：覆盖 Legacy `WindowBuilder` / `PlugableWindow` 的兼容边界；
- BehaviorTree、HFSM、Pipeline、Trigger 各自的 Editor 测试源码和定向程序集编译门禁。

本轮相关项目已通过定向 `dotnet build` / `dotnet msbuild` 源码编译且为 0 errors；仍可能存在既有程序集冲突、deprecated 或未使用字段 warnings。

重要限制：`dotnet build` 只证明生成的 C# 项目可编译，不等于 Unity Test Runner 已执行。新增 EditMode/NUnit 测试在 Unity Test Runner 实际运行前，只能称为“测试源码已编译”，不能称为“测试通过”。Domain Reload、语言即时切换、布局恢复、Unity Undo/Redo、诊断定位和真实 AssetDatabase 导入仍需 Unity 侧验收。

| 等级 | 状态 | 说明 |
|---|---|---|
| E0 | 已具备 | Platform 与 Legacy 源码、asmdef 和文档存在 |
| E1 | 已具备 | Platform 服务、命令、诊断、状态、UI、会话、同步和导出 API 可被领域编辑器调用 |
| E2 | 已具备 | BT、HFSM、Pipeline、Trigger 已按领域边界渐进接入 |
| E3 | 部分具备 | 专项测试源码和定向编译存在；本轮未执行 Unity Test Runner |
| E4 | 部分具备 | 领域侧存在 golden/export 测试；总体验收矩阵仍待 Unity 执行 |
| E5 | 待建立 | 最终 CI、Unity 批量测试、发布与回滚责任仍需正式门禁 |

## 八、源码阅读路径

1. `Editor/Platform/AbilityKit.Editor.Platform.asmdef`：独立 Platform 程序集边界；
2. `Editor/Platform/Core/`：服务、模块、贡献和平台上下文；
3. `Editor/Platform/Localization/`：本地化服务和资源源；
4. `Editor/Platform/Diagnostics/`：结构化诊断；
5. `Editor/Platform/State/`：用户态与项目态存储；
6. `Editor/Platform/Commands/`：稳定命令注册与执行；
7. `Editor/Platform/UI/`：IMGUI/UI Toolkit 组合基元；
8. `Editor/Platform/Documents/`：DocumentSession；
9. `Editor/Platform/Synchronization/`：Source Sync 分类和策略；
10. `Editor/Platform/Export/`：Export Report/Job 和 atomic writer；
11. `Tests/Editor/EditorPlatformCoreTests.cs`：Platform 测试源码；
12. `Editor/Framework/Core/PlugableWindow.cs` 与 `Editor/Framework/Layout/WindowBuilder.cs`：Legacy API；
13. `Tests/Framework/WindowBuilderCompatibilityTests.cs`：Legacy 兼容测试源码。

## 九、后续治理顺序

1. 由 Unity 正式刷新新增源码、asmdef reference 和 `.meta`，移除临时 csproj validation target；
2. 执行 Platform 与四类编辑器相关 Unity EditMode tests；
3. 建立依赖无环、Domain Reload、语言即时切换、布局恢复、诊断定位、同步冲突和原子导出的总体验收门禁；
4. 完成 `git diff --check`、相关项目定向构建和 `dotnet build Unity.sln --no-restore -m:1`，保持 0 errors；
5. 继续减少硬编码 UI 文本，但不以本地化为由改写领域语义；
6. 仅在有消费者和迁移测试时修复 Legacy WindowBuilder，不把它重新定义为 Platform；
7. 为旧 GameplayTag、ActionEditor 示例和空 Preview/Sampler 单独制定迁移或退役计划。
