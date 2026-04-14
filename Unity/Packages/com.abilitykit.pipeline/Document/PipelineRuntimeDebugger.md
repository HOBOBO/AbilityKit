# Pipeline Runtime + Debugger（Run-centric）

## 适用范围

本文档描述**以 Run 为中心**的 Pipeline 调试体系结构，以及如何使用编辑器调试窗口快速定位问题。

内容包含：

- **运行时（Runtime）**：`AbilityPipeline<TCtx>` 的运行实例（run）、`AbilityPipelineLiveRegistry`、`PipelineRunTrace`。
- **编辑器（Editor）**：`AbilityPipelineRunDebuggerWindow` 的使用方式，Trace 查看/筛选/跳转，以及 Graph 同步与高亮。


## 快速上手（3 分钟）

### 运行时侧（你需要确保能注册到 LiveRegistry）

- 使用 `IAbilityPipeline<TCtx>.Start(config, context)` 启动一次 run。
- 在默认实现 `AbilityPipeline<TCtx>` 中，会在编辑器模式下自动：
  - `AbilityPipelineLiveRegistry.RegisterRun(...)`
  - 在每次 `Tick()` 时 `AbilityPipelineLiveRegistry.TouchRun(...)`
  - 在结束时 `AbilityPipelineLiveRegistry.UnregisterRun(...)`

如果你是自定义 pipeline run，请确保也能在 `UNITY_EDITOR` 下写入上述注册/触达逻辑。

### 编辑器侧（打开窗口 + 绑定图）

- 进入 **Play Mode**。
- 打开 `AbilityPipelineRunDebuggerWindow`。
- 在 `Running Run` 下拉中选择要观察的 run。
- 若要可视化/高亮：
  - 将一个 `PipelineGraphAsset` 拖到 `Graph Asset`
  - 点击 `Sync Graph From Selected Run` 从当前 run 同步生成/更新图

### 常用操作

- **事件 -> 图**：点击 Trace 的某条 phase 事件（`PhaseStart/PhaseComplete/PhaseError`），会 focus 并居中到对应节点。
- **图 -> 事件**：点击 `Locate Trace For Focus`，会定位到当前 focus（或 current phase）对应的最近事件。


## 运行时概览（Runtime Overview）

### 关键概念

- `IAbilityPipeline<TCtx>`
  - 通过 `Start(config, context)` 创建 run。

- `IAbilityPipelineRun<TCtx>`
  - 外部驱动执行的句柄。
  - 主要操作：
    - `Tick(float deltaTime)`
    - `Pause()` / `Resume()`
    - `Interrupt()` / `Cancel()`

- `AbilityPipeline<TCtx>`
  - 默认 pipeline 实现。
  - 内部有私有 `Run` 实现，持有 run 的执行状态。


### 调试数据模型（Debug runtime data model）

调试系统以 run 为中心，依赖以下运行时数据：

- **Selected run（全局焦点）**
  - `AbilityPipelineLiveRegistry.SelectedRun`：调试窗口的全局 focus。

- **Live entries（活跃条目）**
  - `AbilityPipelineLiveRegistry.Entry` 内部用 `WeakReference` 保存：
    - `Config`
    - `Pipeline`
    - `Run`
  - 同时保存：
    - 最新 `Snapshot`
    - 编辑器专用 `PipelineRunTrace`

- **Snapshot（快照）**
  - `AbilityPipelineLiveRegistry.Snapshot`：
    - `State`
    - `CurrentPhaseId`
    - `PhaseIndex`
    - `IsPaused`

- **Trace（事件流）**
  - `PipelineRunTrace`：一个有容量上限的 ring buffer，存 `PipelineTraceEvent`。
  - 常见类型：
    - `RunStart`, `RunEnd`
    - `PhaseStart`, `PhaseComplete`, `PhaseError`
    - `Tick`


### 数据如何写入（How data gets populated）

在默认运行时实现中：

- run 创建时：
  - 调用 `AbilityPipelineLiveRegistry.RegisterRun(owner, config, this)`（仅编辑器）
  - 追加 `RunStart`

