# com.abilitykit.triggering

> 文档类型：包入口与扩展导航
> 核校状态：2026-11-25 对照包声明、正式 API 边界、主线样例和现有 Triggering 测试；使用说明按其页面范围阅读，本轮未重跑 Unity 样例。

Triggering 将类型化事件与数据化计划连接起来。运行时负责事件分派、条件评估与规则执行；事件含义、领域动作、配置组织和时钟由接入项目决定。

## 接入

- Unity 包直接依赖 Core、Continuous、Modifiers、World DI、Deterministic 和 Newtonsoft JSON；精确版本以 [package.json](package.json) 为准。本轮该声明中没有 `analyzer` 依赖。
- 只用事件通知可直接使用 `EventBus`；条件、顺序和数据化规则再按需接入 `TriggerRunner<TCtx>`。从 Package Manager 导入 **Triggering Mainline**，即可运行[即时样例](Samples~/FormalMainline/FormalTriggeringMainlineExample.cs)，不要求宿主装配调度。独立业务定时由宿主负责；正式与兼容入口见[入口清单](Documentation~/FormalApiBoundary.md)。
- 需要验证队列派发与排序时，可单独导入 **Triggering Queued Events**，见[运行说明](Samples~/QueuedEvents/README.md)。
- `Runtime/Abstractions/` 持有共享抽象；在 Unity 和 .NET 中均只由独立程序集定义。.NET 兼容转发保留旧程序集限定名的解析；反射按程序集枚举定义时，应访问抽象所属程序集。

纯事件和即时规则不需要调度。扁平计划需要延迟或周期时，宿主可实现 `IFlatActionScheduler`，也可显式安装 `com.abilitykit.triggering.actionscheduling` 并注入其默认实现；执行树时间节点仍使用核心包内的独立入口。维护代码先看[源码目录](Documentation~/SourceLayout.md)；验收指引、编辑器诊断和迁移资料见[包内文档索引](Documentation~/README.md)。
