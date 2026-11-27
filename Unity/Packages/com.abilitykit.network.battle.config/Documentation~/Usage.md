# Battle 配置使用指南

> 文档类型：使用指南
> 事实基线：2026-11-25，当前包源码。
> 核校状态：已核校下述配置、可变性及协议预设边界；线上会话与目标平台未验证。

安装条件见[包入口](../README.md)。`NetworkBattleConfig` 是 Room Gateway 预设的配置构建器，不是任意协议的自动适配器。自定义协议可直接构造 NetworkTransportOptions。

## 本地配置完整示例

此例只构造配置，不建连。示例将十进制字符串身份映射为 wire 数值 ID；真实项目应替换为自身稳定、可验证的映射。

```csharp
using System;
using AbilityKit.Network.Battle.Config;

var config = new NetworkBattleConfig()
    .WithGateway("127.0.0.1", 4000)
    .WithTcpTransport()
    .WithSession("local-token", "local-battle", "local-room")
    .UseRoomGatewayProtocol("local-battle", "local-room")
    .UseRoomGatewayStateSyncInput("local-battle",
        player => uint.Parse(player.Value), world => ulong.Parse(world.Value));
var options = config.Build();
if (options.SerializeSubmitInput == null || options.DeserializeSubmitInputResponse == null)
    throw new InvalidOperationException("输入 codec 未装配");
```

预期得到带标准输入 codec 的 Options。输入 payload 本身仍归项目；快照或权威帧的 typed decoder 必须另外提供，或明确采用 raw downlink，不能把 Build 成功当成完整可运行会话。

## 配置顺序与预设

1. 选择端点与一种连接来源：TCP/transport factory、owned connection factory、借用/拥有 SDK 或 owned SDK factory。后设置的来源清掉先前互斥来源；`WithSdkClient` 默认借用，`WithInjectedConnection` 返回的连接由新 SDK 拥有。
2. 设置 session token，再应用 `UseRoomGatewayProtocol(battleId, roomId)`。该方法登记目录中的 opcode、Renew/Subscribe、可靠事件 ack、全量 resync 和输入序号包装。不要在文档或业务里复制操作码数字。
3. 安装输入 codec，以及项目的帧/快照 decoder 或 raw 下行策略；按协议选固定基线订阅、作用域输入和可靠事件游标。
4. Build 后交给唯一所有者构造 NetworkTransport，安排墙钟 Tick、鉴权/失败事件、世界恢复与释放。

`WithSession` 当前只写 token 和 RenewSession 配置，其 battleId/roomId/worldId 参数不自动成为所有后续路由的事实源；实际路由取对应协议预设、输入 codec 和 scope 方法传入的身份。保持这些值一致，不依赖无效参数弥补漏配。

`UseRoomGatewayStateSyncInput` 安装通用输入外壳与回执转换，不改变房间最终同步模式；默认 retry policy 为不重试，由项目显式提供条件才开启引擎重试。`UseScopedStateSyncInput` 的 generation 是生产者作用域，不能和 Snapshot ID、可靠事件 epoch 或输入终结位置互换。可靠事件游标从委托读取，不是构建时深拷贝。

`WithRawDownlinkOnly` 清掉当前全部 typed push decoder；应在预设及 typed 配置之后调用。后续再配置 decoder 或重复应用预设会重新开启对应 typed 路由。`UseFixedBaselineStateSyncSubscription` 等依赖既有预设的配置也按源码要求的顺序应用。

## Build 与所有权限制

Build 只校验存在来源、token、协议预设和输入 serializer，不完整校验端点、所有 codec、真实身份、服务端兼容或鉴权成功。它返回内部同一份 Options，重复 Build 不克隆；后续 builder 调用会影响已返回配置。输入序号及若干委托捕获当前 builder，不能把一个 builder 的输出视为多个独立 session。每个会话使用独立 builder，交付后停止修改。

`WithConnectionConfiguration` 替换整段连接配置委托，不像 SDK 的 ConfigureConnection 那样累加；`UseSdkConnectionDefaults` 清空 Battle 预设委托。已有 SDK 的包络、dispatcher、连接和流量观察在原组合根配置，不由这个 builder 覆写。

`WithResponseEnvelopeCodec` 必须匹配服务端实际响应。可靠事件 ack/resync 请求 codec 只是 wire 外壳；成功响应不提供事件应用或快照导入完成的证明。`WithSnapshotDeserializer` 不替调用者验证基线、恢复世界或提交接收位置。

## 源码与验证边界

源码：[NetworkBattleConfig](../Runtime/NetworkBattleConfig.cs)。本轮执行既有 .NET 配置测试和上述完整例子。未连接真实 Room Gateway、验证跨端输入编码、Unity 或发布制品。
