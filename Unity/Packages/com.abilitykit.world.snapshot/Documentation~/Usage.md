# Snapshot 使用指南

> 文档类型：使用指南
> 事实基线：2026-11-25，当前包源码。
> 核校状态：已核校下述路由与装配范围；Unity、网络接入和完整世界恢复未验证。

## 最小派发

以下完整示例只处理本地生成的单字节载荷；操作码 `1` 是示例约定，不是正式协议分配。

```csharp
using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Snapshots.Routing;

using var dispatcher = new FrameSnapshotDispatcher();
dispatcher.Register<int>(1, Decode);
int received = 0;
using var subscription = dispatcher.Subscribe<int>(1, (_, value) => received = value);
dispatcher.Feed(new FramePacket(new WorldId("local"), new FrameIndex(0),
    Array.Empty<PlayerInputCommand>(), new WorldStateSnapshot(1, new byte[] { 42 })));
if (received != 42) throw new InvalidOperationException("未收到预期载荷");

static bool Decode(in WorldStateSnapshot snapshot, out int value)
{
    value = 0;
    if (snapshot.Payload == null || snapshot.Payload.Length != 1) return false;
    value = snapshot.Payload[0];
    return true;
}
```

## 注册与执行顺序

- `Register<T>` 按操作码绑定类型；同码同类型会替换 decoder，同码不同类型抛异常。先注册，再 `Subscribe<T>` 或 `AddStage<T>`；找不到路由或类型不匹配会抛异常。
- `Feed(null)` 无动作。非空信封先发 `FrameReceived`；有快照时再发 `SnapshotReceived`，随后运行 dispatcher 的类型化路由。仅在 dispatcher 找不到路由时发 `NoRouteForOpCode`；decoder 返回假不触发该事件。
- `SnapshotPipeline` 在构造时订阅 `SnapshotReceived`，拥有独立 decoder 表。只注册 dispatcher decoder 不会同时注册 pipeline decoder。阶段按 `order` 升序、同序按注册顺序执行。
- 同一委托可多次订阅或添加阶段；每次返回独立、可重复释放的令牌。`SnapshotCmdHandler` 则每个操作码只允许一个注册，重复抛异常。

## 所有权与失败

`SnapshotRoutingBuilder.Build` 按注册项顺序调用 `RegisterAll`；启用集合只筛选有 `RegistryId` 的注册项，无标识项总是参与。目录拒绝重复 ID。

自有 dispatcher 的装配实例持有 pipeline、命令订阅和 dispatcher；外部 dispatcher 重载只借用它，`Snapshots` 为 null，通过 `ExternalDispatcher` 访问。装配失败撤销本次 pipeline 订阅和命令订阅，但不会回滚已写入外部 dispatcher 的 decoder；它不是事务。

`FrameSnapshotDispatcher.Dispose()` 当前为空实现，不清理路由、不终止后续 `Feed`。业务必须保存并释放直接订阅令牌、解除自身事件订阅；不能把 `Dispose` 当作终态或在途回调屏障。`SnapshotPipeline.Dispose` 解除其事件订阅；`SnapshotCmdHandler.Dispose` 撤销其持有的订阅。

decoder 返回假会跳过该路由；decoder 抛错及直接事件订阅抛错向外传播，可能阻断后续派发。类型化 handler 和 pipeline stage 的异常分别记录后继续执行后续项，不撤销已发生的副作用。需要原子导入的业务不能依赖这些异常隔离完成事务。

本包不深拷贝信封、快照字节数组或解码对象。输入生命周期、异步留存副本与业务状态导入由调用方负责。注册、派发、撤销均在同一执行线上安排；不在回调中修改正在遍历的注册集合，不承诺并发或重入安全。

## 核校依据

已对照 [FrameSnapshotDispatcher](../Runtime/SnapshotRouting/FrameSnapshotDispatcher.cs)、[SnapshotPipeline](../Runtime/SnapshotRouting/SnapshotPipeline.cs)、[装配器](../Runtime/SnapshotRouting/SnapshotRoutingBuilder.cs)与[装配实例](../Runtime/SnapshotRouting/SnapshotRoutingInstance.cs)。完整示例验证与跨包测试范围由仓库日期化记录维护；本指南不把本地路由结果扩展为完整恢复能力。
