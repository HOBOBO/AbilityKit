# Ability-Kit Combat Collision 碰撞抽象模块开发设计文档

> **历史资料 / 已取代的接口说明**：保留 2026-06-05 背景，当前安装见[包入口](../README.md)，使用契约见[指南](../Documentation~/Usage.md)。当前类型为 ICollisionWorld、NaiveCollisionWorld 和 GridCollisionWorld，形状在 CollisionPrimitives.cs；下文 CollisionWorld、CollisionShapes.cs 及“无空间分区”的表述不作为现行事实。未借本次导航更新刷新历史正文日期。

> **阅读对象**：需要在战斗逻辑中进行形状查询、碰撞世界注册和命中检测的开发者。
>
> **文档目标**：说明该包的碰撞形状、查询、服务和世界容器边界。

---

## 一、设计理念

Combat Collision Abstractions 提供逻辑层碰撞查询的轻量抽象。它不绑定 Unity Physics，而是用纯数据形状和服务接口描述战斗中常见的圆、扇形、矩形等查询。

---

## 二、模块边界

负责：

- 定义碰撞形状模型。
- 定义 `CollisionWorld` 保存可查询对象。
- 定义 `ICollisionService` 和默认 `CollisionService`。
- 定义查询参数和结果。

不负责：

- 不驱动 Unity Collider。
- 不负责物理模拟。
- 不处理表现层碰撞。
- 不做复杂空间分区优化。

---

## 三、目录结构

| 文件 | 职责 |
|------|------|
| `CollisionShapes.cs` | 逻辑碰撞形状 |
| `CollisionQueries.cs` | 查询参数/结果 |
| `CollisionWorld.cs` | 碰撞对象容器 |
| `ICollisionService.cs` | 服务接口 |
| `CollisionService.cs` | 默认查询实现 |

---

## 四、注意事项

- 该包适合战斗逻辑判定，不适合作为 Unity 物理系统替代品。
- 如果实体数量上升，需要引入网格、四叉树或 BVH 等空间索引。
- 查询形状字段变更会影响技能配置和回放一致性。

---

*文档版本：1.0*  
*最后更新：2026-06-05*
