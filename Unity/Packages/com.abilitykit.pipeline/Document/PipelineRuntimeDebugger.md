# Pipeline Runtime Debugger

## 1. 定位

Pipeline 调试体系分成两个单向依赖层：

| 层 | 职责 | 依赖 UnityEditor |
|---|---|---|
| Runtime | 执行 Pipeline，按需发送开始、Trace、结束等观测事件 | 否 |
| Editor | 订阅事件，保存活跃运行与历史，展示和控制运行实例 | 是 |

Editor 不替换 `PipelineRuntime.Registry` 或 `TraceRecorder`。业务运行时继续使用自己的注册表和追踪策略，Editor 通过 `PipelineDebugHooks` 旁路观察，因此默认 Runtime、自定义 Runtime 和多 World Runtime 都可以被同一个窗口查看。

调试回调由 Runtime 做异常隔离。Editor 观察者抛出异常不会改变 Pipeline 状态或阻止清理。

## 2. 打开窗口

1. 打开 `Window/AbilityKit/Pipeline Runtime Debugger`。
2. 进入 Play Mode 并启动任意 `AbilityPipeline<TCtx>` run。
3. 左侧选择运行实例，右侧查看 Overview、Phases、Trace 和 Context。

窗口可以在退出 Play Mode 后继续查看已保留的运行历史。默认最多保留 128 个已结束 run，每个 run 最多保留 2048 条 Trace；容量和刷新频率可从窗口右上角的设置菜单调整。

## 3. 窗口能力

### 3.1 Run 列表

- 按名称、Pipeline 类型、Run ID 或当前 Phase 搜索。
- 按 All、Active、History、Failed、Pinned 筛选，并直接显示运行、活跃、失败和置顶数量。
- 列表按 `ACTIVE`、`PINNED`、`RECENT` 分组；当前需要处理的运行优先于普通历史。
- 运行可置顶；置顶历史不会被容量裁剪或“清理历史”移除，取消置顶后重新遵循普通历史策略。
- `Follow` 自动选择最新运行。
- 状态色区分 Executing、Paused、Completed 和 Failed。
- Capture 开关控制是否记录后续新 run。
- 清理按钮只删除已结束历史，不影响活跃运行。
- 左右面板分隔线可拖动，宽度会作为当前用户偏好保留。
- 空状态会区分未进入 Play Mode、等待 run、暂停采集和筛选无结果。
- 右键运行可置顶、保存快照、复制摘要或直接跳到失败位置；双击 Phase 会打开仅显示该 Phase 的 Trace。

### 3.2 Overview

显示 Run ID、状态、当前 Phase、业务 elapsed time、墙钟时长、开始/结束时间，以及 Pipeline、Config、Context 的实际类型。

失败运行会优先显示真实异常消息，并提供 `Trace` 和 `Phase` 跳转；类型和 UTC 时间等技术信息默认折叠，减少日常排障噪声。

活跃 run 可以从窗口执行：

- Pause / Resume
- Cancel：在下一次 Tick 生效
- Interrupt：立即进入终态

控制入口只依赖非泛型 `IPipelineRunControl`，Editor 不反射调用具体 Pipeline 类型。
`Interrupt` 默认要求二次确认，避免把调试观察误操作成业务终止。

如果 pipeline、config、context 或 run 本身是 `UnityEngine.Object`，Overview 会提供只读对象引用和 Ping 操作；普通 C# 对象仍只显示类型，不改变弱引用生命周期。

### 3.3 Phases

`AbilityPipeline<TCtx>` 实现 `IPipelineDebugGraphProvider`，在 run 开始时提供不可变的节点、边和 `StructureId`。运行时结构始终是执行真相；Editor 只创建临时只读视图，不会在没有定义资产时生成或修改项目资产。

Graph 视图会区分 Sequence、Parallel、Conditional、Gate、普通 Phase 和其他 Composite。节点展示 Pending、Active、Completed、Skipped、Failed；Conditional 边还会显示命中、拒绝和当前选择分支。状态来自 run 的 `IPipelineDebugStateProvider`，终态会在池化阶段列表释放前缓存，因此历史 run 仍能保留复合节点和条件结果。

