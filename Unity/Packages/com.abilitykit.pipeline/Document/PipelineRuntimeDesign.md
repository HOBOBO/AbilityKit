# Pipeline Runtime 设计说明（com.abilitykit.pipeline/Runtime）

## 目标

本设计文档聚焦 `com.abilitykit.pipeline/Runtime` 内的**管线运行时**：核心类型、执行模型、状态机、阶段（Phase）抽象与复合阶段（Composite）模型，以及与图（Graph）/调试（Debug/Editor）的边界。

适合：

- 第一次接手该模块，需要快速理解“Start -> Run -> Tick”的数据流。
- 需要新增自定义阶段（Phase）、复合阶段（Composite）、或扩展事件/调试能力。


## 总体设计要点

- **Run-centric**：管线本身是配置与阶段容器；真正“执行中的实例”由 run 表示。
- **外部驱动**：通过 `IAbilityPipelineRun<TCtx>.Tick(deltaTime)` 外部驱动推进（便于接入 ECS/回放/确定性驱动）。
- **统一阶段模型**：所有阶段都有 `IsComplete`；瞬时阶段在 `Execute` 内完成，持续阶段由 `OnUpdate` 推进完成。
- **可调试性**：编辑器下可写入 LiveRegistry + Trace，实现运行中观测与回放线索。


## Runtime 目录结构索引（去哪找什么）

> 目的：让第一次接手的人能快速定位关键文件。

- `Runtime/Ability/Share/Pipeline`
  - 核心 pipeline 抽象与默认实现（`AbilityPipeline<TCtx>`）
  - 接口：`Interface/`（`IAbilityPipeline<TCtx>`、`IAbilityPipelineRun<TCtx>`、`IAbilityPipelinePhase<TCtx>`、`IAbilityPipelineContext` 等）
  - 阶段：`Phase/`（基类、复合阶段、并行/条件/序列等实现）
  - Debug：`Debug/`（`AbilityPipelineLiveRegistry`、Trace）

- `Runtime/Graph`
  - 图资产：`PipelineGraphAsset`
  - DTO：`Graph/Dtos/`（用于序列化/导入导出）

- `Runtime/Ids`
  - 图相关 ID 类型：`PipelineNodeId`、`PipelinePortId` 等


## 关键类型与职责

> 说明：以下类型主要位于 `Runtime/Ability/Share/Pipeline`。

### `IAbilityPipeline<TCtx>`

- 职责：
  - 管线容器与构建入口
  - 管线生命周期事件（`Events`）
  - `Start(config, context)` 生成 run
  - `AddPhase/InsertPhase/RemovePhase/Reset` 维护阶段列表

### `IAbilityPipelineRun<TCtx>`

- 职责：
  - 单次运行实例（执行句柄）
  - 对外暴露执行状态与控制：
    - `State`、`Context`、`CurrentPhaseId`、`IsPaused`
    - `Tick/Pause/Resume/Interrupt/Cancel`

### `IAbilityPipelineContext`

- 职责：
  - run 的可变运行时状态容器
  - 必须包含：
    - `CurrentPhaseId`
    - `PipelineState`
    - `IsAborted` / `IsPaused`
    - `StartTime` / `ElapsedTime`

### `IAbilityPipelinePhase<TCtx>`

- 职责：
  - 阶段抽象
  - 统一模型：
    - `Execute(context)`：进入并执行一次（瞬时阶段在此完成）
    - `OnUpdate(context, dt)`：持续阶段每 tick 推进
    - `IsComplete`：阶段完成标记
    - `ShouldExecute(context)`：是否应执行（用于跳过阶段）
    - `Reset()`：可复用（多次 run）
  - 复合阶段：
    - `IsComposite` 为 true
    - `SubPhases` 暴露子阶段列表（用于调试/图同步）

### `AbilityPipelinePhaseBase<TCtx>`（建议使用的基类）

- 提供统一的 Enter/Execute/Exit 组织方式：
  - `Execute` 会先 `OnEnter` 再 `OnExecute`
  - `Complete` 会设置 `IsComplete = true` 并 `OnExit`

- 子类常见选择：
  - `AbilityInstantPhaseBase<TCtx>`：瞬时阶段（`OnInstantExecute` 后立即完成）
  - `AbilityDurationalPhaseBase<TCtx>`：持续阶段（带 `Duration`/计时逻辑，可重写 `OnTick`）
  - `AbilityInterruptiblePhaseBase<TCtx>`：可中断持续阶段（实现 `IInterruptiblePhase<TCtx>`）


## 执行模型：Start -> Run -> Tick

以 `AbilityPipeline<TCtx>` 为例：

1. 构建 pipeline：通过 `AddPhase(...)` 添加阶段（顺序列表）。
2. `Start(config, context)`：
   - 重置所有阶段（`Reset()`）
   - 创建 `Run` 实例并返回为 `IAbilityPipelineRun<TCtx>`
3. 外部循环驱动 `Tick(dt)`：
   - 若当前有运行中的阶段：调用该阶段 `OnUpdate`
   - 若阶段完成：触发 `OnPhaseComplete`，推进 phase index
   - 在同一个 tick 内尽可能执行“瞬时阶段”（`ExecutePipeline()` 循环）
   - 所有阶段完成后触发 `Complete()`


