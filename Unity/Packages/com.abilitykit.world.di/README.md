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

- **避免 service-locator 风格**
  - 不要新增 `Get<T>()` / `TryGet<T>()` 这类模式。
  - 优先使用 `Resolve/TryResolve` 或构造注入。

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
