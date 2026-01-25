---
name: ability-kit
section: invariants
---

# Project invariants / constraints (must follow)

- **性能优先**：默认战斗逻辑在高频路径，禁止不必要分配/反射/LINQ。
- **默认池化**：args/defs 使用 `PooledTriggerArgs` / `PooledDefArgs`。
- **生命周期**：不要在 Entitas `Cleanup()` 做一次性反注册，使用 `TearDown()`。
- **EventBus 一致性**：Subscribe 与 Publish 必须使用同一 `IEventBus` 实例（DI singleton）。
- **新写业务代码需中文注释**：说明目的、关键约束、边界条件。
