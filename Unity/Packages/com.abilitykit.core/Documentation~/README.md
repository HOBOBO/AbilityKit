# Core 基础设施文档索引

> 当前迁移状态：DebugDraw 已由 `com.abilitykit.diagnostics` 接管，Core 旧 API
> 仅保留兼容；`DisposeUtils` 和 Marker 全局启动入口不再接受新消费者。

> AbilityKit Core 基础设施包文档入口

---

## 包定位

`com.abilitykit.core` 是 AbilityKit 的 P0 基础底座包，提供上层模块共享的纯 C# 基础设施。它不是业务战斗包，也不是 Demo 包。

当前候选的[首发支持清单](./FirstReleaseSupport.md)区分稳定推荐、高级接口、历史兼容与待评审项。首发推荐的基础面覆盖以下能力：

- 数学基础：`Runtime/Math`。
- 日志：`Runtime/Logging`。
- 稳定集合：`Runtime/Collections`。
- 托管数组所有权：`Runtime/Buffers`。
- 稳定标识：`Runtime/Identifiers`。
- 生命周期注册：`Runtime/Lifetime`。
- 单调时间：`Runtime/Timing`。
- 实例对象池与类型化事件键：`Runtime/Pooling`、`Runtime/Eventing`。

帧索引与顺序缓冲区、池管理和现有事件派发面继续随包交付，不作为默认接入。

`Runtime/Continuous`、`Runtime/Config`、`Runtime/Markers`、
`Runtime/Reflection` 和 `Runtime/DebugDraw` 是迁移中的兼容面，不是新功能落点。
其中 MOBA 的设置与可选模块安装已迁回所属 Demo 包；Core 中的 Config/Reflection
类型仅用于下一主版本前的源码和二进制兼容（`Runtime/Numerics` 已于 2026-08 删除）。

`Runtime/Observability` 的既有原语另列为待准入复核范围：当前缺少三个独立框架使用方的证据，注释与测试完成不代表正式纳入。

---

## 文档列表

### 1. [Core 边界与准入规则](./CoreBoundary.md)

**阅读对象**：新增 Core API、迁移遗留职责或评审跨包基础设施的开发者

**内容概要**：程序集边界、三消费者准入规则、API 兼容门禁、线程安全契约与迁移清单。

### 2. [Marker 注册与扫描兼容契约](./Markers.md)

**阅读对象**：维护既有标记登记、扫描或迁移全局引导入口的开发者。

**内容概要**：类型与键的计数、历史目录、实时及延迟查询、实例创建异常、部分类型加载、扫描入口差异、全局状态重入与所有者迁移。

### 3. [同步事件契约](./Eventing.md)

**阅读对象**：接入事件分发器、管理订阅生命周期或评审参数所有权的开发者。

**内容概要**：同步顺序、快照变更、一次性订阅重入、参数释放、异常边界与最小接入示例。

### 4. [集合、缓冲区与数组所有权契约](./CollectionsAndBuffers.md)

**阅读对象**：使用稳定集合、整数帧缓冲区、顺序缓冲区或租用数组的开发者。

**内容概要**：稳定决胜、完整整数键、容量变化、后端成本、引用释放、视图有效期及数组池异常边界。

### 5. [对象池生命周期与配置契约](./Pooling.md)

**阅读对象**：使用对象池、管理池作用域或配置池创建参数的开发者。

**内容概要**：回调顺序、异常与部分成功、归还责任、容量预留、裁剪策略、作用域释放及配置诊断限制。

### 6. [数学原语契约](./Mathematics.md)

**阅读对象**：使用浮点向量、四元数、三维变换或射线的开发者。

**内容概要**：单位、退化输入、开方分辨率、旋转顺序、方向缩放、逆变换及射线参数。

### 7. [稳定标识与单调时间契约](./IdentifiersAndTiming.md)

**阅读对象**：使用持久化名称标识、局部名称注册表、单调时钟或时长换算的开发者。

**内容概要**：编码与黄金向量、真实冲突、零及负标识、查询失败输出、时钟来源、单位、取整与溢出。

### 8. [定点数学桥接契约](./DeterministicMathBridge.md)

**阅读对象**：使用浮点与定点转换、定点几何或桥接数学函数的开发者。

**内容概要**：严格转换与归零守卫、量化和表示范围、几何退化、中间溢出、函数定义域与依赖内核的跨目标评审项。

### 9. [Core Continuous 兼容与迁移契约](./ContinuousCompatibility.md)

**阅读对象**：维护旧 Core 持续体使用方或迁往独立 Continuous 包的开发者。

**内容概要**：登记与默认相等比较、回调顺序、异常及补偿、活动索引、查询视图、批处理与重入限制，以及目标包的激活失败差异。

### 10. [反射与释放工具兼容契约](./ReflectionAndDisposalCompatibility.md)

**阅读对象**：维护或迁移旧反射调用、资源释放辅助入口的开发者。

**内容概要**：类型解析与程序集遍历、重载歧义、包装异常、异步返回值、四个释放重载的失败结果、引用清空时机及重入和别名限制。

### 11. [配置文件、缓存与模块描述兼容契约](./ConfigurationFileCompatibility.md)

**阅读对象**：维护或迁移旧应用设置文件、持久化配置缓存和模块安装描述的开发者。

