# com.abilitykit.world.di

此包提供一个小型、仅运行时（runtime-only）的依赖注入容器，用于 AbilityKit 的 World 体系。

## Concepts

## 概念

### WorldLifetime

`WorldLifetime` 用于控制实例的缓存/生命周期边界。

- **Singleton**
  - 每个 `WorldContainer` 只有一个实例。
  - 在第一次 `Resolve` 时创建。
  - 在 `WorldContainer` 被 `Dispose` 时释放。
  - 适用于无状态服务、共享注册表、配置单例等。

- **Scoped**
  - 每个 `WorldScope` 只有一个实例。
  - 在该 scope 内第一次 `Resolve` 时创建。
  - 在 `WorldScope` 被 `Dispose` 时释放。
  - 适用于对局/会话态服务、world tick 相关服务、每个 world 的缓存等。

### WorldScope

`WorldScope` 表示 scoped 服务的运行时生命周期边界。

- `WorldContainer` 可以创建多个 scope（通常每个逻辑 world 实例一个 scope）。
- scoped 服务会缓存到 scope 内部。
- scope 被释放时，会释放所有实现了 `IDisposable` 的 scoped 实例。

实践建议：

- 在 world 初始化时创建一次 scope，并在整个 world 生命周期内复用。
- 避免在每帧创建大量 scope。
- 如果服务持有原生资源/订阅等，应实现 `IDisposable`，以便 `WorldScope.Dispose()` 能正确清理。

### IWorldResolver

`IWorldResolver` 是用于解析服务的“窄接口”（推荐依赖面）。

- `Resolve<T>()` / `Resolve(Type)`
  - 适用于“必选依赖”。
  - 若服务未注册会抛异常。
  - 建议只在组合边界（composition root / installers 等）使用，或作为迁移期的临时桥接。

- `TryResolve<T>(out T)` / `TryResolve(Type, out object)`
  - 适用于“可选依赖”。
  - 若服务未注册会返回 `false`。
  - 仅当依赖确实可选且代码有合理 fallback 时使用。

推荐用法：

- **优先使用构造函数注入（constructor injection）**
  - 注册类型后，让容器使用“最合适的 public 构造函数”来创建实例。
  - 这样依赖更显式，代码也更易测试。

- **只在边界使用 `IWorldResolver`**
  - 例如 world modules、systems installers、bootstrap/composition 代码。
  - 避免把 `IWorldResolver` 传入过深的 gameplay 逻辑里。

补充：

- **通常不需要关心“模块注册顺序”**
  - `Configure(...)` 阶段只是注册 `serviceType -> factory`，不会立刻创建实例。
  - 真正创建实例发生在 `Resolve(...)`（首次解析或构造函数触发解析）阶段。
- **要重点关注：解析时机 / 循环依赖 / 生命周期穿透**
  - 若在 compose 过程中提前 `Resolve`，而依赖尚未注册完，会失败。
  - 构造函数里互相 `Resolve` 容易形成 `A -> B -> A` 循环依赖。
  - 避免 singleton 在构造时解析 scoped（可能导致 scoped 实例泄漏到 singleton 生命周期）。

- **避免 service-locator 风格**
  - 不要新增 `Get<T>()` / `TryGet<T>()` 这类模式。
  - 优先使用 `Resolve/TryResolve` 或构造注入。

## 推荐架构：World / Modules / Services / Systems

`world.di` 的核心目标是把“组合（composition）”和“业务执行（runtime logic）”分离：

- **World modules**：组合边界（composition root）的可拆分子块，负责“安装/注册/接线”。
- **Services**：可被业务依赖的能力（有明确接口与生命周期），推荐构造函数注入。
- **Systems / Runners**：驱动执行（tick/reactive/pipeline），依赖 services，不负责注册。

### 粒度（模块通常是什么级别）

模块粒度通常介于“整个 world 类型”和“具体一个 service/system”之间，常见拆分方式：

- 以子域拆分：`CombatModule` / `MovementModule` / `SkillModule` / `ProjectileModule` / `SummonModule`
- 以集成拆分：`EntitasIntegrationModule` / `HotReloadModule`
- 以复用包拆分：某个 package 对外提供一个模块，供不同 world 类型按需组合

