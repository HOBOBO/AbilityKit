# com.abilitykit.network.host

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

启用 `IdleTimeout` 后，应从 Unity `Update` 或无头服务循环中调用 `host.Tick()`。超时基于
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
超过期限后取消剩余请求并断开。`GracefulStops`、`DrainTimeouts` 和 `RequestsCancelled` 可用于
区分正常退出与强制收尾。

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

InProcess 只替换传输，客户端仍经过 `ConnectionManager`、帧协议和服务端 Pipeline。

## 边界

本包不负责登录、房间、战斗语义、玩家身份认证和 World Tick。Gateway 状态码包络也不是
通用 Session 的内建要求，应由具体协议 handler 处理。权威 World Host 的适配位于
`com.abilitykit.host.network`。
