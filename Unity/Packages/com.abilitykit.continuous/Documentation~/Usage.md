# Continuous 使用契约与限制

> 文档类型：包使用契约
> 核校状态：2026-11-25 对照 `IContinuous`、配置扩展接口、`DefaultContinuousManager` 和包内测试；E0/E3 为局部 .NET 证据，未验证并发或 Unity Player。

从[包入口](../README.md)开始。该包负责持续对象外壳和管理器中的注册/活动索引，不负责推进持续对象的时间，也不替调用方创建 Buff、修饰器、定时器或快照恢复语义。

## 生命周期与所有者

`IContinuousConfig.OwnerId` 是管理器所有者索引的键。`IContinuous` 自己实现激活、暂停、恢复、结束及 elapsed time；`DefaultContinuousManager` 管理实例注册、准入、批量操作、活动集合和生命周期通知，不提供 `Tick`。

典型流程由宿主装配入口驱动：

1. 创建配置和持续实例，注册到所属管理器。
2. 使用 `TryActivate` 请求激活；拒绝时检查 `LastRejectReason`。
3. 由领域执行器推进实例自己的时间或效果，并按需要调用暂停、恢复、结束或中断。
4. 实例结束事件更新管理器活动状态；宿主在对象失效时显式注销或 `Clear`。

| 操作 | 当前契约 |
| --- | --- |
| `Register` | 同一已注册实例重复注册返回成功；空实例/空配置拒绝。准入策略与索引完成后调用绑定器；绑定器失败会回滚注册、补偿已尝试的绑定器并传播原异常 |
| `TryActivate` | 未注册时先尝试注册，再运行激活准入；被拒绝返回 false。激活后的绑定器或事件回调失败会尝试结束并注销，保留原异常 |
| `TryPause` / `TryResume` | 仅在真实状态转换后更新活动索引并通知绑定器；恢复前重新检查激活准入 |
| `TryEnd` / `TryInterrupt` | 直接调用实例结束/中断；中断会尊重 `CanBeInterrupted`。管理器方法不保证把回调异常转成结果值 |
| `Unregister` / `Clear` | 移除管理器索引和结束订阅并通知绑定器/事件；注销本身不负责释放实例拥有的其他资源 |

注册后的公开 `OnRegistered` 事件在绑定器补偿范围之外；该事件抛错时注册仍已完成。`Unregister` 先移除索引，再通知；通知失败不会还原索引，也不保证后续通知完成。`Clear` 按注册顺序逐个注销，遇到异常立即停止，剩余实例可能仍注册。宿主应记录失败并按自身所有权继续清理。

`GetAllContinuous`、`GetAllActiveContinuous` 返回列表副本，但元素仍是原对象引用；它们不是状态快照。按 owner 查询也不应作为跨线程并发集合使用。

## 可选策略接口

- `IDurationConfig`、`ITagConfig`、`IMutexConfig`、`IHierarchyConfig`、`IStackConfig` 只提供数据契约；并不由 `DefaultContinuousManager` 自动统一实现时长、标签暂停、层叠结束或堆叠规则。
- 可显式注册 `IContinuousAdmissionPolicy`，区分注册准入和激活准入；默认 allow-all 策略不是互斥或玩法阻止策略。
- `IContinuousLifecycleBinder` 用于接入修改器、视图或统计等横切状态。绑定器副作用由调用方设计补偿；异常可能发生在管理器已更新状态之后。
- `IContinuous.End` 引发 `OnEnded`，管理器据此清除活动索引并注销；End/Unregister 是不同操作，不要假设终止会销毁调用方对象或所有者资源。

## 失败和恢复边界

回调与外部事件同步执行，异常可能传播；不要在策略、绑定器或事件中重入改变同一管理器的活动集合，除非调用方已针对具体路径验证。该管理器没有并发保证。

受保护的 `RestoreRegistrationForRollback` 只重建注册、owner/active 索引和内部结束订阅。它不执行准入、生命周期回调、绑定器补偿或公开事件；派生恢复器必须自行处理相关状态和一致性。本轮没有验证任何项目恢复链。

## 源码与证据

- `Runtime/IContinuous.cs`、`IContinuousConfig.cs`：持续对象和配置最小面。
- `Runtime/DefaultContinuousManager.cs`：当前管理器与回调顺序。

2026-11-25 运行现有 `core-stability` Continuous 单步：3/3 通过。这是本地 .NET 镜像的有限 E3 证据，不覆盖 Unity、并发或任一业务恢复实现。