每个节点使用结构路径形式的稳定 `NodeKey`，例如 `0/1/0`。`NodeKey` 用于结构、状态、边和布局关联；`PhaseId` 继续用于业务查询和 Trace，不能代替节点键，因为不同分支可能使用相同 Phase ID。

工具栏支持 `Graph / Tree` 切换：

- Graph 自动进行层级布局；中键拖动或 `Alt + 左键` 平移，滚轮缩放。
- Fit 按钮适配全部节点；靶心按钮聚焦 Active 或 Failed 节点。
- 单击选择节点，`Trace` 跳转到对应 Phase；双击直接进入 Phase Trace。
- Tree 是紧凑层级回退，同样使用运行时节点状态，不依赖 Graph 绘制能力。

Pipeline 或 Config 可以选择实现 `IPipelineDebugGraphLayoutProvider` 提供 authored 坐标。常见做法是由 ScriptableObject 定义实现该接口，但 Runtime 协议本身仍是纯 C#，不依赖 `ScriptableObject`。只有布局的 `StructureId` 与运行时图完全一致时才会采用坐标；旧 SO 或结构已变更时窗口会显示警告并自动回退布局，绝不会用资产覆盖运行结构。

### 3.4 Trace

Trace 支持：

- All、Lifecycle、Phases、Errors、Control 分类。
- 按 Phase ID、事件类型和消息搜索。
- 选择并复制单条事件。
- 在列表下方查看选中事件的完整消息。
- 在 UTC 时间和从 run 开始计算的相对时间之间切换。
- 清理当前 run 的 Trace。
- 从 Overview 的失败摘要或 Phase 树进入 Trace 时，自动设置对应失败/Phase 筛选。

时间使用事件原始 `UtcTime`，不会在 Editor 复制到 ring buffer 时重新生成。

### 3.5 Context

Editor 在活跃期按固定频率读取 Context，并在结束通知到达、Context 释放前保存最后一份文本快照。内容包括：

- `PipelineState`、`CurrentPhaseId`、暂停和中断状态。
- `ElapsedTime`、`AbilityInstance`。
- `SharedData`。
- Context 公开的业务属性，最多 64 项。

快照只保存名称和值的文本，不强引用业务对象。属性读取异常会降级为异常类型占位符，不中断调试采集。

Context 页以 `Field | Start | Current` 展示运行开始与当前/结束快照，变化字段会高亮。`Changed` 开关只显示变化字段；搜索会同时匹配字段名、初始值和当前值。复制变化字段时使用 `before -> after` 保留差异语义。

## 4. 编辑器状态与会话资产

调试器使用两种不同的 ScriptableObject 状态，不能与实时对象仓库混用：

| 类型 | 存储位置 | 内容 | 是否持有运行时对象 |
|---|---|---|---|
| `PipelineDebuggerUserState` | `UserSettings/AbilityKit/PipelineDebuggerState.asset` | 当前用户的筛选、搜索、面板宽度、刷新频率、容量和确认选项 | 否 |
| `PipelineDebugSessionAsset` | 用户显式选择的项目资产路径 | 单个 run 的图结构、节点状态、Trace，以及 Context 初始/最终 DTO 快照 | 否 |

`PipelineDebuggerUserState` 使用 `ScriptableSingleton`，属于工作环境偏好，不应作为团队共享的 Pipeline 定义资产。只有存在明确的团队级调试策略时，才应另外增加共享 profile，而不是把用户布局提交到项目中。

工具栏的保存按钮会创建格式 v3 的 `PipelineDebugSessionAsset`。除原有 run、Trace 和 Context DTO 外，v3 还保存 `NodeKey`、节点类型、摘要、执行状态、选择分支、条件结果、图边、`StructureId`、布局来源，以及存在时的 authored 坐标。该资产的 Inspector 是只读诊断视图，可在退出 Play Mode 或跨机器后检查，但它不是回放引擎，也不会重新连接原 run。

