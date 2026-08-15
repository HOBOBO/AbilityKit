# Core 基础设施文档索引

> 当前迁移状态：DebugDraw 已由 `com.abilitykit.diagnostics` 接管，Core 旧 API
> 仅保留兼容；`DisposeUtils` 和 Marker 全局启动入口不再接受新消费者。

> AbilityKit Core 基础设施包文档入口

---

## 包定位

`com.abilitykit.core` 是 AbilityKit 的 P0 基础底座包，提供上层模块共享的纯 C# 基础设施。它不是业务战斗包，也不是 Demo 包。

Core 的稳定基础面当前覆盖以下能力：

- 数学基础：`Runtime/Math`。
- 日志：`Runtime/Logging`。
- 稳定集合：`Runtime/Collections`。
- 托管缓冲区所有权：`Runtime/Buffers`。
- 稳定标识：`Runtime/Generic`。
- 生命周期注册：`Runtime/Lifetime`。
- 单调时间：`Runtime/Timing`。

`Runtime/Continuous`、`Runtime/Config`、`Runtime/Markers`、
`Runtime/Reflection` 和 `Runtime/DebugDraw` 是迁移中的兼容面，不是新功能落点。
其中 MOBA 的设置与可选模块安装已迁回所属 Demo 包；Core 中的 Config/Reflection
类型仅用于下一主版本前的源码和二进制兼容（`Runtime/Numerics` 已于 2026-08 删除）。

---

## 文档列表

### 1. [Core 边界与准入规则](./CoreBoundary.md)

**阅读对象**：新增 Core API、迁移遗留职责或评审跨包基础设施的开发者

**内容概要**：程序集边界、三消费者准入规则、API 兼容门禁、线程安全契约与迁移清单。

### 2. [Marker 系统说明](../Runtime/Markers/README.md)

**阅读对象**：需要用 Attribute/Marker 自动注册类型的框架或业务模块开发者

**内容概要**：
- MarkerAttribute 的用途
- MarkerRegistry 和扫描入口
- 上层包如何通过 Marker 降低手写注册成本

---

## 快速入门

### 想了解 Core 是什么？

先阅读包根目录 [`README.md`](../README.md)，确认 Core 的定位、边界和 Starter 验收要求。

### 旧 Core Numerics（NumberValue 等）已删除

该兼容面于 2026-08（首次发布前）移除：唯一框架消费方早已迁移到 MOBA 自有的
`CombatNumberValue`（定点管线，旧名 DamageNumberValue）。如需数值容器，在调用方领域内实现（参考
`com.abilitykit.demo.moba.runtime` 的 Damage/Resource 定点管线与
`com.abilitykit.world.framesync/Document/定点帧同步接入指南.md`）。

### 想验证 Foundation Starter？

Foundation Starter 的第一阶段只应依赖 `core` 和 `world.di`，用于验证日志、集合、所有权、World 服务注册和宿主驱动 Tick。示例落点优先使用 `src/AbilityKit.Samples.Logic`，避免直接依赖任何 `demo.*` 包。

---

## 概念速查

### Core 子能力

| 子能力 | 路径 | 用途 |
|------|------|------|
| Math | `Runtime/Math` | 跨 Unity/服务端/测试的基础数学类型 |
| Logging | `Runtime/Logging` | 日志入口与输出 Sink 抽象 |
| Event | `Runtime/Event` | 轻量事件发布、订阅和取消订阅 |
| Pooling | `Runtime/Pooling` | 对象池、池作用域、池配置与诊断 |
| Markers | `Runtime/Markers` | Attribute 标记和类型扫描注册 |
| Continuous | `Runtime/Continuous` | 已弃用的兼容面；新代码使用 `com.abilitykit.continuous` |
| Config/Reflection | 对应 Runtime 目录 | 已迁移 MOBA 消费方，仅保留弃用兼容 API |
| Markers/DebugDraw | 对应 Runtime 目录 | 待迁出的发现和诊断能力 |

### Starter 推荐 API

| 能力 | 推荐 API |
|------|------|
| 日志 | `Log.SetSink(...)`、`ILogSink` |
| 事件 | `EventDispatcher`、`EventKey` |
| 对象池 | `ObjectPool<T>`、`PoolScope`、`PoolRegistry` |
| 集合 | `StablePriorityList<T>`、`SortedIntSet` |
| 所有权 | `PooledBufferOwner<T>`、`DisposableRegistration` |
| 时间 | `IMonotonicClock`、`MonotonicTime` |

---

## 相关文档

- [AbilityKit 包总览](../../README.md) - 包分级、推荐组合和 Starter 推进顺序
- [属性系统模块](../../com.abilitykit.attributes/Document/) - 持久属性系统
- [Modifiers 包](../../com.abilitykit.modifiers/) - 通用玩法修饰器
- [能力管线模块](../../com.abilitykit.pipeline/Document/) - 技能执行管线
- [触发器模块](../../com.abilitykit.triggering/Document/) - 事件触发系统

---

## 源码路径

```
com.abilitykit.core/Runtime/
├── Buffers/                 # 托管缓冲区所有权
├── Collections/             # 稳定集合
├── Generic/                 # 稳定标识与哈希
├── Lifetime/                # 生命周期注册
├── Logging/                 # 日志抽象
├── Math/                    # 纯 C# 数学类型
├── Timing/                  # 单调时间
├── Event/、Pooling/         # 当前兼容基础设施，后续拆薄
└── Config/、Continuous/、Markers/、Reflection/、DebugDraw/
                              # 待迁出的兼容面（Numerics 已删除）
```

---

*最后更新：2026-08-14*
