# AbilityKit Foundation Starter

Foundation 组合（`com.abilitykit.core` + `com.abilitykit.world.di`）的最小可运行入口。
只依赖这两个包，不依赖任何 demo / combat / network 包，纯 C# 控制台即可运行。

## 运行

```bash
dotnet run --project src/AbilityKit.Samples.Foundation
```

## 它演示了什么

| 能力 | 包 | 在示例中的位置 |
|---|---|---|
| 日志（ILogSink 扩展点 + Log 静态门面） | core | `ConsoleLogSink`：默认 sink 是 NullLogSink，接入方先 `Log.SetSink(...)` 让日志可见 |
| 事件（EventDispatcher 订阅 / 零分配 struct 发布） | core | `EntitySpawnedEvent` + `SampleEventIds` |
| 对象池（Pools / ObjectPool<T> 租借归还） | core | `PooledSpawnSystem`：实体从池租借，存活期结束归还 |
| World 服务注册（IWorldModule + WorldContainerBuilder） | world.di | `GameplayModule.Configure`：单例注册、工厂注入、生命周期三种玩法 |
| 模块装配（WorldModulePlanner 依赖排序） | world.di | world factory 中 `plan.Entries` 逐个 `AddModule` |
| 生命周期钩子（OnInit / OnDeinit 自动调用） | world.di | `PooledSpawnSystem` 实现 `IWorldInitializable` / `IWorldDeinitializable` |
| 世界管理（WorldManager 创建 / Tick / 销毁） | world.di | `Program`：`Create → Tick 循环 → Destroy` |

## 结构

```
Program.cs          入口：装配 registry / manager / module，订阅事件，驱动 60 帧 Tick
FoundationWorld.cs  最小 IWorld 实现：world.di 只提供抽象，具体世界由接入方实现
GameplayModule.cs   业务模块 + 服务：事件、对象池、生命周期钩子的组合示例
```

## 下一步

- **SkillCore**：在 Foundation 之上加入 `triggering` + `pipeline` + `attributes`，
  展示一次技能释放、属性变化和触发规则（对应 `Samples.Logic` 中已有的示例）。
- **BattleRuntime**：再加入 `combat.targeting` / `combat.projectile` / `combat.damage`，
  展示可测试的命中链路。

组合分级的完整定义见 `Unity/Packages/README.md` 的「内部推广分级」与「推荐组合」。