## 最小使用示例（伪代码）

> 说明：下面是“用法形态”，不依赖具体游戏侧实现细节。类型名以当前 Runtime 为准。

### 1) 构建 pipeline + 启动 run

```csharp
// 伪代码：TCtx 必须实现 IAbilityPipelineContext
IAbilityPipeline<TCtx> pipeline = new MyPipeline();

pipeline.AddPhase(new MyInstantPhase<TCtx>(new AbilityPipelinePhaseId("A")));
pipeline.AddPhase(new MyDurationalPhase<TCtx>(new AbilityPipelinePhaseId("B")));

// config/context 由业务侧提供
IAbilityPipelineConfig config = ...;
TCtx ctx = ...;

IAbilityPipelineRun<TCtx> run = pipeline.Start(config, ctx);

// 外部驱动
while (run.State == EAbilityPipelineState.Executing)
{
    run.Tick(deltaTime);
}
```

### 2) 一个瞬时阶段（推荐继承 `AbilityInstantPhaseBase<TCtx>`）

```csharp
// 伪代码
sealed class MyInstantPhase<TCtx> : AbilityInstantPhaseBase<TCtx>
    where TCtx : IAbilityPipelineContext
{
    public MyInstantPhase(AbilityPipelinePhaseId id) : base(id) {}

    protected override void OnInstantExecute(TCtx context)
    {
        // 做一次性逻辑
        // 基类会在 OnInstantExecute 后立即 Complete
    }
}
```

### 3) 一个持续阶段（推荐继承 `AbilityDurationalPhaseBase<TCtx>`）

```csharp
// 伪代码
sealed class MyDurationalPhase<TCtx> : AbilityDurationalPhaseBase<TCtx>
    where TCtx : IAbilityPipelineContext
{
    public MyDurationalPhase(AbilityPipelinePhaseId id) : base(id)
    {
        Duration = 0.5f; // 例如 0.5 秒后自动完成
    }

    protected override void OnTick(TCtx context, float deltaTime)
    {
        // 每 tick 推进逻辑
        // 可根据条件提前调用 ForceComplete(context)
    }
}
```


## 程序集 / 命名空间 / 依赖关系说明

### asmdef

在 `com.abilitykit.pipeline/Runtime` 下存在 `AbilityKit.Pipeline.asmdef`，它定义了该包运行时代码的编译单元。

### 命名空间现状

当前 Runtime 目录下同时存在多套命名空间（例如 `AbilityKit.Pipeline` 与 `AbilityKit.Ability`）。

这意味着：

- **程序集名（asmdef）不等于命名空间**：即使 asmdef 叫 `AbilityKit.Pipeline`，类型仍可能在 `namespace AbilityKit.Ability` 下。
- 使用方引用时以 **类型全名（namespace + type）** 为准，不要只凭 asmdef 名猜命名空间。

### 推荐使用习惯

- 在业务侧建议显式 `using AbilityKit.Ability;` / `using AbilityKit.Pipeline;`，避免同名类型或迁移期的歧义。
- 若未来要做“彻底迁移命名空间”，建议分阶段：
  - 先稳定 API（类型名/文件布局）
  - 再做 namespace 统一（需要全项目重命名/重定向成本）


## 状态与异常处理

- `State`：run 的核心状态（例如 `Executing/Completed/Failed/...`）。
- 错误路径：
  - phase 执行或更新抛异常会进入 `HandlePhaseError`：
    - `State = Failed`
    - 调用 `phase.HandleError(context, ex)`（安全 try/catch）
    - 触发 `Events.OnPhaseError`
    - 清理并注销

- 中断/取消：
  - `Cancel()`：标记 `_isCancelled`，在 `Tick()` 中转 `Fail()`
  - `Interrupt()`：
    - 若当前阶段（及并行子阶段）支持 `IInterruptiblePhase<TCtx>`，会调用 `OnInterrupt`
    - 设置 `context.IsAborted = true`
    - 触发 `Events.OnPipelineInterrupt`
    - 进入失败清理

- 暂停：
  - `Pause/Resume` 同步 `IsPaused` 与 `context.IsPaused`
  - 暂停期间 `Tick()` 不推进阶段


## 复合阶段（Composite）模型

- 复合阶段的典型特征：
  - `IsComposite == true`
  - 通过 `SubPhases` 暴露结构

- 运行时的执行策略：
  - `AbilityPipeline<TCtx>` 在执行复合阶段时，会进入专门的 composite 处理逻辑（例如 `HandleCompositePhase` / `OnCompositeUpdate`），以统一驱动子结构。

> 具体 composite 类型（例如 sequence/parallel/conditional）会被图同步工具识别，并展开成非线性图。


## IDs 与 Graph（运行时数据结构）

本包包含一套图结构用于可视化与同步：

- `PipelineGraphAsset`
  - 保存 nodes/ports/edges 及节点位置
  - 节点通过 `RuntimeKey` 与运行时相绑定

