# com.abilitykit.pipeline

> 文档类型：包入口与快速开始
> 核校状态：2026-11-25 对照运行时、样例和 Pipeline 单步测试；.NET 44 项通过，未运行 Unity 样例。

本包把一次能力执行编排为顺序、条件或组合阶段。每次启动产生一个 Run，由宿主传入时间步长推进；阶段配置、领域结算和跨帧快照由各自的调用方定义。包内的图结构、LiveRegistry 和 Trace 用于观察运行。

调试器的实时对象、每用户窗口状态和可持久化会话快照彼此分离：实时对象只由 Editor 弱引用观察，窗口偏好写入 `UserSettings`，需要长期保留的现场由用户显式导出为 `PipelineDebugSessionAsset`。

## 接入

- Unity 包直接依赖 `com.abilitykit.core`。在 Package Manager 中选择本包的 `Pipeline Usage` 样例并导入；样例位于 `Samples~/PipelineUsage`，不属于运行时程序集。
- `.NET` 镜像复用包内 `Runtime/`，以不含 Unity 场景加载钩子的同名 `Pipeline` 门面替代包内门面，不编译 `Editor/`。跨平台宿主优先显式创建并持有 `PipelineRuntime`；只有 .NET 公开静态 `Pipeline.Initialize()`。
- 执行步长、上下文、成功后的领域结算和回滚状态由宿主负责；样例不代替项目的存档、回滚或技能规则。

省略 `PipelineRuntime` 的注册表参数时沿用全局 `PipelineRegistry.Instance`；需要隔离时显式传入并初始化独立注册表。`SetRegistry` 影响随后登记的运行，已有运行向原注册表注销。阶段工厂、重置或启动失败会归还已租借的阶段列表；失败的启动不会代替调用方释放业务上下文。`Release()` 丢弃运行仍不重放业务回调或释放上下文。

## 自定义阶段

带有运行状态、且可能被多个 Run 同时使用的阶段，应实现公开的 `IAbilityPipelinePhaseInstanceFactory<TCtx>`，为每次运行创建独立实例。工厂复制规则和稳定的 `PhaseId`，`Reset()` 清理该实例的运行状态；继承阶段基类本身不会自动隔离状态。未实现工厂的旧阶段仍复用原对象，调用方须避免重叠运行。

需要恢复业务字段时，通过 `PipelineExecutionProgress` 的自定义捕获、校验和写回回调接入；基类只能提供通用字段。校验在全树写入前执行，写回应直接设置状态，避免再次执行进入阶段的业务动作。自定义写回失败不提供事务补偿。详见 `Document/PipelineRuntimeDesign.md` 的扩展指南。

## 文档

- 使用方法和资源边界见 [Documentation~ 索引](Documentation~/README.md)。
- 调试体系结构与快速上手：
  - `Document/PipelineRuntimeDebugger.md`

- 运行时设计说明（Start/Run/Tick、Phase/Composite、状态与扩展点）：
  - `Document/PipelineRuntimeDesign.md`
