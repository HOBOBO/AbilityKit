# Pipeline 模块（com.abilitykit.pipeline 独立包）
> v0.1.0 Beta -- AbilityKitStable=true, has direct src tests, zero hard errors.

> Pipeline 已从 `Runtime/Ability/Share/Pipeline/` 独立成单独的 UPM 包 `com.abilitykit.pipeline`。命名空间 `AbilityKit.Pipeline`（运行时）/ `AbilityKit.Pipeline.Editor`（编辑器）。

## 当前真实结构

```
com.abilitykit.pipeline/
├── Runtime/
│   ├── Pipeline.cs                              # 静态类，持有 Pipeline.DefaultRuntime
│   ├── PipelineRuntime.cs                       # 显式运行时（Registry + TraceRecorder）
│   ├── Core/
│   │   ├── Pipeline/AbilityPipeline.cs          # 主类（abstract partial）
│   │   ├── Pipeline/AbilityPipeline_Composite.cs
│   │   ├── Pipeline/AbilityPipeline_ExtensionPoint.cs
│   │   ├── Pipeline/AbilityPipelinePhaseRuntime.cs
│   │   ├── Pipeline/InstantAbilityPipeline.cs
│   │   └── Time/ITimeProvider.cs, TimeProvider.cs
│   ├── Interface/                               # 全部泛型接口
│   │   ├── IAbilityPipeline.cs
│   │   ├── IAbilityPipelineRun.cs
│   │   ├── IAbilityPipelinePhase.cs
│   │   ├── IAbilityPipelineContext.cs
│   │   ├── IAbilityPipelineConfig.cs
│   │   ├── IAbilityPipelineExtensionPoint.cs
│   │   ├── IAbilityPipelinePhaseInstanceFactory.cs
│   │   ├── IAbilityInstantPhase.cs
│   │   ├── IDurationalPhase.cs
│   │   └── IInterruptiblePhase.cs
│   ├── Phase/
│   │   ├── Core/AbilityPipelinePhaseBase.cs     # 含 4 个基类
│   │   ├── Core/AbilityActionPhase.cs
│   │   ├── Condition/                           # 条件节点系统
│   │   ├── Timing/                              # Timeline/Delay/Repeat/WaitUntil
│   │   └── Composite/                           # Sequence/Parallel
│   ├── Graph/PipelineGraph.cs                   # 静态 DSL（非 Asset）
│   ├── Ids/AbilityPipelinePhaseId.cs            # 强类型 ID
│   ├── Data/                                    # Result/Snapshot
│   ├── Enum/                                    # EAbilityPipelineState 等
│   ├── Event/AbilityPipelineEvents.cs
│   ├── Lifecycle/                               # IPipelineLifeOwner, IPipelineRegistry, PipelineRegistry
│   ├── Impl/AAbilityPipelineContext.cs
│   ├── Debug/                                   # PipelineDebugHooks, TraceRecorder
│   └── Pooling/PipelinePools.cs
└── Editor/
    ├── PipelineEditorInitializer.cs
    ├── Core/Time/UnityTimeProvider.cs
    └── Debug/
        ├── EditorPipelineRegistry.cs            # ← 旧 AbilityPipelineLiveRegistry 的真身
        └── EditorPipelineTraceRecorder.cs       # ← Ring Buffer trace
```

## 核心接口（方法签名）

### `IAbilityPipeline<TCtx>`

```csharp
AbilityPipelineEvents<TCtx> Events { get; }
IAbilityPipelineRun<TCtx> Start(IAbilityPipelineConfig config, TCtx context);
void AddPhase(...);
void InsertPhase(int index, ...);
void RemovePhase(AbilityPipelinePhaseId phaseId);
void Reset();
```

### `IAbilityPipelineRun<TCtx>`

```csharp
EAbilityPipelineState State { get; }
TCtx Context { get; }
AbilityPipelinePhaseId CurrentPhaseId { get; }
bool IsPaused { get; }
void Tick(float deltaTime);
void Pause();
void Resume();
void Interrupt();
void Cancel();                                    // 新增
```

### `IAbilityPipelinePhase<TCtx>`

```csharp
AbilityPipelinePhaseId PhaseId { get; }
bool IsComplete { get; }
bool IsComposite { get; }                          // 新增
IReadOnlyList<IAbilityPipelinePhase<TCtx>> SubPhases { get; }  // 新增
bool ShouldExecute(TCtx);
void Execute(TCtx);
void OnUpdate(TCtx, float);
void Reset();
void HandleError(TCtx, Exception);                // 新增
```

