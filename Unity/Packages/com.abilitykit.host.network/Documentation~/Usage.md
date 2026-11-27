# com.abilitykit.host.network

> 文档类型：使用指南
> 事实基线：2026-11-25，当前包源码。
> 核校状态：部分核校；覆盖下述装配、所有权和失败边界，平台与真实部署验证另述。

安装入口见[包 README](../README.md)。除标为完整本地示例的代码外，其余均是装配片段，省略的 handler、codec、工厂与 dispatcher 必须由应用提供。

将 `com.abilitykit.network.host` 的服务端 Session 接入 `com.abilitykit.host` 权威世界运行时。
该包不创建固定协议，也不要求 TCP。

`Runtime/Connections` 管理 Host 身份映射与连接生命周期；`Runtime/Transports` 提供 TCP 和进程内装配；`Runtime/Abstractions` 定义 codec、请求处理及身份映射端口。底层监听、帧编解码、会话队列由 `network.host` / `network.runtime` 负责，认证和房间策略由项目负责。

纯 C#，Unity 安装需满足依赖（`host` 要求 2022.3）；.NET 10 直接编译同一份包源码。直接依赖见 [package.json](../package.json)。

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
或 InProcess。上层也可装配兼容的 WebSocket、KCP、Relay 等 listener；本轮不为这些部署组合作验收保证。

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
配置 `NetworkHostOptions.IdleTimeout` 或 `EstablishmentTimeout` 后，从宿主循环调用 `connections.Tick()`；运行指标可从
`connections.GetDiagnostics()` 获取。

进程退出或热切换时可调用：

```csharp
await connections.StopAsync(TimeSpan.FromSeconds(5), cancellationToken);
```

管理器会先排空已进入队列的 Host handler，再从 `HostRuntime` 移除连接。停止后的
`GetDiagnostics()` 仍保留最后一次终态快照；活动会话明细可在运行期间通过
`GetSessionSnapshots()` 获取。`InProcessHostNetwork` 提供相同的 `StopAsync` 和快照 API。

从宿主控制流串行调用生命周期入口。开始排空后，等待原 `StopAsync` 完成，再重启、解绑或释放；不要用第二次停止调用代替等待。启动检查通过前不会调用监听器工厂；工厂返回的监听器由管理器接管。运行中的桥不能切换到另一个 `HostRuntime`。

若配置由外部 address/port 提供，也可以使用 `TcpHostNetwork.Create(...)` 后调用
`StartListen(address, port)`。

## 资源与失败边界

Start 前需先关联 HostRuntime；启动预检通过才调用 listener factory，返回对象交给管理器持有。停止会排空请求、释放 NetworkHost 并移除 runtime 连接索引，不销毁世界、不代发业务 Leave。世界与业务资源仍按宿主契约单独收尾。

管理器正在停止时，再次 `StopAsync` 可能直接返回；务必等待第一次调用保存的任务。这与底层 NetworkHost 重复停止返回同一次任务的契约不同。生命周期入口需从宿主控制流串行调用，不能在待排空 handler 内等待自身停止。

本轮 `AbilityKit.Host.Network.Tests` 11/11 通过（E3），核验本地身份绑定与生命周期；本页装配代码是依赖业务 codec/handler/worldFactory 的片段，未作为独立完整示例运行。未验证 Unity、正式网关部署或所有世界实现。

源码入口：包内 `Runtime/Connections/`（运行时绑定、启动/停止和身份索引）、`Runtime/Transports/`（TCP/InProcess 装配）、`Runtime/Abstractions/`（codec、handler 和身份解析契约）。

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