- ID 类型（示例）：
  - `PipelineNodeId`
  - `PipelinePortId`

重要约定：

- 对于 pipeline phases，通常使用 `PhaseId.ToString()` 作为图节点 `RuntimeKey`。


## Debug/Editor 边界

### `AbilityPipelineLiveRegistry`（仅编辑器）

- 编译条件：`#if UNITY_EDITOR`
- 目的：
  - 在 Play Mode 下收集活跃 run 的快照与 trace
  - 为调试窗口提供数据源

- 关键能力：
  - `RegisterRun(pipeline, config, run)`：注册并创建 trace
  - `TouchRun(run)`：刷新 snapshot + 记录 Tick
  - `UnregisterRun(run)`：注销
  - `SelectedRun`：全局焦点；当失效时会自愈回退

### Trace

- `PipelineRunTrace` 是 ring buffer，容量有限。
- 事件类型 `PipelineTraceEventType` 用于调试 UI 过滤与定位。


## 扩展指南

### 新增一个自定义阶段（Phase）

推荐做法：

- 继承 `AbilityInstantPhaseBase<TCtx>` 或 `AbilityDurationalPhaseBase<TCtx>`
- 在 `OnInstantExecute` 或 `OnTick` 中实现逻辑
- 确保：
  - 设置合理的 `PhaseId`
  - 在 `Reset()` 恢复可复用状态

### 新增一个复合阶段（Composite）

- 实现 `IsComposite == true` 与 `SubPhases`
- 明确子阶段的驱动策略（并行、条件分支、序列等）
- 若需要可视化展开：
  - 需要 `PipelineGraphSyncUtility` 支持该 composite 的反射/识别

### 扩展调试信息

- 新增 trace 类型：扩展 `PipelineTraceEventType` 并在 run 的关键点写入。
- 扩展 snapshot：在 `AbilityPipelineLiveRegistry.CaptureSnapshot` 中捕获额外字段（保持轻量、可选）。


## 常见扩展场景与最佳实践

### 场景 1：新增一个带“分支”的复合阶段（Composite）

目标：实现类似 `if/else` 或多分支选择的阶段，并能被调试器展开为非线性图。

建议：

- 运行时层面：
  - 复合阶段需要 `IsComposite == true`。
  - 需要通过 `SubPhases`（或内部结构可被反射读取）暴露子阶段信息，以便图同步工具识别。
  - 需要明确“当前活跃分支”的推进规则：分支切换时如何更新 `Context.CurrentPhaseId`、如何结束自身。

- 图同步层面：
  - 若 `PipelineGraphSyncUtility` 已支持该 composite 类型（例如 conditional/parallel/sequence），只要字段/结构符合约定即可自动展开。
  - 若是新 composite：需要在 `PipelineGraphSyncUtility` 中新增识别与展开规则（生成 ports/edges），并为 edge 生成可区分的 `FromPortId`（例如 `branch[...]`）。

### 场景 2：让一个“容器阶段”在图里保持可读

目标：像 `Sequence/Parallel` 这种容器阶段，既能在运行时复用，又能在图里清晰。

建议：

- `PhaseId` 要稳定（避免每次启动生成随机 id），否则 `RuntimeKey` 对不上，节点位置无法复用。
- 子阶段的 `PhaseId` 同样建议稳定。
- 如果容器阶段只是逻辑聚合，不希望在图里显示为独立节点：
  - 可以在图同步工具里选择“只展开子阶段，不画容器节点”（取决于当前同步策略）。

### 场景 3：扩展 Trace（推荐）

目标：让调试窗口更容易“定位到问题点”，例如补充资源加载、条件评估结果、分支选择原因等。

建议：

- 扩展 `PipelineTraceEventType` 后，在 run 的关键点写入。
- 事件 `Message` 建议遵循：
  - 短文本、可搜索
  - 包含关键信息（例如分支名/条件值/错误摘要）
- 注意容量：`PipelineRunTrace` 是 ring buffer，重要事件优先级要高于每 tick 噪声。

### 场景 4：扩展 Snapshot（谨慎）

目标：在 run 列表中更快看到“当前到底卡在哪”。

建议：

- Snapshot 捕获在编辑器下可能被频繁调用（每 tick）。
- 优先捕获：
  - `State`、`CurrentPhaseId`、`PhaseIndex`、`IsPaused`
- 额外字段建议保持：
  - 读取开销低（避免深层反射/集合遍历）
  - 可选/降级安全（缺字段也不抛异常）

### 常见坑

- **Phase 未重置导致跨 run 污染**：如果 phase 内部有缓存状态，请在 `Reset()` 清理。
- **PhaseId 不稳定**：会导致 graph 的 `RuntimeKey` 映射失效，节点位置无法保留。
- **持续阶段忘记完成条件**：持续阶段应在 `OnUpdate/OnTick` 里调用 `Complete/ForceComplete` 或配置 `Duration`。
- **调试与运行时耦合过重**：调试逻辑应包在 `#if UNITY_EDITOR`，避免运行时开销与依赖污染。


## 相关文档

- Debugger/Graph 使用说明：
  - `Documentation~/PipelineRuntimeDebugger.md`
