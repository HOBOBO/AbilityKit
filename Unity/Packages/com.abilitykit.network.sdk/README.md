# com.abilitykit.network.sdk

> AbilityKit 多人联网 SDK 的组装根与连接生命周期持有者。它不绑定房间、战斗或具体网络协议，而是把 `ITransport` / `IConnection` 组装为稳定的请求、响应、推送与重连入口。

- **版本**：0.1.0（Beta）
- **程序集**：`AbilityKit.Network.Sdk`（纯 C#，`noEngineReferences`）
- **依赖**：`com.abilitykit.network.runtime` 0.1.0
- **公共类型**：`NetworkSdkBuilder`、`NetworkSdkClient`

## 一、能力定位与非目标

```text
ITransport        TCP / InMemory / LiteNet / WebSocket 等原始字节通道
  ↓
IConnection       成帧、心跳、重连、派发（通常为 ConnectionManager）
  ↓
NetworkSdkClient  连接生命周期 + 单一 RequestClient + 统一事件
  ↓
能力包            network.room / network.battle / 游戏协议适配层
```

本包负责：

- 选择 transport factory 或 connection factory，并建立明确的所有权。
- 持有一条 `IConnection` 及一个 `RequestClient`。
- 转发生命周期、数据包、服务端推送、踢下线和重连事件。
- 为房间、战斗等上层能力提供共享连接和共享请求序号空间。

本包不负责：

- 房间、匹配、战斗协议及 DTO 编解码。
- 选择服务端监听协议，或保证任意客户端 transport 都已有配套服务端。
- 自动驱动 `Tick`，也不隐式切换到 Unity 主线程。
- 为传入或收到的 `ArraySegment<byte>` 提供长期存储所有权。

## 二、Builder 装配与所有权

`NetworkSdkBuilder` 有两条互斥装配路径，后设置的 factory 会清除先设置的 factory：

| 路径 | Builder 行为 | 创建时机 | 所有权 |
|---|---|---|---|
| `UseTransportFactory` | SDK 创建 `ConnectionManager` | connection 在 `Build()` 创建；transport 在 `Open()` 或每次重连时创建 | SDK 拥有 connection；connection 拥有每次 factory 返回的 transport |
| `UseOwnedConnectionFactory` | SDK 直接使用 factory 返回的 `IConnection` | 每次 `Build()` 调用 factory | SDK 拥有并最终释放 connection |
| `UseConnectionFactory` | `UseOwnedConnectionFactory` 的兼容别名 | 同上 | 同上，不是 borrowed connection |

关键契约：

1. Builder 可重复 `Build()`；每次都必须获得独立 connection，不能让多个 client 共享同一个有状态连接实例。
2. transport factory 是延迟执行的。仅 `Build()` 不会创建 socket transport；首次 `Open()` 及后续重连会分别创建新实例。
3. transport 路径的每次 `Build()` 都创建独立 `ConnectionOptions`；`ConfigureConnection` 可调用多次，配置委托按注册顺序累积。
4. 未配置任何 factory、factory 返回 `null` 或 client 构造失败都会立即失败；构造过程中已取得的 connection 会被释放。
5. `ConfigureConnection` 和 `UseDispatchers` 只作用于 SDK 创建的 `ConnectionManager`，不会改写外部 connection 的内部策略。

```csharp
var sdk = new NetworkSdkBuilder()
    .UseTransportFactory(() => new TcpTransport())
    .ConfigureConnection(options =>
    {
        options.EnableReconnect = true;
        options.ReconnectMaxAttempts = 5;
    })
    .UseDispatchers(callbackDispatcher, ioDispatcher)
    .Build();
```

若外部框架已经提供 `IConnection`，使用 owned connection 路径：

```csharp
var sdk = new NetworkSdkBuilder()
    .UseOwnedConnectionFactory(CreateGameFrameworkConnection)
    .Build();
```

## 三、Client 生命周期

`NetworkSdkClient` 持有且只持有一条 connection 和一个 `RequestClient`。房间能力通过 `CreateRoomClient()` 复用这条请求链，不应再创建第二套 request tracker。