## Phase 基类（`Runtime/Phase/Core/AbilityPipelinePhaseBase.cs`）

同文件定义 4 个基类：

- `AbilityPipelinePhaseBase<TCtx>` — 抽象基类
- `AbilityInstantPhaseBase<TCtx>` — 实现 `IAbilityInstantPhase`，`OnExecute` 密封后调用 `OnInstantExecute` 并立即 Complete
- `AbilityDurationalPhaseBase<TCtx>` — 实现 `IDurationalPhase`，有 `Duration` / `_elapsedTime` / `OnTick` / `ForceComplete`
- `AbilityInterruptiblePhaseBase<TCtx>` — 实现 `IInterruptiblePhase`，有 `OnInterrupt`

## PipelineGraph 静态 DSL

`Runtime/Graph/PipelineGraph.cs` 是 **`public static class`**（不是 ScriptableObject）。提供静态工厂：

```csharp
PipelineGraph.Sequence(...)
PipelineGraph.Parallel(...)
PipelineGraph.Conditional(...)
PipelineGraph.Repeat(...)
PipelineGraph.Action(...)
PipelineGraph.Delay(...)
PipelineGraph.WaitUntil(...)
PipelineGraph.Gate(...)
```

## 条件节点系统（旧 skill 未覆盖）

`Runtime/Phase/Condition/`：

- `IAbilityConditionNode<TCtx>` — 接口
- `AbilityConditionNodeBase<TCtx>` — 基类
- `AbilityAndCondition` / `AbilityOrCondition` / `AbilityNotCondition` — 复合
- `AbilityConditionalBranch` / `AbilityConditionalPhase` / `AbilityGatePhase` — 条件分支/Phase
- `EConditionCheckStrategy` / `ENoConditionBehavior` — 策略枚举

## 调试（旧 skill 大幅改写）

**旧 `AbilityPipelineLiveRegistry` 不存在**；真身是 `EditorPipelineRegistry`（`Editor/Debug/EditorPipelineRegistry.cs`，`#if UNITY_EDITOR`）：

- `sealed class : IPipelineRegistry`，单例 `Instance`
- `DebugEntry` 用 `WeakReference` 持有 owner
- 方法：`Register / Unregister / GetActiveOwners / InterruptAll / GetOwnersByPhase / RentOwnersByPhase / GetOwnersByState / GetTrace / TryGetOwner`

**旧 `PipelineGraphAsset`（ScriptableObject）不存在**；调试基础设施通过 API 暴露，**没有 EditorWindow**：

- `EditorPipelineTraceRecorder` — `sealed class : IPipelineTraceRecorder`，单例，含嵌套 `EditorPipelineRunTrace`（Ring Buffer 实现 `IPipelineRunTrace`）
- `PipelineDebugHooks` — 静态调试钩子
- `NoOpPipelineTraceRecorder` — runtime 默认空实现

## 运行时（PipelineRuntime）

`Runtime/PipelineRuntime.cs` 是 `sealed class`，组合：

- `IPipelineRegistry Registry`（runtime 用 `PipelineRegistry`，editor 用 `EditorPipelineRegistry`）
- `IPipelineTraceRecorder TraceRecorder`（runtime 用 `NoOpPipelineTraceRecorder`，editor 用 `EditorPipelineTraceRecorder`）

`Pipeline.DefaultRuntime` 是默认实例。

## 生命周期注册（旧 skill 未覆盖）

- `IPipelineLifeOwner` — 运行实例自注册（`OwnerId` / `OwnerName` / `ActivePhases`）
- `IPipelineRegistry` / `PipelineRegistry`（runtime） / `EditorPipelineRegistry`（editor）
- `IPipelineInterruptible` — 中断能力
- `PipelineRegistryOwnerListLease` — 池化租约

## 扩展点机制（旧 skill 未覆盖）

`AbilityPipeline_ExtensionPoint.cs`：

- `IAbilityPipelineExtensionPoint<TCtx>` 接口
- `AbilityPipeline.AddExtensionPoint(phaseId, extension, order)`
- `ExecuteExtensionPhaseStart` / `ExecuteExtensionPhaseComplete`

## 关键约定

- **外部驱动**：通过 `IAbilityPipelineRun<TCtx>.Tick(float deltaTime)` 推进
- **强类型 ID**：`AbilityPipelinePhaseId`（`Runtime/Ids/`）
- **调试代码**：必须 `#if UNITY_EDITOR`
- **池化**：`PipelinePools` 提供 `PipelineRegistryOwnerListLease` 等租约
