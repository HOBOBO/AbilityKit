---
trigger: manual
---

# Pipeline Runtime & Debugger 规则（Run-centric）

本规则用于在 AbilityKit 项目中进行 Pipeline runtime / debugger 相关修改时，保证一致性、可调试性与低运行时开销。

## 1) 核心约束

- Pipeline 执行必须以 `IAbilityPipelineRun<TCtx>` 为中心，通过 `Tick(deltaTime)` 外部驱动推进。
- Debug/Editor 逻辑必须包在 `#if UNITY_EDITOR` 内，避免运行时依赖与开销。
- 阶段（phase）必须可复用：多次 run 之间不能发生状态串扰。
  - 若 phase 内部有缓存/计时/列表等状态，务必在 `Reset()` 清理。

## 2) 运行时与图（Graph）的映射约定

- 图节点通过 `PipelineGraphNode.RuntimeKey` 与运行时绑定。
- 对于 pipeline phases，默认约定：
  - `RuntimeKey == PhaseId.ToString()`
- `PhaseId` 需要稳定：不要每次启动随机生成，否则会导致 graph 同步后节点位置无法复用。

## 3) LiveRegistry / Trace 约束

- `AbilityPipelineLiveRegistry` 只在编辑器下启用（`UNITY_EDITOR`）。
- 写入点建议：
  - run 创建：`RegisterRun(...)` + `RunStart`
  - 每 tick：`TouchRun(...)` + `Tick`
  - phase 生命周期：`PhaseStart/PhaseComplete/PhaseError`
  - run 结束：`UnregisterRun(...)` + `RunEnd`

- Trace 事件应遵循：
  - 短文本、可搜索
  - 保留关键字段（phaseId/state/message）
  - 注意 ring buffer 容量，避免无意义高频噪声

## 4) Composite 阶段扩展约束

- 新增 composite 阶段时：
  - `IsComposite == true`
  - `SubPhases` 或内部结构需要可被图同步工具识别（通常通过反射字段约定）。
  - 明确分支/并行驱动策略，避免 current phase / state 更新不一致。

## 5) 参考文档

- `Unity/Packages/com.abilitykit.pipeline/Documentation~/PipelineRuntimeDesign.md`
- `Unity/Packages/com.abilitykit.pipeline/Documentation~/PipelineRuntimeDebugger.md`
