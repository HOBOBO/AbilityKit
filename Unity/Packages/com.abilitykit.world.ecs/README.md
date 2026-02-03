# AbilityKit World ECS（com.abilitykit.world.ecs）

本包提供一套**客户端侧的简易实体组件容器（Entity-Component Container）**，主要定位为：

- 将历史上未按 ECS 组织的代码，以较低侵入成本“挂载/桥接”到统一管理体系中
- 提供稳定的 `EntityId(index, version)` 句柄、基础的组件挂载/查询、以及父子层级关系
- 为调试与工具链（Editor/Inspector/Replay 等）提供统一的实体数据入口

> 重要：本包当前实现**不是**完整意义上的“高性能 ECS”（如 archetype/chunk/query/system 管线），也不以极致数据导向为目标。

## 1. 适用场景

- 需要统一管理“业务对象/视图对象/旧模块对象”的生命周期（创建/销毁/层级）
- 希望将旧模块桥接到统一的 entity 体系（例如：角色、投射物、UI 节点、特效节点）
- 需要通过事件（EntityCreated/Destroyed/ComponentSet/Removed/ParentChanged）驱动外部系统同步

## 2. 不适用场景

- 大量系统需要对某类组件进行**高频批处理遍历**并追求极致性能（典型纯 ECS 主循环）
- 需要 chunk 级别内存布局优化、SIMD/Job/多线程调度等

如果出现以上需求，建议引入成熟第三方 ECS（见第 5 节），或在明确性能瓶颈后再评估自研完整 ECS。

## 3. 设计取舍与核心类型

- `EntityId(index, version)`
  - 通过版本号避免 index 复用导致的悬空引用
- `EntityWorld`
  - 负责实体的创建/销毁、父子关系管理、组件挂载
  - 组件存储为每实体一个 `object[]`（按 `ComponentTypeId` 索引）
- `Entity`
  - 轻量句柄/门面，封装常用操作（AddComponent/GetComponent/SetParent 等）
- `ComponentTypeId`
  - 运行时为组件 `Type` 分配整数 id

## 4. 使用约束（建议团队统一遵守）

- **线程模型**：默认假设在 Unity 主线程使用（不保证并发安全）
- **组件类型**：当前组件以 `class`（引用类型）为主，便于直接桥接旧对象
- **资源释放**：销毁实体默认只会断开引用，不会自动 Dispose 组件；如组件需要释放资源，应由上层约束或扩展点统一处理

## 5. 演进建议：引入第三方 ECS 更合适

对于“标准 ECS”能力（Query/System 管线、archetype/chunk、性能优化、工具生态），通常更推荐采用成熟方案：

- Svelto ECS
- Entitas
- Unity Entities（DOTS）

建议路线：

- 将本包长期作为**桥接层/适配层**保留
- 当引入第三方 ECS 时，在桥接层提供适配：
  - 旧模块继续通过 `EntityWorld/Entity` 访问（低侵入）
  - 新模块直接使用第三方 ECS
  - 在边界处用同步/映射策略（如 view/transform 同步、生命周期同步）逐步迁移

仅当出现明确且持续的性能瓶颈（并且第三方方案无法满足/不适配约束）时，再评估自研完整 ECS。
