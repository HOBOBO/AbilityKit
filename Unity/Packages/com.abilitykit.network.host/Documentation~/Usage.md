# com.abilitykit.network.host

> 文档类型：使用指南
> 事实基线：2026-11-25，当前包源码。
> 核校状态：部分核校；覆盖下述装配、所有权和失败边界，平台与真实部署验证另述。

安装入口见[包 README](../README.md)。除标为完整本地示例的代码外，其余均是装配片段，省略的 handler、codec、工厂与 dispatcher 必须由应用提供。

AbilityKit 的服务端网络运行时。它负责接受双向 Channel、复用统一帧协议创建服务端 Session，
并通过 Pipeline 和 op-code Router 将消息交给上层。核心不依赖 Unity，也不假设 TCP。

## 分层

```text
IChannelListener                  连接接入（TCP/WebSocket/KCP/Relay/InProcess）
  -> IServerChannel              一个已接受的双向字节通道
    -> ServerNetworkSession      FrameCodec + NetworkPipeline
      -> NetworkHost             Session 生命周期、连接上限、请求路由
        -> application handler   Room/Battle/自定义协议
```

官方随包提供：

- `TcpChannelListener`：默认 TCP 监听实现。
- `InProcessChannelListener`：本机 Host 客户端，不经过 Socket。
- `ServerRequestRouter`：显式 op-code 路由，不使用反射和全局注册。
- `AsyncServerRequestRouter`：支持取消的异步路由；同一 Session 串行执行，不同 Session 可并发。

## 传输无关用法

```csharp
IChannelListener listener = CreateYourListener();
var router = new ServerRequestRouter()
    .Register(100, (session, header, payload) =>
        session.SendResponse(header.OpCode, header.Seq, payload));

var host = new NetworkHost(listener, new NetworkHostOptions
{
    MaxConnections = 32,
    RequestHandler = router,
    ConfigurePipeline = pipeline => pipeline.Use(yourMiddleware)
});
host.Start();
```

自定义传输只需实现 `IChannelListener` 和 `IServerChannel`。帧协议、中间件和上层 handler
无需变化。

## 异步请求与背压

```csharp
var router = new AsyncServerRequestRouter()
    .Register(100, async (session, header, payload, cancellationToken) =>
    {
        await HandleLoginAsync(session, payload, cancellationToken);
        session.SendResponse(header.OpCode, header.Seq, default);
    });

var host = new NetworkHost(listener, new NetworkHostOptions
{
    AsyncRequestHandler = router,
    MaxPendingRequestsPerSession = 256,
    IdleTimeout = TimeSpan.FromSeconds(60),
    EstablishmentTimeout = TimeSpan.FromSeconds(10),
    AdmissionPolicy = channelAdmissionPolicy
});
host.Start();
```

每个 Session 的请求按到达顺序串行执行，避免同一玩家命令并发修改状态；不同 Session
拥有独立队列。断开连接或停止 Host 会取消正在执行及尚未执行的请求。待处理请求达到
`MaxPendingRequestsPerSession` 时，Host 会关闭该 Session，避免静默丢包造成状态分歧。

异步 handler 抛出的异常通过 `SessionError` 上报并计入诊断，但默认不会关闭 Session，
队列会继续处理后续请求。需要按协议错误断开时，handler 应显式调用 `session.Close()`。

启用 `IdleTimeout` 或 `EstablishmentTimeout` 后，应从 Unity `Update` 或无头服务循环中调用 `host.Tick()`。超时基于
注入的 `IMonotonicClock`，接收和发送均会刷新活动时间。

`EstablishmentTimeout` 用于限制 Session 完成应用层建立流程的时间。建立流程可以是认证、
加密协商、房间票据或 Relay admission，不绑定具体协议；成功后显式调用：

```csharp
session.Context.MarkEstablished();
```

`IChannelAdmissionPolicy` 在 Session 创建前读取抽象 `IServerChannel` 与当前连接数，返回
`ChannelAdmissionResult`。拒绝原因通过 `ChannelRejected` 暴露，适合维护状态、来源限制或
自定义连接配额；它不依赖 IP endpoint，也不假设 Channel 来自 Socket。

## 优雅停机