实时 owner、pipeline、config、run 和 context 始终只存在于 `EditorPipelineRegistry` 的弱引用中。Unity 无法可靠序列化任意 C# 运行对象，把这些引用写进 ScriptableObject 还会延长生命周期并造成已销毁对象、Domain Reload 和内存泄漏问题。

## 5. Runtime 观测协议

`PipelineDebugHooks` 提供：

| 事件 | 时机 |
|---|---|
| `OnRunStartedDetailed` | Run 已创建并注册，首条 RunStart Trace 之前 |
| `OnTrace` | Runtime 写入一条 Trace 时 |
| `OnRunEnded` | Run 进入终态、释放 Context 之前 |

`PipelineRunStartedData` 携带 owner、pipeline、config、非泛型 control 和 context。观察者只能把这些对象视为瞬时引用；Editor 实现统一使用 `WeakReference`，避免工具延长业务对象生命周期。

`IPipelineLifeOwner.OwnerId` 由进程级生成器分配，不再使用对象哈希，避免不同 Context 泛型或哈希碰撞导致调试记录互相覆盖。

图诊断使用三个互相独立的可选协议：

| 接口 | 作用 |
|---|---|
| `IPipelineDebugGraphProvider` | 捕获只读节点、边和结构哈希 |
| `IPipelineDebugStateProvider` | 捕获当前或已缓存的节点执行状态与条件结果 |
| `IPipelineDebugGraphLayoutProvider` | 提供与 `StructureId` 匹配的可选 authored 坐标 |

旧的 `IPipelineDebugStructureProvider` 仍保留兼容；Editor 会为其构造基础图，但新实现应优先提供完整图协议。

## 6. 自定义 Pipeline 接入

继承 `AbilityPipeline<TCtx>` 的实现无需额外代码，会自动发送完整观测事件和阶段结构。

完全自定义的 Run 若希望进入统一调试窗口，需要：

1. 实现 `IPipelineLifeOwner`。
2. 如需窗口控制，实现 `IPipelineRunControl`。
3. 在开始、Trace 和结束边界调用 `PipelineDebugHooks` 对应通知。
4. 如需完整阶段图，实现 `IPipelineDebugGraphProvider`；仅有旧阶段树时仍可实现 `IPipelineDebugStructureProvider`。
5. 如需复合节点实时状态，实现 `IPipelineDebugStateProvider`。
6. 如需 SO 或其他定义提供的固定坐标，实现 `IPipelineDebugGraphLayoutProvider`，并保证 `StructureId` 精确匹配。

业务代码不应引用 `AbilityKit.Pipeline.Editor`。

## 7. 性能与线程边界

- 没有观察者时，DebugHooks 只做空委托判断。
- 阶段树只在 run 开始且 Editor 观察者存在时读取。
- Context 反射和历史 ring buffer 全部位于 Editor 程序集。
- Hook 可能从 Pipeline 所在线程触发；窗口只在 `EditorApplication.update` 主线程调用 Unity GUI API。
- 默认采集适合开发调试，不是生产遥测或确定性回放格式。

## 8. 文件入口

| 文件 | 作用 |
|---|---|
| `Runtime/Debug/PipelineDebugHooks.cs` | Runtime 到观察者的安全通知入口 |
| `Runtime/Debug/PipelineRunDebugData.cs` | 开始/结束数据和阶段结构协议 |
| `Runtime/Interface/IPipelineRunControl.cs` | 非泛型运行控制面 |
| `Editor/PipelineEditorInitializer.cs` | 安装和卸载 Editor 观察者 |
| `Editor/Debug/EditorPipelineRegistry.cs` | 活跃运行、历史、Context 和 Trace 存储 |
| `Editor/Debug/PipelineRuntimeDebuggerWindow.cs` | 调试窗口 |
| `Editor/Debug/PipelineDebuggerUserState.cs` | 当前用户的窗口和采集偏好 |
| `Editor/Debug/PipelineDebugSessionAsset.cs` | 显式保存的只读 run 快照资产 |
