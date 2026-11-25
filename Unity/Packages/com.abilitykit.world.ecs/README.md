# AbilityKit World ECS

`com.abilitykit.world.ecs` 提供轻量实体与组件容器，包括创建、销毁、父子关系和事件。不负责宿主时钟或系统调度，也不替代第三方 ECS。

## 安装与接入

从项目配置的包来源安装本包；Unity 版本及依赖见 [`package.json`](package.json)。

```csharp
using AbilityKit.World.ECS;

var world = new EntityWorldBuilder().Build();
var entityId = world.Create("example").Id;
world.Destroy(entityId);
var isAlive = world.IsAlive(entityId);
```

销毁后 `isAlive` 为 `false`。包内的身份、资源和线程边界见[文档索引](Documentation~/README.md)与[使用契约与限制](Documentation~/Usage.md)。

`EntityWorld` 当前没有整体 `Dispose` 入口；资源组件、事件订阅和世界对象的持有关系由应用管理。本轮按当前源码核校，未执行 Unity Editor/Player 测试。
