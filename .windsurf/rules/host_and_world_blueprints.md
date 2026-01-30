---
trigger: manual
---

# Host + WorldBlueprints 规则

## 0. 目标

本文用于说明 `com.abilitykit.host` 的架构意图，以及 World Blueprints 的推荐使用方式。

## 1. 包层级与依赖方向

- `com.abilitykit.host` 属于 **框架/运行时基础设施**。
- Host 包不得依赖任何 gameplay 包（例如 `com.abilitykit.demo.*`, `com.abilitykit.game.*`）。
- Gameplay 包可以依赖 `com.abilitykit.host`。

## 2. Host 应该包含什么

Host 的职责：
- world 生命周期 + tick 驱动 + 路由
- 基于 world capability 的 driver 选择
- 横切型 host module（time/rollback/record/metrics）
- 传输抽象（connection + message）

Host 不应：
- 写死任何 gameplay 规则
- 包含 gameplay 特有的 world 组装/初始化逻辑

## 3. WorldType 是 gameplay 合同

- 不同玩法/世界的差异应通过以下方式表达：
  - `WorldType` 字符串（合同/协议）
  - `WorldCreateOptions` 的组合（modules + services + extensions）

## 4. World Blueprints

### 4.1 目的

Blueprints 用于避免在各个创建 world 的调用点散落“临时装配代码”。

`IWorldBlueprint.Configure(WorldCreateOptions)` 通常负责：
- `options.ServiceBuilder` 的默认构建/补齐
- `options.Modules.Add(...)`
- `options.Extensions[...]`（例如 Entitas contexts factory）

### 4.2 注册风格

推荐 **应用侧显式注册**：

- 每个 gameplay 应用包提供一个唯一入口：
  - `RegisterAll(WorldBlueprintRegistry registry)`

Host 不做反射扫描（避免隐式全局状态与不可控的装配）。

### 4.3 Factory 接线（wiring）

使用 blueprints 时，world factory 推荐按如下组合：

- `WorldTypeRegistry`（runtime world 实现选择：Entitas/其他）
- `RegistryWorldFactory`（基础工厂）
- `WorldBlueprintWorldFactory`（创建前应用 blueprint）
- `WorldManager`（管理多个 world）

## 5. Capabilities

Host 与 gameplay 的交互必须通过 `world.Services` 中解析出来的 capability：

- `IWorldInputSink`
- `IWorldStateSnapshotProvider`
- `IWorldPlayerLifecycle`

除非存在明确的 Host↔World 交互需求，否则不要随意新增 capability。
