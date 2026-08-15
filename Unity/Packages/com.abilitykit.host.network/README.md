# com.abilitykit.host.network

将 `com.abilitykit.network.host` 的服务端 Session 接入 `com.abilitykit.host` 权威世界运行时。
该包不创建固定协议，也不要求 TCP。

## 扩展点

- `IHostMessageCodec`：把 `ServerMessage` 编码为通用网络包。
- `IHostNetworkRequestHandler`：把入站网络包转换为 Host/World 命令。
- `IAsyncHostNetworkRequestHandler`：支持取消的异步 Host/World 命令入口。
- `IHostClientIdResolver`：把网络 Session 映射为 Host ClientId；认证系统可替换默认实现。
- `IChannelListener`：决定 TCP、WebSocket、KCP、Relay 或 InProcess 接入方式。

## 默认 TCP 装配

```csharp
var connections = TcpHostNetwork.CreateConfigured(
    new TcpChannelListenerOptions
    {
        Address = IPAddress.Any,
        Port = 9000
    },
    hostMessageCodec,
    hostRequestHandler);

var (host, _) = WorldHostBuilder.Create()
    .SetWorldFactory(worldFactory)
    .SetConnectionManager(connections)
    .BuildWithOptions();

connections.Start();
```

## 内置本机组合

单机模式、编辑器联调和集成测试可以使用低成本组合入口，但仍完整经过客户端
`ConnectionManager`、帧协议、服务端 Session 和 Pipeline：

```csharp
using var network = new InProcessHostNetwork(hostMessageCodec, hostRequestHandler);

var (host, _) = WorldHostBuilder.Create()
    .SetWorldFactory(worldFactory)
    .SetConnectionManager(network.Connections)
    .BuildWithOptions();

network.Start();
using var client = network.CreateClientConnection();
client.Open("inprocess", 1);
```

这只是官方提供的 `IChannelListener` 组合，不改变抽象层级，也不会让 Host 核心内定 TCP
或 InProcess。生产环境仍可替换为 WebSocket、KCP、Relay 等 listener。

## 认证后绑定身份

连接建立时默认使用 Channel Id 作为临时 ClientId。登录验证成功后，可在请求 handler 中
原子替换为账号或玩家身份：

```csharp
if (!connections.TryBindClient(session.Id, new ServerClientId(accountId)))
{
    session.Close(); // 身份已被其他在线 Session 占用
}
```

重绑定会同步更新 `HostRuntime` 的连接索引。重复身份不会覆盖已有连接，并可通过
`OnClientRebound` 观察身份变化。成功的 `TryBindClient` 同时调用
`session.Context.MarkEstablished()`，因此可以直接配合 `NetworkHostOptions.EstablishmentTimeout`
限制未认证连接占用时间。

异步入口使用 `HostNetworkConnectionManager.CreateAsync(...)` 或
`InProcessHostNetwork.CreateAsync(...)`。同一 Session 的请求串行执行，并在断连时取消。
配置 `NetworkHostOptions.IdleTimeout` 后，从宿主循环调用 `connections.Tick()`；运行指标可从
`connections.GetDiagnostics()` 获取。

进程退出或热切换时可调用：

```csharp
await connections.StopAsync(TimeSpan.FromSeconds(5), cancellationToken);
```

管理器会先排空已进入队列的 Host handler，再从 `HostRuntime` 移除连接。停止后的
`GetDiagnostics()` 仍保留最后一次终态快照；活动会话明细可在运行期间通过
`GetSessionSnapshots()` 获取。`InProcessHostNetwork` 提供相同的 `StopAsync` 和快照 API。

若配置由外部 address/port 提供，也可以使用 `TcpHostNetwork.Create(...)` 后调用
`StartListen(address, port)`。

## 自定义传输

```csharp
var connections = new HostNetworkConnectionManager(
    () => yourChannelListener,
    hostMessageCodec,
    hostRequestHandler,
    authenticatedClientIdResolver);

builder.SetConnectionManager(connections);
connections.Start();
```

`IConnectionManager` 只负责连接与 Host 的绑定；通用生命周期由
`IConnectionManagerLifecycle` 表达，IP address/port 能力单独位于
`IEndpointConnectionManager`，因此核心契约不会内定 Socket 端点。