```text
Build
  → Open / OpenIfDisconnected
  → Connected
  → Tick + SendPacket / SendRawRequestAsync
  → Close（允许以后再次 Open）
  → Dispose（终态）
```

- `Open(host, port)` 委托给底层 connection；host 不能为空，port 必须在 1..65535。
- `OpenIfDisconnected` 只在当前状态为 `Disconnected` 时调用 `Open` 并返回 `true`；其他状态返回 `false`。
- `Tick(deltaTime)` 必须由宿主持续推进。默认 `ConnectionManager` 的心跳检测、重连等待和重连尝试都依赖它。
- `Close()` 关闭当前连接，但不释放 client；调用者仍可再次 `Open`。
- `ResetReconnect()` 仅在底层实现 `IReconnectableConnection` 时有效，否则抛出 `NotSupportedException`。
- `Dispose()` 是终态且幂等；之后 `Open`、`Tick`、发送和重连操作抛 `ObjectDisposedException`，`Close()` 则保持 no-op。

释放顺序是契约的一部分：

1. 标记 client 已释放并退订 connection 事件。
2. 释放 `RequestClient`，让所有 pending request 失败完成。
3. 调用 connection `Close()`。
4. 即使 `Close()` 失败，也在 `finally` 中调用 connection `Dispose()`。
5. 清空 client 对外事件，避免连接对象继续持有业务订阅。

## 四、请求、推送与缓冲区

- `SendPacket` 发送原始信封，不创建请求跟踪项。
- `SendRawRequestAsync` 分配 seq，并按 seq 配对响应；超时、取消、断线、错误或 client 释放都会结束 pending task。
- `RequestClient` 在完成请求 task 前复制响应 payload，因此成功返回的请求响应可在回调结束后继续持有。
- `PacketReceived` 与 `ServerPushReceived` 是同步透传边界。TCP 和 WebSocket 对较大接收块会使用池化数组，并在事件回调返回后归还；业务若需异步处理或长期保存，必须在回调内复制 payload。
- `ITransport.BytesReceived` 同样不承诺底层数组的长期所有权。自定义 transport 应明确自己的回调线程和缓冲区生命周期。

## 五、线程与派发边界

未调用 `UseDispatchers` 时，callback dispatcher 和 IO dispatcher 都是 `InlineDispatcher`。这意味着事件可能直接发生在 transport 的 IO 线程：

- `TcpTransport` 和 `WebSocketTransport` 的接收循环运行在后台 task。
- `LiteNetTransport` 使用 `UnsyncedEvents = true`，由 LiteNetLib 内部线程触发事件。
- `InMemoryTransport.Send` 在调用线程同步触发对端事件。

Unity 业务若访问场景对象或 UI，应注入主线程 callback dispatcher。IO dispatcher 可独立配置，但不能假设所有回调天然位于 PlayerLoop。

## 六、Transport 选型与证据边界

| Transport | 包 / 实现 | 当前验证 | 当前采用与限制 |
|---|---|---|---|
| TCP | `network.runtime/TcpTransport` | SDK 有契约测试；Orleans 有 TCP smoke 链路 | Room、Battle、Moba、Shooter 的默认或实际运行路径；未发现客户端 transport 级独立真实 socket 回环测试 |
| InMemory | `network.transport.inmemory` | linked-pair 同步回环测试；另有一条较弱的 SDK 集成测试 | 面向进程内测试；不模拟 socket、延迟、丢包、乱序、线程切换或真实断网 |
| LiteNet | `network.transport.litenet` | 本机 LiteNetLib UDP echo round-trip | 客户端候选实现；未发现 AbilityKit LiteNet/UDP 服务端网关和生产消费者，未做真实弱网或性能对比 |
| WebSocket | `network.transport.websocket` | 本机 `HttpListener` echo round-trip | 客户端候选实现；Unity WebGL 不支持。Orleans 服务端类存在，但 canonical Gateway 尚未注册或托管，未形成默认端到端链路 |
| GameFramework bridge | `gameframework.network` 的 `IConnection` 适配 | Shooter 接入路径存在 | 走 connection factory，不是 `ITransport` 实现 |

不能从“实现存在”或“本机 echo 测试通过”推导生产成熟。当前 TCP 有业务运行时采用；三个独立 transport 包有实现和局部测试，但没有同等级生产采用证据。

