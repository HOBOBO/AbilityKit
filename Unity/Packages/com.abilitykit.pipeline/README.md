# com.abilitykit.pipeline

本包把一次能力执行编排为顺序、条件或组合阶段。每次启动产生一个 Run，由宿主传入时间步长推进；阶段配置、领域结算和跨帧快照由各自的调用方定义。包内的图结构、LiveRegistry 和 Trace 用于观察运行。

调试器的实时对象、每用户窗口状态和可持久化会话快照彼此分离：实时对象只由 Editor 弱引用观察，窗口偏好写入 `UserSettings`，需要长期保留的现场由用户显式导出为 `PipelineDebugSessionAsset`。

## 文档

- 调试体系结构与快速上手：
  - `Document/PipelineRuntimeDebugger.md`

- 运行时设计说明（Start/Run/Tick、Phase/Composite、状态与扩展点）：
  - `Document/PipelineRuntimeDesign.md`