推荐准则：**以依赖闭包 + 可开关复用为准**。

### 模块/服务/系统的依赖关系（推荐）

```mermaid
flowchart TD
  A[WorldFactory / Compose] --> B[WorldContainer (Root)]
  B --> C[WorldScope (per world)]

  A --> M1[IWorldModule: BaseModule]
  A --> M2[IWorldModule: CombatModule]
  A --> M3[IWorldModule: SkillModule]

  M1 --> R1[Register infra services]
  M2 --> R2[Register combat services]
  M3 --> R3[Register skill services]

  C --> S1[Resolve domain services]
  S1 --> SYS1[Systems / Pipelines]

  subgraph Lifetimes
    L1[Singleton: per container]
    L2[Scoped: per scope]
    L3[Transient: per resolve]
  end
```

关键约束：

- **模块只负责注册/安装**，不要承载业务。
- **业务层尽量不要持有 `IWorldResolver`**，优先依赖具体接口（构造注入）。
- `IWorldResolver` 推荐只出现于：模块、installer、bootstrap、临时迁移桥接。

### 推荐案例：一个对局 world 的模块树

下面是一个“对局/会话态 world”（例如 MOBA 对局）常见的模块组合案例：

```mermaid
flowchart TD
  W[MobaWorld] --> Base[BaseModule]
  W --> ECS[EntitasIntegrationModule]
  W --> Time[TimeModule]
  W --> Events[EventBusModule]
  W --> Units[UnitModule]
  W --> Combat[CombatModule]
  W --> Skill[SkillModule]
  W --> Projectile[ProjectileModule]
  W --> Summon[SummonModule]
  W --> Debug[DebugModule (optional)]

  Combat -->|depends on| Units
  Skill -->|depends on| Events
  Projectile -->|depends on| Combat
  Summon -->|depends on| Units
```

### 模块 vs 服务：能不能“把模块写成服务”？

很多“子域模块”（例如 `CombatModule` / `SkillModule`）确实也可以用一个“域门面服务（facade service）”来承载对外入口，这在功能上通常没问题。

但建议保留 `IWorldModule` 概念：

- 模块负责 **注册/安装/接线**（compose 阶段），不承载业务状态。
- 服务负责 **运行时能力**（resolve 后参与 tick/逻辑），推荐构造函数注入。

把模块完全等价成服务，往往会让“注册/接线”迁移到运行时对象里，增加 service-locator 的诱因，并降低依赖图治理能力。

### 多个模块注册了相同服务会怎样？

以 `WorldContainerBuilder` 的实现为准：

- `Register(...)`
  - **后注册覆盖先注册**（同一个 `serviceType` 只保留最后一次注册）。
- `TryRegister(...)`
  - **先注册生效，后续忽略**（如果 `serviceType` 已存在）。

实践建议：

- 如果你希望“基础模块提供默认实现，业务模块可覆盖”，可以：
  - 基础模块用 `TryRegister` 注册默认实现
  - 更高层模块用 `Register` 显式覆盖
- 如果你希望“禁止重复注册”（强约束、便于排错），建议在组合边界自己做治理：
  - 约定同一 `serviceType` 只能由一个模块注册
  - 或在 compose 阶段加一层校验/日志（例如输出冲突来源）

### 示例

常用/基础用法示例见：`Examples/`。

## Troubleshooting

## 排查

- **服务未注册**
  - `Resolve<T>()` 在服务未注册时会抛异常。
  - 请在 world module（`IWorldModule.Configure`）中注册，或确保创建 world 时包含了对应模块。

- **模块依赖存在循环**
  - 如果模块依赖图存在环，compose 会失败，并且异常信息会包含 cycle path。

## Debug / Diagnostics

## 调试 / 诊断

`WorldCompositionReport` 与 `WorldDebugRegistry`（命名空间 `AbilityKit.Ability.World.Diagnostics`）用于在运行时记录/查询组合信息。

- 安装了哪些 modules
- 执行了哪些 installers
- 注册了哪些 service types