## 七、失败矩阵

| 场景 | 可见行为 | 调用方责任 |
|---|---|---|
| 未设置 factory / factory 返回 `null` | `Build()` 抛异常 | 启动期完成装配自检 |
| 非法 host 或 port | `Open` / `OpenIfDisconnected` 抛参数异常 | 在配置边界校验 endpoint |
| 尚未连接时发送 | 底层 connection/transport 抛错 | 以 `State` 或 Connected 事件门控发送 |
| 请求超时或取消 | request task 失败或取消 | 区分可重试业务与不可重试业务 |
| 断线、transport error、Dispose | pending requests 统一失败 | 不要让业务 task 无限等待 |
| 重连耗尽 | `ReconnectExhausted` 事件 | 切换到登录恢复、会话重建或退出流程 |
| 回调访问 Unity 对象 | 默认可能发生在线程池或 IO 线程 | 注入主线程 dispatcher |
| 回调后继续持有推送 payload | 池化缓冲区可能已复用 | 在同步回调内复制所需数据 |
| 客户端 transport 无配套服务端 | 连接失败 | 同时交付并验收对应服务端监听端 |

## 八、最小接入

```csharp
using var sdk = new NetworkSdkBuilder()
    .UseTransportFactory(() => new TcpTransport())
    .ConfigureConnection(options => options.EnableReconnect = true)
    .Build();

sdk.ServerPushReceived += (opCode, payload) =>
{
    // 需要跨回调保存时，在这里复制 payload。
};

sdk.Open(host, port);

// 宿主主循环持续调用：
sdk.Tick(deltaTime);

var response = await sdk.SendRawRequestAsync(MyOpCodes.Login, requestPayload);
var room = sdk.CreateRoomClient();
```

替换 transport 只保证客户端上层装配不变，不保证目标平台和服务端已经具备对应协议。选择 LiteNet 或 WebSocket 前，必须同时验证服务端监听、部署网络、TLS/代理、断线恢复和目标平台运行时。

## 九、采用证据与未覆盖范围

证据分级采用 E0-E5：E0 源码、E1 Sample/Editor、E2 业务运行时、E3 自动测试、E4 Smoke/Acceptance、E5 CI/发布门禁。

- SDK Builder/Client：Room、Battle、Moba、Shooter 有 E2 运行时消费者；`AbilityKit.Network.Sdk.Tests` 提供 E3 生命周期和请求契约测试。
- TCP：是当前生产默认及 Orleans smoke 主路径，可视为 E2/E4 链路证据；这不替代 transport 自身的 socket 边界专项测试。
- InMemory、LiteNet、WebSocket：实现为 E0，独立回环测试为 E3；未发现 AbilityKit 生产消费者，不标记 E2。
- 尚未形成统一 E5 transport 矩阵，也没有真实弱网、跨平台、WebGL 或多协议服务端门禁。

## 十、源码阅读路径

1. `Runtime/NetworkSdkBuilder.cs`：factory 互斥、延迟实例化和构造失败释放。
2. `Runtime/NetworkSdkClient.cs`：事件转发、单一 RequestClient 和 Dispose 顺序。
3. `network.runtime/Runtime/Network/Runtime/ConnectionManager.cs`：连接、心跳、重连和 dispatcher。
4. `network.runtime/Runtime/Network/Runtime/RequestResponse/RequestClient.cs`：seq、pending task、响应复制和失败收敛。
5. `network.runtime/Runtime/Network/Abstractions/ITransport.cs`：自定义传输的最小契约。
6. `src/AbilityKit.Network.Sdk.Tests/NetworkSdkClientTests.cs`：Builder/Client 的可执行契约。

## 十一、相关

- 房间会话：`com.abilitykit.network.room`
- 战斗数据面：`com.abilitykit.network.battle`
- 传输与连接原语：`com.abilitykit.network.runtime`
- 可选传输：`com.abilitykit.network.transport.inmemory`、`com.abilitykit.network.transport.litenet`、`com.abilitykit.network.transport.websocket`
- 多人接入清单：`Docs/design/07-NetworkSynchronization/07-MultiplayerSdkIntegrationGuide.md`
