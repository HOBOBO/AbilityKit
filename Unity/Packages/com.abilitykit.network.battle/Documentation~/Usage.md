# Battle 数据面使用指南

> 文档类型：使用指南
> 事实基线：2026-11-25，当前包源码。
> 核校状态：部分核校；覆盖 NetworkTransport 的来源、鉴权、输入与推送边界；项目同步算法未验证。

安装条件见[包入口](../README.md)。本包用 opcode、序列化和回调连接通用战斗信封与项目协议，不决定世界使用 FrameSync 还是 StateSync。

## SDK 来源与所有权

| 来源 | 谁创建与释放 SDK | 配置边界 |
| --- | --- | --- |
| `TransportFactory` | 引擎创建并拥有 SDK；连接每次打开/重连取得新 transport | 可安装 FrameCodec、ConfigureConnection、dispatchers 与流量观察 |
| `ConnectionFactory` | 引擎以 owned connection 路径建立并拥有 SDK | 底层 connection 的配置与 dispatcher 由工厂负责；不等于借用连接 |
| `SdkClient` | 默认 `Borrowed`；显式 `Owned` 时由引擎释放 | 保留 SDK 已有请求链、包络和派发；调用方配置，不重新装配 |
| `SdkClientFactory` | 引擎拥有 factory 返回的 SDK | 必须返回独立有效实例，不能与 SdkClient 同时设置 |

直接 Options 同时提供多种来源时，优先 SDK、再 connection、再 transport；构建器的来源方法会清除互斥来源。流量观察只可在引擎从 transport 组合时安装，注入其他来源又配置观察会被拒绝。不要复用一个底层 connection 再创建第二套 RequestClient；需要共享请求空间时借用完整 SDK。

## 本地借用完整示例

此例仅构造并释放适配器，验证借用 SDK 不随适配器释放；不连接服务端或证明认证通过。

```csharp
using AbilityKit.Network.Battle;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Sdk;

using var sdk = new NetworkSdkBuilder()
    .UseTransportFactory(() => new TcpTransport())
    .Build();
using (var battle = new NetworkTransport(new NetworkTransportOptions
{
    SdkClient = sdk,
    SdkClientOwnership = NetworkSdkClientOwnership.Borrowed
}))
{
}
sdk.Tick(0f);
```

借用只改变释放责任：`Connect`、`Disconnect`、`Tick` 仍操作该 SDK，不能由多个消费者重复驱动或互相关闭。构造时允许注入来源省略端点，不表示随后 `Connect()` 无需有效 host/port；需要只在已有连接上附加能力时，调用方须自行协调连接代际和鉴权入口。

## 鉴权、时钟与输入

`ConnectionEstablished` 是传输建连事件，早于 `RenewSession → PostAuthentication`。对应代际的握手及可选响应校验完成后才有 `ConnectionAuthenticated` / `IsAuthenticated`；失败走 `AuthenticationFailed`。旧代际完成不能放行新代际输入。成功仅表示配置的握手完成，不提供玩法恢复证明。

宿主必须安排 SDK 的墙钟 `Tick` 来推进心跳和重连等待；dispatcher 只安排工作线程，不自行产生时间步。引擎初始 `ConfigureConnection` 使用 Battle 重连预设；传 null 才采用 SDK 默认值。输入、世界 Tick 和回滚由所属同步链选择，不用网络墙钟推进模拟。

`SendInput` 是否走请求响应取决于是否配置 `DeserializeSubmitInputResponse`。未配置时等待鉴权后直接发送，失败通过 `SubmitInputFailed` 报告；配置后同样调用请求响应路径，但不向调用者返回 Task。`SendInputAsync` 要求两个输入 codec 配齐，返回最终业务回执。不能把 void/awaitable 或 raw/typed 调用方式与 FrameSync/StateSync 一一绑定。

回执要求重试且存在 `RewriteSubmitInputFrame` 时，最多重试一次，目标帧为 `ServerFrame + max(1, SubmitInputRetryFrameLead)`，默认 lead 为 2；不是默认对全部业务拒绝重试。准备通知只通知首次准备，不证明服务器接收、执行或输入终结。业务拒绝通过最终回执和 `SubmitInputCompleted` 报告；传输/超时失败通常返回 `TransportError` / `TransportTimeout` 并触发 `SubmitInputFailed`，调用方主动取消继续抛出。准备回调、事件订阅者或配置委托本身抛错仍可能传播，不能只处理失败状态。

显式 timeout 建立贯穿等待鉴权和请求尝试的取消预算；不传 timeout 的旧重载为每次请求提供 5 秒默认值，鉴权等待不因此取得独立的总截止时间。业务应按自身可取消性选择带预算的重载。CreateWorld/Join/Leave 的发送没有自动等待相同鉴权屏障，须由应用门控。

## 下行、恢复与所有权

`RawServerPushReceived` 先于 typed 解码触发，但订阅 raw 不会自动关闭 typed 路由。复用 raw apply 管线时清空所有 typed deserializer（配置构建器提供 `WithRawDownlinkOnly`），避免同一推送重复应用。raw 回调异常也可能阻止本次后续 typed 分发，不依赖订阅异常隔离。typed decoder 的匹配按 frame → snapshot → reliable events 短路，opcode 不应冲突。

推送载荷为借用数组片段，异步保存先复制；decoder 返回的对象是否深拷贝由项目 codec 定义。可靠事件 ack 响应不等于业务应用完成，全量 resync 请求被接受不等于新的快照已重建、导入或提交。`FramePacket` 自身是通用信封；项目 adapter 的 jitter buffer、world/session 隔离和恢复位置另由项目承担，不再推荐不存在的通用 Session adapter 路径。

Options 是传入的可变引用，不在连接运行中改身份、opcode、codec 或来源。Dispose 退订事件并使当前鉴权等待失败，仅在 owns SDK 时释放 SDK；不是服务端 LeaveWorld 或世界销毁操作。

`GenericNetworkClient` 是兼容的精简适配器，不提供完整战斗鉴权和公开 Tick；其当前 `OnServerPush` add 会重复挂接内部回调，多订阅者可能造成重复通知。不能把它当成与 NetworkTransport 等价的完整会话入口。本轮记录限制，不修改兼容实现。

## 源码与验证边界

[NetworkTransport](../Runtime/NetworkTransport.cs)、[Options](../Runtime/NetworkTransportOptions.cs)、[GenericNetworkClient](../Runtime/GenericNetworkClient.cs)。

本轮执行 .NET Battle 契约测试与本地借用示例；没有运行业务预测/表现、真实服务器、弱网或 Unity。Projection 类型只约定投影数据与 producer/consumer 端口，不提供跨项目统一同步算法。
