---
name: ability-kit
section: pipeline-runtime-debugger
---

# Pipeline Runtime / Debugger（速查）

## 目标

- 快速定位 Pipeline runtime（Start/Run/Tick、Phase/Composite）与 debugger（run list/trace/graph）的入口。
- 统一 Run-centric 调试与图映射约定。

## 关键入口

### Runtime

- `Unity/Packages/com.abilitykit.pipeline/Runtime/Ability/Share/Pipeline/AbilityPipeline.cs`
- `Unity/Packages/com.abilitykit.pipeline/Runtime/Ability/Share/Pipeline/Interface/IAbilityPipeline.cs`
- `Unity/Packages/com.abilitykit.pipeline/Runtime/Ability/Share/Pipeline/Interface/IAbilityPipelineRun.cs`
- `Unity/Packages/com.abilitykit.pipeline/Runtime/Ability/Share/Pipeline/Interface/IAbilityPipelinePhase.cs`
- `Unity/Packages/com.abilitykit.pipeline/Runtime/Ability/Share/Pipeline/Phase/AbilityPipelinePhaseBase.cs`

### Debug / LiveRegistry（editor-only）

- `Unity/Packages/com.abilitykit.pipeline/Runtime/Ability/Share/Pipeline/Debug/AbilityPipelineLiveRegistry.cs`

### Graph

- `Unity/Packages/com.abilitykit.pipeline/Runtime/Graph/PipelineGraphAsset.cs`
- `Unity/Packages/com.abilitykit.pipeline/Runtime/Graph/Dtos/PipelineGraphDto.cs`

### Editor Window

- `Unity/Packages/com.abilitykit.ability.runtime/Editor/PipelineRuntimeDebugger/AbilityPipelineRunDebuggerWindow.cs`

## 关键约定

- 外部驱动：通过 `IAbilityPipelineRun<TCtx>.Tick(deltaTime)` 推进。
- 图映射：`PipelineGraphNode.RuntimeKey == PhaseId.ToString()`（phase id 需要稳定）。
- 调试代码：必须 `#if UNITY_EDITOR`。

## 文档

- `Unity/Packages/com.abilitykit.pipeline/Documentation~/PipelineRuntimeDesign.md`
- `Unity/Packages/com.abilitykit.pipeline/Documentation~/PipelineRuntimeDebugger.md`
