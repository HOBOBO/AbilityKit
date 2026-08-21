# com.abilitykit.pipeline

本包提供 Pipeline 运行时与相关的图结构/同步能力，并包含一套**以 Run 为中心**的调试数据模型（LiveRegistry + Trace），用于在 Play Mode 下观察运行中的 pipeline。

调试器的实时对象、每用户窗口状态和可持久化会话快照彼此分离：实时对象只由 Editor 弱引用观察，窗口偏好写入 `UserSettings`，需要长期保留的现场由用户显式导出为 `PipelineDebugSessionAsset`。

## 文档

- 调试体系结构与快速上手：
  - `Document/PipelineRuntimeDebugger.md`

- 运行时设计说明（Start/Run/Tick、Phase/Composite、状态与扩展点）：
  - `Document/PipelineRuntimeDesign.md`
