# Room 连接与房间流程使用指南

> 文档类型：使用指南
> 事实基线：2026-11-25，当前包源码。
> 核校状态：部分核校；覆盖连接所有权、分阶段流程、订阅与恢复信号；真实网关及 Unity 交互未重跑。

安装条件见[包入口](../README.md)。本包编解码通用 Room Gateway 外壳，玩法输入、快照导入、预测与重演由项目提供。

## 选择接入入口

| 已有条件 | 入口及持有关系 |
| --- | --- |
| 已有 `NetworkSdkClient` | `sdk.CreateRoomClient()` 复用 SDK 的单一请求链；room client 持有桥接与协议 Agent，释放它不关闭 SDK |
| 只需连接，登录或房间操作由交互流程决定 | `RoomGatewayConnectionSession.ConnectAsync` 持有 SDK、room client 与 Recovery；默认 TCP，连接超时或取消时清理已取得的资源，不自动登录/入房 |
| 需要线性的访客登录与房间启动 | `MultiplayerRoomSession.CreateAsync` 在连接会话上登录并运行房间流程；返回的 session 持有连接；关闭本地 session 不等于服务端退房 |
| 已有认证和分阶段 UI，或需要注入测试客户端 | 直接使用 `RoomGatewaySessionFlow`；按能力调用具体阶段，或以 `RunRoomFlowAsync` 组合，不额外取得传入客户端的所有权 |

## 本地构造完整示例

下例只验证 room 能力可装配及释放后 SDK 仍可使用，不连接网络、不登录或开战。

```csharp
using AbilityKit.Network.Room;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Sdk;

using var sdk = new NetworkSdkBuilder()
    .UseTransportFactory(() => new TcpTransport())
    .Build();
using (var room = sdk.CreateRoomClient())
{
    var flow = new RoomGatewaySessionFlow(room);
}
sdk.Tick(0f);
```

## 连接、房间与订阅分别完成

`ConnectAsync` 校验 host、1..65535 端口和正 timeout，并轮询连接状态；默认 timeout 为 30 秒。它不泵送外部 dispatcher：若 Connected 回调被排进宿主队列，调用方必须继续排空该队列，不能在同一线程阻塞等待连接。连接建立后仍需按真实墙钟间隔调用 `Tick`，网络超时不使用快进模拟时间。

`CreateAsync` 的流程为连接 → 访客登录 → 加入指定房间或创建再加入 → `afterJoinAndBeforeReady` → ready → `afterReadyAndBeforeBattleStart` → 可选等待开战 → 可选订阅。加载、选角或直接开战取决于服务端配置和调用方钩子；方法不会替所有房间自动执行相同的加载步骤。

`joinFallbackToCreate` 默认关闭。开启时，加入失败结果或非取消异常可回退创建；`OperationCanceledException` 仍终止。`waitForBattleStart: false` 只跳过等待，不能从返回值推导已经 InBattle；此时 `Result.Started` 仍可能为 false。`Result` 是本次流程结果，不是实时房间视图，后续状态读取 snapshot feed 或显式请求。

`RoomGatewaySessionFlow` 以 `IRoomGatewaySessionClientBase` 为基础，按接口探测选角、加载、直接开战、订阅等可选能力；缺失时会失败，不根据游戏名字补行为。等待、轮询及逐请求 timeout 不是整条流程的单一总预算，需要总预算时传入由调用方管理的取消令牌。阶段成功不意味着之前或之后的服务器副作用可回滚；失败和 Dispose 不自动发送 LeaveRoom 或补偿。

组合双连接时，Room 控制面完成登录/入房后，应由 Battle 数据面独占对应推送订阅，房间侧用 `subscribeStateSync: false`。此标志不决定主同步模式；帧同步会话也可附带状态推送。订阅成功只说明订阅请求完成，不说明世界已重建或首个基线已导入。

## 恢复与资源边界

`Recovery` 只跟踪连接状态及 Generation。连接重新建立后进入 `RestoreRequired`；业务完成认证、房间恢复与所属同步链恢复后，再用对应 generation 调用 `CompleteRestore`。该调用仅切换连接恢复状态，不替业务导入快照、追帧或验证输入历史。

`RoomGatewayRestoreFirstConnectionPolicy` 在允许 fallback 时捕获 restore 的所有异常，包括取消异常，再调用 fallback delegate；需要取消贯穿时，调用方应在 delegate 内检查自己的令牌，不能把这个辅助策略等同于上述 join fallback 的取消语义。返回的 `RestoreFailure` 保留原失败，fallback 失败仍向外传播。

`CreateRoomClient` 借用 SDK；直接以 `IConnection` 构造 wire client 的兼容入口则建立自己的 RequestClient，不能与 SDK 请求链并用造成序号冲突。响应是裸 DTO 还是 Gateway 状态包络取决于实际服务端与请求链的 envelope codec，需在组合根匹配；不能重复剥包络或把未解包的数据直接当 DTO。

推送负载按回调借用处理，跨回调持有先复制。room client 的强类型状态推送仍只是协议数据，不接管帧时钟。快照 ID、可靠事件游标、FrameSync 确认和 StateSync 输入终结保持各自契约。

连接 session 的 Dispose 按 Recovery → room client → SDK 的 finally 链尝试释放；`MultiplayerRoomSession.Dispose` 关闭其持有的连接，不调用业务退房。取消、错误和释放不能撤回服务端已经接受的操作；不得把本地清理写成分布式事务。会话业务操作及 UI 状态更新应串行调度。

## 源码与验证边界

- [连接 session](../Runtime/RoomGatewayConnectionSession.cs)、[自动流程](../Runtime/MultiplayerRoomSession.cs)、[分阶段 Flow](../Runtime/RoomGatewaySessionFlow.cs)。
- [SDK 桥接](../Runtime/NetworkSdkRoomExtensions.cs)、[wire client](../Runtime/RoomGatewayWireSessionClient.cs)、[连接恢复](../Runtime/Recovery/RoomGatewayConnectionRecovery.cs)、[restore-first 策略](../Runtime/RoomGatewayRestoreFirstConnectionPolicy.cs)。

本轮执行既有 .NET Room 测试和上述本地构造示例；真实账号登录、服务端启动、两名成员开战、恢复完整链和 Unity 场景未运行。旧 README 中的 2026-08-10 Console 推送数量是历史示例记录，不作为本轮验收。
