# 双连接客户端会话使用指南

> 文档类型：使用指南
> 事实基线：2026-11-25，当前包源码。
> 核校状态：部分核校；覆盖两连接装配、交付取消与释放，完整业务恢复未运行。

安装条件见[包入口](../README.md)。`MultiplayerClientSession` 持有 Room 控制面与 Battle 数据面，传递 token、battleId、roomId 并协调订阅。它不提供具体玩法的输入、状态导入、预测或重演算法。

## 接入顺序

`EnterAsync` 先运行房间流程，再按 `attachBattle` 挂接战斗面；也可传 `MultiplayerClientSessionOptions`，分别配置 RoomConnection、RoomFlow 与 Battle。launch spec 必须匹配服务端支持的 gameplay/ruleset/config/protocol/world 身份，加载和选角由流程钩子安排，不能使用任意示例身份期待开战成功。

以下为接入片段：`host`、`port`、`accountId`、`launchSpec`、`DecodeSnapshot` 和取消令牌由应用提供；需要访问 Unity 对象时还需提供会持续泵送的主线程 dispatcher。此处的 ID 转换只适用于十进制字符串身份。

```csharp
using var client = await MultiplayerClientSession.EnterAsync(
    host, port, accountId, launchSpec,
    configureBattle: (config, session) => config
        .UseRoomGatewayStateSyncInput(session.BattleId,
            player => uint.Parse(player.Value), world => ulong.Parse(world.Value))
        .WithSnapshotDeserializer(DecodeSnapshot),
    cancellationToken: cancellationToken);
client.Battle.StateSyncSnapshotPushed += snapshot => { /* 交给项目状态接收流程 */ };
// 宿主持续调用：client.Tick(realWallClockDelta);
```

`configureBattle` 有两个参数 `(config, sessionResult)`。config 已预填端点、连接来源、token 与 Room Gateway 预设，回调补玩法 codec，不调用 Build，也不自行覆盖成另一场战斗身份。参数、返回类型和命名空间见[源码](../Runtime/MultiplayerClientSession.cs)。

## 推送绑定与挂接时机

`attachBattle: true` 跳过房间侧 SubscribeStateSync，所以 Room 结果的 Subscribed 为 false 是预期行为。对应服务器 observerKey 的推送槽由战斗面持有；连接成功之后再由 Room 侧重复订阅可能覆盖战斗流。该纪律不决定主同步模式，也不代表新基线已提交。

一站式 Enter 在返回前已调用 Battle.Connect。调用者返回后才添加的事件订阅可能错过同步到达的初始通知；需要先装配接收器再建连时，使用分步构造并调用 `AttachBattle(configureBattle, connect: false)`，订阅后显式 Connect。该方法返回不代表鉴权完成，业务还需检查 `ConnectionAuthenticated`、首个权威输入/基线与恢复资格。

`AttachBattle` 只允许一次成功挂接，失败不会释放房间侧；成功的 transport 由 session 持有。独立调用静态 `CreateBattleTransport` 时，成功返回的 transport 由调用者持有，立即 Connect 抛错时辅助方法清理已创建资源。构造成功后，传入的 Room session 或 SDK 所有权交给客户端 session，不能再让外部所有者重复释放。

## 时间、取消与关闭

`Tick` 依次泵送 Room、Battle，使用真实墙钟间隔；默认 dispatcher 不保证主线程，注入队列也不免除泵送责任。场景暂停、快进或断线不能将模拟 delta 当网络计时，也不应重复由多个宿主 Tick 同一 SDK。各会话操作串行执行，不支持并发调用。

Enter 在交付前检查取消；房间完成后取消或挂接失败会清理已取得连接。令牌不是已交付会话的永久停止开关；交付之后由调用者显式结束会话。本地取消与释放不能撤销服务端已接受的建房、入房或输入。

Dispose 不发送 LeaveRoom；先尝试释放 Battle，再尝试 Room。单项异常原样传播，两项失败以 AggregateException 保留；标记终态后重复调用不重试。需要退房先显式执行协议操作，再关闭本地连接，世界清理仍归宿主。

底层连接可能执行配置好的物理重连，但该组合 session 不自动完成所有玩法恢复；调用方按 Room staged restore 和所属 Battle 同步链恢复，或重建整个 session。不得把物理连接恢复当成业务恢复完成。

## 验证范围

本轮运行既有 .NET Client 生命周期测试；本页在线片段未执行。未启动真实 Gateway、双成员开战、推送抢占实验、Unity 或业务帧/状态恢复链。
