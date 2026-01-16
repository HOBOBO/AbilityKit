# Triggering Blackboard 接入说明

本文档说明 Triggering 模块的 Blackboard 设计目的、实现边界、以及实现层（Entitas/其他 ECS/纯业务代码）应该如何接入，来支持：

- Trigger 表达式/数值变量通过 `domainId.key` 访问不同生命周期的数据
- “快照（snapshot）”在特定时机把计算结果写入某个实例黑板，后续持续读取该快照值

## 1. 概念与职责边界

### 1.1 纯逻辑层：`IBlackboard`
Blackboard 是一个通用的 key-value 存储容器，框架层不关心它挂载在什么对象上。

- 接口：`IBlackboard`
- 默认实现：`BlackboardImpl`

约束：

- key 使用 `string`
- value 使用 `object`
- Numeric 相关读取通过 `TryGetDouble` 统一做 `Convert.ToDouble`（失败返回 false）

### 1.2 实现层：`IBlackboardResolver`
实现层负责把“某个 boardId（字符串）”解析为“当前上下文下的实例黑板”。

- 接口：`IBlackboardResolver`
- 参考实现：`BlackboardResolver`（仅做 Dictionary 映射，适合纯逻辑/测试场景）

框架层不会依赖 Entitas，也不会假设有 Actor/Skill/Effect 等类型；这些映射全部由实现层决定。

### 1.3 触发器上下文：`TriggerContext.Services`
框架层通过 `TriggerContext.Services` 获取 `IBlackboardResolver`，入口方法：

- `TriggerContextBlackboardExtensions.TryResolveBlackboard(boardId, out bb)`

实现层需要保证构造 `TriggerContext` 时，`Services` 中能 `GetService(typeof(IBlackboardResolver))` 返回对应 resolver。

## 2. 与 NumericVar / Expr 的接入

### 2.1 NumericVar 的 domainId
NumericVar/Expr 通过 `domainId + key` 访问变量，例如：

- `actor.atk`
- `effect.snap.damage`

其中 `domainId` 需要在 `INumericVarDomainRegistry` 中存在对应域。

### 2.2 Blackboard 数值域：`BlackboardNumericVarDomain`
`BlackboardNumericVarDomain(domainId, boardId)` 将某个 `domainId` 映射到某个 `boardId` 的 blackboard。

- `TryGet`：`resolver.TryResolve(boardId)` -> `bb.TryGetDouble(key)`
- `TrySet`：`resolver.TryResolve(boardId)` -> `bb.Set(key, value)`

注意：当前框架默认 registry 仅注册 `local/global`，黑板域建议由实现层显式注册（便于按项目定制）。

## 3. 推荐的 boardId / domainId 约定

提供常量：`BlackboardIds`（仅为推荐，非强制）

- `actor`：Actor 实例黑板
- `skill`：Skill 实例黑板（一次施放/技能流程）
- `effect`：Effect 实例黑板（推荐用于快照/持续效果）
- `projectile`：Projectile 实例黑板
- `battle`：Battle session 黑板
- `global`：全局黑板（慎用）

一般情况下建议 `domainId == boardId`，这样表达式前缀与解析 id 一致。

## 4. 快照（Snapshot）策略

### 4.1 核心思路
快照的本质是：在某个时机（例如效果 Apply / 命中 / 创建投射物时）把“当时计算出来的值”写入某个实例黑板的特定 key。

后续持续效果读取该 key，就等同于读取快照。

该策略完全合理，并且强解耦：

- 框架层只负责表达式求值与统一读写接口
- 实现层负责决定写入时机、写入目标（哪个实例 blackboard）与 key 命名

### 4.2 Key 命名建议
可使用前缀约定避免冲突：

- `snap.`：快照值

常量：`BlackboardNumericKeys.SnapshotPrefix = "snap."`

示例：

- `effect.snap.damage`
- `effect.snap.atk_on_apply`

### 4.3 辅助工具
- `BlackboardUtil.CopyKeys(from, to, keys)`：按 key 列表拷贝值（可用于批量快照）

## 5. 实现层接入清单（后续联调时按此做）

### 5.1 注入 `IBlackboardResolver`
实现层在创建 `TriggerContext` 时：

- 提供一个 `IServiceProvider`，其 `GetService(typeof(IBlackboardResolver))` 能返回 resolver
- resolver 能根据 boardId 解析到当前上下文对应实例的 blackboard

### 5.2 注册 blackboard domain
在你使用的 `INumericVarDomainRegistry` 中注册：

- `new BlackboardNumericVarDomain("actor", "actor")`
- `new BlackboardNumericVarDomain("effect", "effect")`
- `new BlackboardNumericVarDomain("skill", "skill")`
- etc.

之后表达式即可使用：

- `actor.atk * 1.2`
- `effect.snap.damage + 10`

### 5.3 生命周期
建议：

- `effect` / `skill` / `projectile` 等实例黑板随实例生命周期销毁
- 避免把快照写到 `global` 或共享 actor 黑板，除非明确需要跨效果共享