`Stop()` 立即取消请求并断开 Session。需要发布、房间迁移或无头进程退出时使用：

```csharp
await host.StopAsync(TimeSpan.FromSeconds(5), cancellationToken);
```

`StopAsync` 先停止 Channel 接入和新请求，再等待已经进入各 Session 串行队列的请求完成。
超过期限或取消排空等待时，仍会取消剩余请求并关闭旧会话。重复调用 `StopAsync` 会等待同一次
停机并得到相同结果，不会替换首次调用的期限或取消令牌。停机期间不得调用 `Start()`、`Stop()`
或 `Dispose()`；等停机任务结束后再操作。不要在参与排空的请求处理器内等待本次停机。

`GracefulStops`、`DrainTimeouts` 和 `RequestsCancelled` 可用于区分正常退出与强制收尾。

`host.GetDiagnostics()` 提供连接、超时、监听/Session 错误和请求队列计数。单个 Session
还提供收发字节/包计数，以及用于认证、房间绑定等应用元数据的 `session.Context`。
`host.GetSessionSnapshots()` 返回不暴露可变 Session 的只读运维快照，包括 endpoint、建立状态、
活动时间、pending 请求和流量计数。

## 官方 TCP

```csharp
var listener = new TcpChannelListener(new TcpChannelListenerOptions
{
    Address = IPAddress.Any,
    Port = 9000,
    Backlog = 128,
    ReceiveBufferSize = 64 * 1024
});

var host = new NetworkHost(listener, options);
host.Start();
```

## 本机 Host 客户端

```csharp
var listener = new InProcessChannelListener();
var host = new NetworkHost(listener, options);
host.Start();

var client = new ConnectionManager(() => listener.CreateClientTransport());
client.Open("inprocess", 1);
```

InProcess 只替换传输，客户端仍经过 `ConnectionManager`、帧协议和服务端 Pipeline。listener 必须已 Start；`CreateClientTransport` 创建时会先通知服务端接入，再由客户端 Connect。它复制交付字节，与简单 linked pair 的 InMemoryTransport 不同。

## 完整本地请求往返示例

不创建 socket，使用默认 raw response body；不是 Gateway 状态码包络或业务认证演示。

```csharp
using System;
using AbilityKit.Network.Host;
using AbilityKit.Network.Host.InProcess;
using AbilityKit.Network.Runtime;

var listener = new InProcessChannelListener();
var router = new ServerRequestRouter().Register(42,
    (session, header, payload) =>
        session.SendResponse(header.OpCode, header.Seq, payload));
using var host = new NetworkHost(listener,
    new NetworkHostOptions { RequestHandler = router });
host.Start();
using var connection = new ConnectionManager(() => listener.CreateClientTransport());
using var requests = new RequestClient(connection);
connection.Open("inprocess", 1);
var response = await requests.SendRequestAsync(42,
    new ArraySegment<byte>(new byte[] { 7 }), TimeSpan.FromSeconds(1));
if (response.Count != 1 || response.Array[response.Offset] != 7)
    throw new InvalidOperationException("请求往返失败");
await host.StopAsync(TimeSpan.FromSeconds(1));
```

## 所有权与核校范围

NetworkHost 持有 listener 及接入的 session/channel，不拥有业务 IWorld 或它的时钟。默认最大连接 128、每 session 待处理队列 256、帧上限 4 MiB；两个超时默认零（禁用），实际配置以 Options 为准。同 session 的异步请求串行不等于不同 session 对共享世界的修改串行，宿主必须安排执行线；取消为协作式，handler 需响应令牌。MarkEstablished 只记录应用确认，不执行认证。

本轮 `AbilityKit.Network.Host.Tests` 21/21 通过（E3），上述示例独立编译执行；没有新增部署、Unity 或业务世界回归。源码入口：[NetworkHost](../Runtime/NetworkHost.cs)、[Options](../Runtime/NetworkHostOptions.cs)、[InProcessChannelListener](../Runtime/InProcess/InProcessChannelListener.cs)。

## 边界

本包不负责登录、房间、战斗语义、玩家身份认证和 World Tick。Gateway 状态码包络也不是
通用 Session 的内建要求，应由具体协议 handler 处理。权威 World Host 的适配位于
`com.abilitykit.host.network`。
