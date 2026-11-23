# com.abilitykit.triggering

Triggering 将类型化事件与数据化计划连接起来。运行时负责事件匹配、条件评估与计划调度；事件含义、领域动作、配置组织和宿主生命周期由接入项目决定。

## 接入

- Unity 包直接依赖 `core`、`continuous`、`modifiers`、`world.di`、`deterministic`、`analyzer`，以及供运行时读取 JSON 的 `com.unity.nuget.newtonsoft-json`。
- 只用事件通知可直接使用 `EventBus`；条件、顺序和数据化规则再按需接入 `TriggerRunner<TCtx>`。从 Package Manager 导入 **Triggering Mainline**，即可运行[即时样例](Samples~/FormalMainline/FormalTriggeringMainlineExample.cs)，不要求宿主装配调度。业务定时规则单独导入 **Triggering Rule Scheduling**；正式与兼容入口见[入口清单](Documentation~/FormalApiBoundary.md)。
- 需要验证队列派发与排序时，可单独导入 **Triggering Queued Events**，见[运行说明](Samples~/QueuedEvents/README.md)。
- `Runtime/Abstractions/` 持有共享抽象；在 Unity 和 .NET 中均只由独立程序集定义。.NET 兼容转发保留旧程序集限定名的解析；反射按程序集枚举定义时，应访问抽象所属程序集。

维护代码先看[源码目录](Documentation~/SourceLayout.md)；验收指引、编辑器诊断和迁移资料见[包内文档索引](Documentation~/README.md)。
