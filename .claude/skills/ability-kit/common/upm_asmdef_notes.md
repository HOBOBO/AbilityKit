# UPM / asmdef notes

## asmdef 引用不传递（重要）

Unity 默认 asmdef `references` 非传递。缺类型时**优先补 asmdef references**，而不是只加 `using`。

### 实例：`com.abilitykit.demo.moba.runtime.asmdef`（34 条 references）

引用 `AbilityKit.Ability` 不会自动得到其下游包。`moba.runtime` 显式列出全部下游：

```
AbilityKit.Core
AbilityKit.Attributes
AbilityKit.Modifiers
AbilityKit.GameplayTags
AbilityKit.World.DI
AbilityKit.World.FrameSync
AbilityKit.World.ECS
AbilityKit.World.Snapshot
AbilityKit.World.StateSync
AbilityKit.Combat.Damage         ← AbilityKit.Ability 已引用，仍需在此声明
AbilityKit.Combat.EntityManager  ← 同上
AbilityKit.Combat.Targeting      ← 同上
AbilityKit.Combat.Collision.Abstractions
AbilityKit.Combat.Projectile
AbilityKit.Triggering
AbilityKit.Pipeline
AbilityKit.Host
AbilityKit.Host.Extension
... (共 34 条)
```

## World DI 注册路径（两条）

World DI（`com.abilitykit.world.di`）有两种注册方式：

### 方式 1：attribute 扫描

```csharp
using AbilityKit.Ability.World.Services;

[WorldService(lifetime: WorldLifetime.Singleton)]
public sealed class MyService : IMyService { ... }
```

定义在 `Unity/Packages/com.abilitykit.world.di/Runtime/World/Services/Attributes/WorldServiceAttribute.cs`，默认 `WorldLifetime.Scoped`。配合 `AttributeWorldServicesModule` 自动扫描注册。

### 方式 2：module 显式注册

```csharp
public sealed class MyWorldModule : IWorldModule
{
    public void Configure(WorldContainerBuilder builder)
    {
        builder.TryRegisterType<IEventBus, EventBus>(WorldLifetime.Singleton);
    }
}
```

通过 `WorldCreateOptions.Modules.Add(IWorldModule)` 装载，或由 `HostRuntimeModuleHost.Add(module)` 添加。

### 关键陷阱

- 若某服务由 module 注册（例如 `IUnitResolver`、`IEventBus`），**必须确保 module 被装载**，否则运行时 DI 会报"dependencies are registered"。
- moba 的 `WorldModulesStage`（`com.abilitykit.demo.moba.runtime/Runtime/Application/Systems/Bootstrap/Flow/Stages/`）负责注册第二套 `IEventBus` + `TriggerRunner<IWorldResolver>` + 各 Registry。
- ability 包的 `DefaultWorldServicesModule`（`com.abilitykit.ability/Runtime/Ability/World/Services/`）负责注册第一套 `IEventBus` + `TriggerRunner`。

## 包名变更历史（避免误引用旧路径）

- `com.abilitykit.ability.runtime` → **`com.abilitykit.ability`**（去掉 `.runtime` 后缀）
- `com.abilitykit.demo.moba.runtime/Runtime/Ability/Share/Impl/Moba/` → **`Runtime/Application/Services/`** + **`Runtime/Application/Systems/`**
- `com.abilitykit.pipeline/Runtime/Ability/Share/Pipeline/` → **`Runtime/Core/Pipeline/`**
