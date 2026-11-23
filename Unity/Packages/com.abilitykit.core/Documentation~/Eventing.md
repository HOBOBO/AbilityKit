# 同步事件契约

源码统一位于 `Runtime/Eventing`，命名空间保持 `AbilityKit.Core.Eventing`。事件通道由整数标识与参数静态类型共同区分；字符串名称先通过 `StableStringIdRegistry` 映射为稳定 V1 标识，因此字符串入口与对应整数入口共享通道。

`EventDispatcher.cs` 管理通道登记、发布入口和参数释放；同一类型的 `EventDispatcher.Channel.cs` 承载私有通道、监听器排序及快照分发，不引入第二套派发实现。

## 作用域与调用顺序

`EventDispatcher` 在发布线程同步执行回调，优先级较高者先执行，同优先级保持注册顺序。它不保证线程安全；订阅、退订、发布和清空由单一所有者管理，或在整个操作外同步。`GlobalEventDispatcher` 共享进程实例，不自动提供战斗世界、场景或测试隔离。

多监听器发布使用当次快照。回调中新增订阅从后续发布开始生效；已经进入当前快照的普通监听器仍可能在退订后执行。`Clear` 清空后续通道查找，但不撤销当前已经取得的普通回调快照；旧句柄不能误删重新创建的订阅。

`once` 监听项在调用前从实时列表移除，并在执行回调前记录单次执行状态。内层发布已经消费的监听项不会被外层旧快照再次执行，回调抛出异常后也不会重试。这不限制普通回调的嵌套发布，也不提供全局递归深度保护。

## 参数所有权与失败行为

`Publish` 默认启用 `autoReleaseArgs`。即使没有监听器，合法事件入口也会在分发结束时进入释放流程：

| 参数情况 | 行为 |
|---|---|
| 实现 `IDisposable` | 调用 `Dispose`，隔离其异常，不再尝试对象池归还 |
| 未实现 `IDisposable` 且存在全局池归还句柄 | 调用 `Pools.TryRelease`；分发器不额外捕获该操作抛出的异常 |
| 未成功归还且实现 `IPoolable` | 调用 `OnPoolRelease` 并隔离其异常，不表示已进入某个对象池 |
| 其他参数 | 不额外处理 |

处理器异常会被隔离，后续监听器继续执行；接口不返回处理成功结果。字符串订阅或注册入口拒绝空引用名称，但允许空字符串；字符串发布入口遇到空引用名称时直接返回，参数仍由调用方持有，不会自动释放。

关闭自动释放时，最终释放责任由调用方承担。启用自动释放后，监听器不得长期保留参数引用；需要跨帧使用时复制必要数据。嵌套发布同一个参数对象时，调用方必须避免多次自动释放。

## 订阅句柄与键值

`IEventSubscription` 只提供幂等的 `Unsubscribe`，不继承 `IDisposable`，也不负责释放回调目标。所有者卸载时应显式退订。

`EventKey.cs` 声明带运行时参数类型的非泛型键；`EventKey.Generic.cs` 声明 `EventKey<TArgs>`，在类型参数中表达参数类型。二者保留各自的构造和相等语义。字符串键与整数键不因显示内容相同而相等；默认结构值不等同于显式构造的零值整数键。其 `GetHashCode` 只适用于运行时集合，不是持久化或协议标识。

当前分发器接收字符串或整数事件标识，参数类型通过泛型调用隔离；键值类型的存在不表示分发器提供以键值类型为参数的发布或订阅重载。

## 最小示例

```csharp
using AbilityKit.Core.Eventing;

var dispatcher = new EventDispatcher();
var lastValue = 0;
var subscription = dispatcher.Subscribe<int>(
    "sample.value",
    value => lastValue = value);
try
{
    dispatcher.Publish("sample.value", 7, autoReleaseArgs: false);
}
finally
{
    subscription.Unsubscribe();
}
```

发布队列、线程切换、跨网络传输、持久化、世界生命周期及具体事件目录由所属模块负责。准入及兼容规则见 [Core 边界](CoreBoundary.md)。
