# Pipeline Runtime Debugger（编辑器窗口）

本目录包含用于观察运行中 Pipeline Runs 的编辑器窗口：`AbilityPipelineRunDebuggerWindow`。

## 使用前提

- 必须在 **Play Mode**。
- 运行时需要把 run 注册到 `AbilityPipelineLiveRegistry`（默认 `AbilityPipeline<TCtx>` 会在 `UNITY_EDITOR` 下自动注册/触达/注销）。

## 快速上手

- 打开 `AbilityPipelineRunDebuggerWindow`。
- 在 `Running Run` 下拉选择 run。
- 需要可视化/高亮时：
  - 绑定 `PipelineGraphAsset`
  - 点击 `Sync Graph From Selected Run`
- 事件与图跳转：
  - 点击 Trace 的 phase 事件可 focus/居中到节点
  - `Locate Trace For Focus` 可从节点回到最近相关事件

## 详细文档

- `com.abilitykit.pipeline/Documentation~/PipelineRuntimeDebugger.md`