- 每次 `Tick()`：
  - `AbilityPipelineLiveRegistry.TouchRun(this)` 刷新 snapshot（仅编辑器）
  - 追加 `Tick`

- 阶段边界：
  - 追加 `PhaseStart` / `PhaseComplete` / `PhaseError`

- run 结束时：
  - `AbilityPipelineLiveRegistry.UnregisterRun(this)`（仅编辑器）


### 生命周期与安全性（Safety / lifetime behavior）

- Registry 对运行时实例的引用均为弱引用（`WeakReference`），避免 editor 工具反向“延长对象生命周期”。
- Registry 会清理已经被 GC 的 run。
- 若 `SelectedRun` 变为无效（unregister 或 GC），Registry 会自动回退选择一个仍存活的 run（通常是“最新注册的”）。


## 编辑器工具（Editor Tooling）

### 窗口入口

- `AbilityPipelineRunDebuggerWindow`（仅 Play Mode 可用）

窗口数据来源：

- `AbilityPipelineLiveRegistry.GetEntries()`
- `AbilityPipelineLiveRegistry.SelectedRun`


### Run 列表行为

- 下拉框会列出当前活跃 runs。
- `Follow Selected Run`：
  - 窗口选择会跟随 `AbilityPipelineLiveRegistry.SelectedRun`。

- `Lock Global SelectedRun`：
  - 窗口会强制 `AbilityPipelineLiveRegistry.SelectedRun` 与当前下拉选择保持一致。


### Trace 体验（Trace UX）

- Trace 筛选：
  - 类型多选 + 预设：`All` / `Lifecycle` / `Errors` / `Ticks`
  - 文本筛选会匹配 `PhaseId` 与 `Message`

- 点击 Trace 行：
  - 选中并高亮该行，同时滚动定位。
  - 若为 phase 事件（`PhaseStart/PhaseComplete/PhaseError`）且已绑定 `PipelineGraphAsset`，会 focus 并居中到对应节点。

- `Locate Trace For Focus`：
  - 基于 `_focusRuntimeKey`（若无 focus 则使用 current phase id）定位最近相关事件，并滚动+高亮。


## Graph 资产与同步（Graph Assets and Sync）

### Graph 资产

调试器使用 `PipelineGraphAsset` 做可视化与高亮。

关键映射规则：

- `PipelineGraphNode.RuntimeKey` 用于把图节点绑定到运行时。
- 对于 pipeline phase：`RuntimeKey` 通常是 `PhaseId.ToString()`。


### 从选中 run 同步图

窗口提供：

- `Sync Graph From Selected Run`

该操作会根据当前选中的 pipeline 实例生成/更新节点与边。

行为：

- 通过 `RuntimeKey` 匹配来**保留已有节点位置**。
- 补齐缺失 nodes/edges。


### 高亮

- current phase / focus phase 会影响：
  - 节点高亮
  - 边高亮（路径强调，非相关边会变暗）
  - 可选：仅显示与 focus/current 连通的子图


## 常见问题（FAQ / 排错）

### 窗口没有数据

- 必须在 Play Mode。
- 必须确实产生了 run，并且 run 会在编辑器下调用 `RegisterRun` / `TouchRun`。

### 图不高亮

- 窗口中需要绑定 `PipelineGraphAsset`。
- 确认节点的 `RuntimeKey` 与 `PhaseId.ToString()` 一致。

### `SelectedRun` 显示 “(Not in list)”

- 说明 `SelectedRun` 指向的对象不在当前活跃 entries 中。
- Registry 会在清理/注销时自动修复选择；必要时重新打开窗口也会触发刷新。


## 扩展点（Extension points）

- 添加更多 Trace 事件：
  - 扩展 `PipelineTraceEventType`，并在 run 的关键节点写入。

- 强化图同步能力：
  - 扩展 `PipelineGraphSyncUtility` 来支持更多 phase/container 结构。

- 支持确定性回放（Deterministic replay）：
  - 可扩展 snapshot 捕获更多字段，但应保持轻量；Trace 为有上限 ring buffer。
