# Flow Examples

本目录用于展示 `com.abilitykit.flow` 的典型用法与设计要点。

- 示例代码尽量保持“最小可读”，以便你复制到业务工程中直接改造。
- 示例默认以纯 C# 方式展示；如需 Unity 场景演示，可在此基础上扩展 MonoBehaviour 驱动。

## 示例列表

- `01_BasicSessionExample.cs`
  - 基础：创建 `FlowSession`，启动节点树，使用 `Step(deltaTime)` 推进

- `02_WakePumpExample.cs`
  - 事件驱动：`AwaitCallbackNode` + `FlowWakeUp.Wake()`（模拟回调触发）

- `03_TimeoutAndRaceExample.cs`
  - 组合：`TimeoutNode` + `RaceNode`（谁先完成谁赢）

- `04_ParallelAllExample.cs`
  - 并行：`ParallelAllNode` 的完成/失败语义

- `05_UsingResourceAndScopeExample.cs`
  - 资源管理：`UsingResourceNode<T>` + `FlowContext` 注入

- `06_ExceptionHandlingExample.cs`
  - 异常上报：`FlowSession.UnhandledException` 与 runner 上报机制

> 注意：示例中的“回调触发”都假设发生在与 `FlowRunner` 相同线程。
