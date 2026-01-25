---
trigger: manual
---

# 性能与实现规范（强约束）

## 1) 总原则

- **性能优先**：默认战斗逻辑在高频路径（每帧/多实体/多技能），避免不必要的分配与反射。
- **struct 优先**：短生命周期/频繁传递的数据优先用 struct。

## 2) 禁止项（高频路径）

- 禁止在 `Update/Tick/Execute` 等高频路径产生 GC：
  - `new List/Dictionary/HashSet`
  - LINQ
  - 闭包
  - 装箱（object/接口传递导致）

## 3) 池化与集合复用

- Args：优先使用 `PooledTriggerArgs` / `PooledDefArgs`
- 临时集合：优先使用项目内 Pools 提供的 pool（或复用已有对象），避免 new

## 4) 参数合并策略

- 对只读合并参数，优先用“临时注入并恢复”或“overlay 视图”，避免复制整个 args 字典。

## 5) 生命周期约束

- 不要在 Entitas `Cleanup()` 做一次性反注册；一次性逻辑应迁移到 `TearDown()`。