**内容概要**：文件失败兜底、直接覆盖与副作用、缓存键和写入时间、对象共享、并发提交与清空边界、默认构造、首个有效模块匹配及所有者迁移。

### 12. [旧 DebugDraw 兼容与迁移契约](./DebugDrawCompatibility.md)

**阅读对象**：维护旧绘制值类型、SceneView 登记或迁往 Diagnostics 的开发者。

**内容概要**：掩码与颜色默认值、静态设置非持久化、公开但未由驱动执行的设置、实时登记遍历、异常隔离范围、形状近似及真实 Unity 验收边界。

### 13. [观测关联原语契约与准入复核](./Observability.md)

**阅读对象**：维护观测标识、同步接收契约或评审 Core 职责的开发者。

**内容概要**：无校验构造、有效性与身份差异、哈希边界、同步接受语义、正式空接收器和独立使用方缺口。

### 14. [旧 Unity 设置引导退役说明](./UnitySettingsBootstrapCompatibility.md)

**阅读对象**：维护旧 Unity 配置引导或迁移应用设置的开发者。

**内容概要**：延迟执行、嵌套协程、路径拼接、首次复制、请求与落盘失败隔离、同步差异、持久层通知及真实平台验证限制。

### 15. [Core 验证门禁与发布缺口](./ValidationAndReleaseReadiness.md)

**阅读对象**：维护 Core 构建、验证依赖闭包或准备发布评审的开发者。

**内容概要**：中文注释和 XML 文档门禁、编译器拒绝验证、Deterministic 依赖、发布计划状态/精确版本、输入与 HEAD/标签内容核对、失败边界与尚未完成的发布证据。

### 16. [Core 与 Deterministic 独立消费验证](./IndependentConsumption.md)

**阅读对象**：准备消费安装、候选替换、回退及平台验证的开发者。

**内容概要**：独立 .NET 本地制品恢复、缺依赖拒绝、真实 Unity tarball 导入、源码归属及尚未覆盖的正式安装与平台场景。

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
| 定点数学桥接 | `Runtime/Mathematics` | 浮点边界到定点内核的现有适配 |
| Logging | `Runtime/Logging` | 日志入口与输出 Sink 抽象 |
| Eventing | `Runtime/Eventing` | 轻量事件发布、订阅和取消订阅 |
| Collections | `Runtime/Collections` | 稳定优先级集合和升序唯一整数索引 |
| Buffers | `Runtime/Buffers` | 有界帧/顺序存储和租用数组所有权 |
| Identifiers | `Runtime/Identifiers` | 版本化稳定哈希和局部名称注册表 |
| Timing | `Runtime/Timing` | 单调时钟、时长计数与毫秒换算 |
| Pooling | `Runtime/Pooling` | 对象池、池作用域、池配置与诊断 |
| Markers | `Runtime/Markers` | 既有标记与注册兼容构件；全局发现及引导已冻结 |
| Continuous | `Runtime/Continuous` | 已弃用的兼容面；新代码使用 `com.abilitykit.continuous` |
| Config/Reflection | 对应 Runtime 目录 | 已迁移 MOBA 消费方，仅保留弃用兼容 API |
| DebugDraw | `Runtime/DebugDraw` | 已迁至 Diagnostics 的旧诊断兼容面 |
| Observability | `Runtime/Observability` | 既有观测关联原语，正式发布前待准入复核 |

### Starter 推荐 API

| 能力 | 推荐 API |
|------|------|
| 日志 | `Log.SetSink(...)`、`ILogSink` |
| 事件 | `EventDispatcher`、`EventKey` |
| 对象池 | `ObjectPool<T>`、`PoolScope`、`PoolRegistry` |
| 集合 | `StablePriorityList<T>`、`SortedIntSet` |
| 标识 | `StableHashV1`、`StableStringIdRegistry` |
| 所有权 | `PooledBufferOwner<T>`、`DisposableRegistration` |
| 时间 | `IMonotonicClock`、`MonotonicTime` |

---

## 相关文档

- [AbilityKit 包总览](../../README.md) - 包分级、推荐组合和 Starter 推进顺序
- [属性系统模块](../../com.abilitykit.attributes/Document/) - 持久属性系统
- [Modifiers 包](../../com.abilitykit.modifiers/) - 通用玩法修饰器
- [能力管线模块](../../com.abilitykit.pipeline/Document/) - 技能执行管线
- [触发器模块](../../com.abilitykit.triggering/Documentation~/README.md) - 事件触发系统

---

## 源码路径

```
com.abilitykit.core/Runtime/
├── Buffers/                 # 有界存储与托管数组所有权
├── Collections/             # 稳定集合
├── Generic/                 # 稳定标识与哈希
├── Lifetime/                # 生命周期注册
├── Logging/                 # 日志抽象
├── Math/                    # 纯 C# 数学类型
├── Mathematics/             # 现有定点数学桥接
├── Timing/                  # 单调时间
├── Unity/                   # 已弃用的 Unity 应用设置引导兼容入口
├── Observability/           # 既有观测关联原语，待准入复核
├── Eventing/、Pooling/         # 当前兼容基础设施，后续拆薄
└── Config/、Continuous/、Markers/、Reflection/、DebugDraw/
                              # 待迁出的兼容面（Numerics 已删除）
```

---

*最后更新：2026-11-18*
