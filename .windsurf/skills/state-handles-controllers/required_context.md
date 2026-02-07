---
name: state-handles-controllers
section: required_context
---

# Required context

在开始重构前，至少确认：

- 当前类/feature 的生命周期边界（Attach/Detach/Start/Stop/Tick/OnFrame）
- 哪些字段属于：
  - 纯状态（应迁入 State）
  - 资源/引用/可释放对象（应迁入 Handles）
  - 行为逻辑（应迁入 Controllers 或保持在 Orchestrator）
- 关键调用链（谁调用谁）：
  - Tick loop
  - FrameReceived
  - PlanBuilt/Starting/Stopping
- 构建验证方式（`dotnet build` / Unity CI）
