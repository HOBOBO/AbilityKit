# Required context

在开始重构前，至少确认：

## 生命周期边界

- 当前类/feature 的生命周期边界（`OnAttach / OnDetach / Tick / OnFrame`）
- 对 Session 层是 `IBattleSessionFeature` 的 `OnAttach/OnDetach/Tick`（在 `BattleSessionFeature.Lifecycle.cs`）
- 对 Entitas ECS 层是 `WorldSystemBase` 的 `OnInit / OnExecute / OnCleanup / OnTearDown`

## 字段归属

哪些字段属于：

- **纯状态** → 应迁入 `State`（或 State 的嵌套 POCO 子状态）
- **资源/引用/可释放对象** → 应迁入 `Handles`（或 Handles 的领域 partial）
- **行为逻辑** → 应迁入 `Controllers`（或保持在 Feature 主文件作为 orchestrator）

## 关键调用链

- Tick loop（如 `TickLoopController.MainTick`）
- FrameReceived（如 Gateway 相关 Controller）
- PlanBuilt / Starting / Stopping（Session 生命周期）

## 构建验证方式

- `dotnet build`（Console Demo 与相关 .NET 项目）
- Unity CI（Editor 进程编译 + 测试）
- 运行时最小可观测日志或断言（避免 silent fail）
