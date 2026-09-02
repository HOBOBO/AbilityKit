# 协议目录与网络流量可观测性

状态：基础能力已实现，2026-08-21

## 1. 范围与决策

本子系统用于统一 AbilityKit 各项目的协议治理和数据包观测能力，同时不改变现有线上字节格式。

- 业务载荷字段由正式 Wire Schema 和生成 DTO 定义。Shooter 与 MOBA 的 catalog MemoryPack 载荷闭包已完成迁移；Room 按独立计划继续迁移。
- `*.protocol.yaml` 是传输标识和运行策略的治理目录。可选的 `*.wire.yaml` 是项目选择生成的载荷字段级 IDL；两类文档使用独立的模式并承担不同职责。
- Room 与 Battle 默认使用独立的物理连接，因为二者的故障域、重连策略、生命周期和流量特征不同。
- 可复用的客户端单元是 `NetworkSdkClient`，它持有一个 `IConnection` 和一个请求客户端。原始 `IConnection` 实例不得在职责无关的角色间池化或共享。
- 数据包观测基于中间件，与 TCP、WebSocket、KCP 或未来新增的传输协议无关。

目标源码流如下：

```text
*.wire.yaml / 外部 IDL              *.protocol.yaml
              |                            |
              +------- 编译器/编辑器 -------+
                          |
                 项目 MemoryPack 导出
                protocol-manifest.json
               BuiltInProtocolCatalogs.g.cs
                          |
           运行时查找 / 解码器注册 / 可视化
```

## 2. 仓库职责归属

| 路径 | 归属 | 规则 |
|---|---|---|
| `Protocols/Catalogs/*.protocol.yaml` | 源文件 | 由项目团队评审和编辑 |
| `Protocols/protocol-catalog.schema.json` | 源文件 | IDE 模式和规范字段结构 |
| `Protocols/WireSchemas/*.wire.yaml` | 可选源文件 | 由项目持有、用于生成载荷的字段级 IDL |
| `Protocols/wire-schema.schema.json` | 源文件 | 线上字段、所有权和兼容性结构 |
| `Protocols/Generated/protocol-manifest.json` | 生成文件 | 禁止手工编辑 |
| `Runtime/Generated/BuiltInProtocolCatalogs.g.cs` | 生成文件 | 禁止手工编辑 |
| `Protocol/Catalog/*` | 共享运行时 | 稳定的跨项目契约 |
| `tools/AbilityKit.Protocol.CatalogCompiler` | 构建工具 | 严格且确定性的编译器 |

目录文件可以按项目和领域拆分。应使用 `<organization>.<project>.<domain>` 形式的全局唯一 ID，例如 `abilitykit.moba.battle`。共享领域可以使用 `abilitykit.room`，并配置共享的 `projectId`。

## 3. 协议目录契约

最小示例：

```yaml
schemaVersion: 1
catalogId: studio.game.battle
projectId: studio.game
domain: battle
revision: 1
defaultCodec: protobuf
messages:
  - id: cast-skill.request
    opCode: 2100
    direction: c2s
    kind: request
    payloadType: Studio.Game.Protocol.CastSkillRequest
    response: cast-skill.response
    reliability: reliable
    minimumSchemaVersion: 1
    maximumSchemaVersion: 1
    maximumPayloadBytes: 8192
    captureSampleRate: 1.0
    sensitiveFields: [authToken]
```

消息的传输标识采用以下复合键：

```text
(catalogId, opCode, direction, kind)
```

这样，请求和响应消息可以共享同一个操作码，同时仍保持无歧义。消息 ID 在单个目录内唯一，目录 ID 在所有已加载项目中唯一。

编译器会拒绝未知的 YAML 成员，并校验保留操作码零、方向与消息类型的兼容性、模式版本范围、最大载荷大小、采样率、敏感字段唯一性以及请求与响应的关联关系。

## 4. 构建与持续集成

编辑目录后执行重新生成：

```powershell
./tools/compile-protocol-catalogs.ps1
```

持续集成必须使用检查模式，并在已提交的生成结果过期时失败：

```powershell
./tools/compile-protocol-catalogs.ps1 -Check
dotnet test src/AbilityKit.Protocol.Tests/AbilityKit.Protocol.Tests.csproj
```

`.github/workflows/abilitykit-test-gates.yml` 中的 `protocol-catalogs` 作业会在每次拉取请求和推送时执行检查模式，因此过期的生成结果会阻断门禁。`compile-protocol-catalogs.ps1` 是唯一治理入口：不带参数时重新生成并写回，使用 `-Check` 时只校验而不写入。

生成过程是确定性的：递归发现源文件后按序号顺序排序，JSON 和 C# 输出顺序遵循源文件顺序。目录变更和两个生成文件必须包含在同一个变更集中。

### Unity 协议工作区

打开 `Tools/AbilityKit/Framework/Protocol/Protocol Workspace`。窗口包含 Catalogs、Wire Schemas 和 Export 三个视图。它通过稳定的 JSON 工作区投影调用与持续集成相同的 .NET 编译器；Unity 不会实现第二套 YAML 解析器或校验器。

- 可以新增、复制、删除和编辑目录消息，并配置方向、消息类型和可靠性。只有运行时目录校验通过后，保存操作才会写入规范化 YAML。
- 线上模式声明 `projectId`、生成的 C# `namespace`、类型、稳定字段 ID、标量或自定义嵌套类型引用、数组或可选结构、MemoryPack 布局、声明或成员形式以及保留 ID。保存时会重新解析输出的 YAML，成功后才替换源文件。
- 项目导出会选择一个 `projectId`，输出 `ProtocolCatalogs.g.cs`、传递闭包内自有模式依赖对应的 `*.MemoryPack.g.cs` DTO，以及 `protocol-export.json`。
- MemoryPack 后端导出还会生成 `ProjectMemoryPackCodecs.g.cs`：其中包含封装 `MemoryPackSerializer` 的编解码门面，以及向 `ProtocolPayloadDecoderRegistry` 注册解码器的粘合代码。如果 DTO 的限定类型只映射到一个 MemoryPack 目录消息，还会附加 `[ProtocolOpCode]`。这些依赖 MemoryPack 的文件只存在于显式启用的导出目录；始终参与编译的 `BuiltInProtocolCatalogs.g.cs` 不会引用 MemoryPack 命名空间。
- 导出清单会列出每个被引用的 MemoryPack 载荷、生成类型、缺失的线上模式和生成文件。为支持渐进迁移，缺失模式默认仅产生警告；严格导出模式则会失败。

Wire Schema 只保留正式的分组源格式。每份文档使用 `schemaVersion: 2`，并由同一 `projectId` 下唯一且稳定的 `groupId` 管理一组共享 C# `namespace` 的相关类型。`defaults` 可统一声明 `memoryPackMode`、`declaration` 和 `memberStyle`，每个 `types` 条目仍可独立覆盖；字段 ID、保留 ID、兼容性身份和生成的 `*.g.cs` 文件仍然按类型隔离。

```yaml
schemaVersion: 2
projectId: abilitykit.shooter
groupId: battle
namespace: AbilityKit.Protocol.Shooter
defaults:
  memoryPackMode: sequential
  declaration: struct
  memberStyle: field
types:
  - name: ShooterPlayerCommand
    fields:
      - id: 0
        name: playerId
        scalarType: int32
        required: true
  - name: ShooterInputPayload
    declaration: class
    memberStyle: property
    fields:
      - id: 0
        name: commands
        type: AbilityKit.Protocol.Shooter.ShooterPlayerCommand
        array: true
        required: true
```

`groupId` 使用小写字母、数字、点或连字符，例如 `battle`、`state-sync`、`room.auth`。项目内公用类型进入正式的 `common` 组，真正跨项目的公用类型进入独立共享项目；不保留无归属类型，也不把 Catalog 传输策略塞入 Wire 文档。Unity 工作区会把分组文档展开为独立类型；保存某个类型时会保留同文件中的其他类型，使用 Wire Schemas 视图的 `Add Type` 可直接向当前 group 追加类型；文档级 `projectId`、`groupId` 和 `namespace` 则直接在 YAML 中维护。导出清单按组记录生成类型。完整格式示例见 `Protocols/README.md`。

编辑器所依赖的编译器命令仍可供无界面工具调用：

```powershell
dotnet run --project tools/AbilityKit.Protocol.CatalogCompiler -- `
  --input Protocols/Catalogs --wire-input Protocols/WireSchemas `
  --workspace-output local/Logs/protocol-workspace.json

dotnet run --project tools/AbilityKit.Protocol.CatalogCompiler -- `
  --input Protocols/Catalogs --wire-input Protocols/WireSchemas `
  --export-memorypack local/ProtocolExports/moba --project abilitykit.moba
```

仓库内正式的无界面生成与门禁入口如下。Catalog 和 Wire Schema 必须与生成的 C# DTO、解码器注册及导出清单一同提交：

```powershell
./tools/compile-protocol-catalogs.ps1
./tools/export-protocol-wire.ps1 -Projects shooter
./tools/export-protocol-wire.ps1 -Projects moba

./tools/compile-protocol-catalogs.ps1 -Check
./tools/export-protocol-wire.ps1 -Projects shooter,moba -Check -Strict
./tools/export-protocol-wire.ps1 -Projects moba -Check
```

`-Check` 会在临时目录执行确定性导出，并与已提交结果比较，不会写回仓库。比较前统一 CRLF 与 LF，因此纯换行符漂移不会产生误报；生成文件缺失、残留或内容过期时返回退出码 3。`-Strict` 还要求目标项目引用的每个 MemoryPack 载荷均有 Wire Schema，闭包不完整时返回退出码 4。Shooter 与 MOBA 均已完成严格闭包，持续集成必须对两者使用 `-Check -Strict`；Room 仍保持渐进式检查。

线上字段 ID 是稳定的 MemoryPack 顺序。新契约默认使用 `memoryPackMode: version-tolerant`、`declaration: class` 和 `memberStyle: property`；已删除的 ID 必须移入 `reservedIds`，不得被静默复用。旧有位置式契约可以使用 `memoryPackMode: sequential`，但 ID 必须从零开始连续排列，且不能存在保留空缺。`memberStyle: field` 可在将声明迁移到生成源码时保留现有的引用式 API。顺序模式仅用于在生成源码迁移期间保持现有字节格式；其字段列表必须冻结，并且每次迁移都必须具备线上字节黄金测试。

Shooter 已完成全部 12 个 MemoryPack 载荷的 Wire Schema 闭包。MOBA 已完成 catalog 引用的 16 个顶层 MemoryPack 载荷及其 10 个生成依赖类型，按 `input`、`room`、`state-sync` 三个稳定分组管理。两者 DTO 成员均由已提交的生成源码参与编译；构造函数、常量、转换逻辑和编解码器保留在手写分部类型中。固定黄金字节、生成类型往返测试、解码器注册测试、严格闭包和已提交源码新鲜度检查共同保护切换过程。外部包已经拥有的值对象与枚举使用字段级 `external: true` 显式引用，不在当前项目重复生成。

协议兼容基线位于 `Protocols/Compatibility/protocol-compatibility-baseline.json`。只有在评审确认当前 Catalog/Wire 源应成为新的兼容起点时，才可临时设置 `AK_UPDATE_PROTOCOL_COMPAT_BASELINE=1` 并运行完整 CatalogCompiler 测试刷新基线；刷新后必须清除该变量，再次运行完整测试和兼容性检查，禁止在普通持续集成中自动更新基线。

## 5. 运行时流量采集

`ConnectionManager` 会在内置协议中间件之前安装 `NetworkTrafficProbeMiddleware`。每个事件包含：

- 稳定的逻辑 `ConnectionId`；
- 表示物理会话的 `Generation`，每次重连后递增；
- 连接的 `Role`、`CatalogId`、端点和传输名称；
- UTC 时间戳、方向、操作码、序列号、标志和载荷长度；
- 可选的有界载荷预览。

默认只采集包头。仅当 `MaximumPayloadPreviewBytes` 大于零时才复制载荷字节。观察器和错误处理器的异常会被隔离，绝不会中断数据包转发。

`NetworkTrafficRingBuffer` 是线程安全的有界采集器。缓冲区已满时，它会淘汰最早的事件并递增 `DroppedCount`。它适合作为编辑器窗口、本地诊断页面或导出桥接器的数据源。

`NetworkTrafficInspector` 是这些消费端共用的数据关联层。它将入站和出站流量映射到目录方向，在数据包标志能够识别请求、响应或推送时使用这些标志；当传输标识不足时，返回明确的未知或歧义记录。只有预览包含完整载荷时，它才会调用已注册的解码器；被截断的预览仍可作为元数据检查，但绝不会传给 DTO 解码器。

观察器在管线当前的 IO 路径上执行，因此必须快速返回。高开销的解码、持久化和远程导出应写入由工作线程持有的有界缓冲区，而不是阻塞数据包处理。

`NetworkTrafficBatchExporter` 为远程和持续集成消费端实现了这条边界。`OnTraffic` 仅在短时锁保护下将事件写入固定容量队列，绝不会等待接收端。队列已满时会淘汰最早的待处理事件并递增 `DroppedCount`，从而保留最新的诊断上下文。工作线程负责解析目录元数据、解码载荷、应用与 `NetworkTrafficJsonExporter` 相同的脱敏策略，并通过 `INetworkTrafficBatchSink` 为每批数据发送一份 JSON 文档。达到 `BatchSize` 时立即刷新，`FlushInterval` 限制不足一批数据的等待时间；`StopAsync` 停止接收新事件，并在调用方的取消期限内排空待处理事件。接收端故障按批次隔离并计入 `FailedBatchCount`，不会隐式重试，因此从导出器边界看，远程投递保持最多一次语义。

宿主负责管理接收端凭据、重试策略、端点或路径配置以及构件保留策略。SDK 刻意不内置 HTTP 身份验证或重试策略。`DroppedCount` 统计关闭后被拒绝、从待处理队列淘汰，或因关闭期限触发强制取消而放弃的事件；`GetSnapshot()` 分别公开待处理、已接收、已丢弃、已导出和失败的事件或批次计数，供健康状态上报使用。

SDK 装配示例：

```csharp
var traffic = new NetworkTrafficRingBuffer(4096);
var battleClient = new NetworkSdkBuilder()
    .UseTransportFactory(CreateBattleTransport)
    .ObserveTraffic(traffic, options =>
    {
        options.Role = "battle";
        options.CatalogId = "abilitykit.moba.battle";
        options.MaximumPayloadPreviewBytes = 256;
    })
    .Build();
```

编辑器监视器和远程或持续集成导出器可以观测相同的物理会话，同时互不阻塞：

```csharp
var monitor = NetworkTrafficMonitor.Default;
using var exporter = new NetworkTrafficBatchExporter(
    monitor.Catalogs,
    monitor.Decoders,
    sink);
var observer = new NetworkTrafficObserverFanOut(
    new INetworkTrafficObserver[] { monitor, exporter });

var battleClient = new NetworkSdkBuilder()
    .UseTransportFactory(CreateBattleTransport)
    .ObserveTraffic(observer, options =>
    {
        options.Role = "battle";
        options.CatalogId = "abilitykit.moba.battle";
        options.MaximumPayloadPreviewBytes = 256;
    })
    .Build();
```

扇出中的每个观察器都具备异常隔离。应在宿主关闭边界释放导出器；如果宿主已持有异步关闭流程，则应带期限等待 `StopAsync`。

同一个采集器可以观测多个 Room 和 Battle 客户端，连接元数据会将各数据流分开。对于需要会话级状态的导出器，`ConfigureTrafficCapture` 可提供按连接代次创建观察器的工厂。

`NetworkTrafficMonitor` 是 Unity 编辑器窗口使用的 SDK 级多项目数据源。其默认实例持有容量为 8192 个事件的有界环形缓冲区和已生成的内置目录注册表；项目可以为测试或无界面工具创建容量更小的私有监视器。监视器本身绝不会创建连接，因此它只是观察和可视化边界，而不是第二个连接池。

从外部 `IConnection` 构建的 SDK 无法安全地安装中间件，因此会拒绝 `ObserveTraffic`。该连接的所有者必须在自己的管线中安装探针。

## 6. 客户端中心与租约

`NetworkSdkClientHub` 是多项目宿主的可复用客户端边界。它持有的是 `NetworkSdkClient` 实例，而不是原始 `IConnection` 对象，因此每个客户端始终只有一个请求跟踪器和一套连接生命周期。

```csharp
using var hub = new NetworkSdkClientHub();
var roomKey = new NetworkSdkClientKey("abilitykit.moba", "room", "primary");
var battleKey = new NetworkSdkClientKey("abilitykit.moba", "battle", "primary");

using var roomLease = hub.Acquire(roomKey, roomBuilder);
using var battleLease = hub.Acquire(battleKey, battleBuilder);
```

相同键会返回同一个客户端并递增其租约计数。项目、角色或实例 ID 任一不同，都不会共享客户端。释放最后一个租约不会关闭连接：Hub 仍然是所有者，并为下一个功能或会话缓存客户端。`Remove` 和 `Dispose` 是显式关闭边界；存在活动租约时执行移除会被拒绝。

`NetworkSdkClientHub.Default` 是示例和编辑器可见客户端的进程级装配根。它与 `NetworkSdkDiagnosticsMonitor.Default` 暴露的是同一个 Hub，因此从该 Hub 获取的生产示例会自动出现在内置的 Connections 和 Routes 视图中。短生命周期的示例启动器使用唯一实例 ID，在关闭时释放租约并移除条目，确保进程级诊断数据源不会保留已释放的客户端。测试和隔离宿主仍可创建私有 Hub。

这与套接字池有意保持区别。Room 控制连接和 Battle 数据面连接拥有不同的协议目录、心跳或重连策略、服务质量和故障域。Hub 在不合并这些边界的前提下统一所有权和复用。

## 7. 可视化与安全边界

可视化层应使用 `NetworkTrafficEvent.CatalogId` 和数据包标识关联 `ProtocolCatalogRegistry`，再通过 `ProtocolPayloadDecoderRegistry` 分发载荷字节。解码器由协议包注册，因此共享网络运行时无需依赖项目 DTO 程序集。

运行时接收链还必须在业务 handler 之前执行协议边界检查。`ProtocolPacketBoundaryValidator` 可安装到 `NetworkPacketRouter`，按目录消息的方向、kind、schema 版本范围、`maximumPayloadBytes` 和 frame payload 长度拒绝输入；被拒绝的包不会进入任何业务 handler。若需要直接解码，应使用带 `ProtocolMessageDefinition` 的 bounded `ProtocolPayloadDecoderRegistry.Decode` 重载。旧的按 `catalogId/messageId` 解码重载仅为兼容保留，不得用于新的 transport、dispatch 或观测入口。失败结果使用 `ProtocolDecodeFailureKind`/`ProtocolPacketBoundaryFailureKind` 分类，指标和断线策略不得依赖异常文本。

连接或 session handshake 应先调用 `ProtocolCatalogRegistry.TryNegotiateSchemaVersion`（或 `ProtocolSchemaVersionNegotiator.TrySelect`）计算双方消息范围的交集，再把选定版本传给接收边界。Wire Schema YAML 的 `schemaVersion: 2` 是文档格式版本，catalog `revision` 是发布修订号，线上 schema version 范围是第三个独立概念；框架不会自动把它们互相转换。

当握手需要交换完整目录广告时，使用 `ProtocolCatalogNegotiator.Negotiate`：它校验 catalog/project/domain 身份，对双方共有消息校验 opcode、方向、kind、codec 和版本范围，并返回按消息 ID 索引的 `SelectedSchemaVersions`。新增消息 ID 不会阻断旧客户端；共有消息没有版本交集或传输身份发生变化时，结果为不可兼容并携带 `ProtocolCatalogNegotiationFailureKind`。协商结果应绑定当前连接代次，重连后重新协商，不能跨连接复用。

正式广告消息由 `abilitykit.system` catalog 定义，使用操作码 `1` 的 `catalog-advertisement.request/response`。载荷通过 `ProtocolCatalogAdvertisement.FromCatalogs` 生成，并由 `ProtocolCatalogAdvertisementCodec` 执行确定性、有界的 `custom-binary` 编解码。一次广告可以携带同一物理连接上的多个 catalog；`ProtocolCatalogRegistry.TryNegotiateAdvertisement` 会忽略本地未知的可选 catalog，但要求双方共有的每个 catalog 都兼容。业务不得另行定义临时 JSON 广告，系统广告也不属于需要迁移到业务 Wire Schema 的 MemoryPack DTO。

启用 `requireNegotiated` 时，系统广告消息本身必须加入 `bootstrapMessageIds`，否则接收边界会在完成协商前拒绝用于完成协商的消息。认证策略仍由业务决定：可在认证成功后交换广告，也可把认证消息和系统广告共同作为最小 bootstrap 集合。

`ProtocolCatalogNegotiationSession` 提供了单 catalog 的连接代次状态：`ConnectionManager.StartConnect` 会自动将已配置的会话置为 `Pending`，业务在自己的认证/握手响应收到后调用 `ConnectionManager.ApplyRemoteCatalog`。启用 boundary 的 `requireNegotiated` 后，除显式列入 `bootstrapMessageIds` 的登录/系统握手消息外，Pending 或 Failed 状态的包会在所有 legacy 与 typed route 入口前被拒绝；协商成功后才使用保存的逐消息版本。Room、MOBA、Shooter 仍可使用不同认证流程，但目录广告统一使用系统 catalog。

`NetworkPacketRouterSnapshot.BoundaryRejectedCount` 用于区分协议边界拒绝与普通未知路由；编辑器诊断窗口应同时展示该计数。边界拒绝不会进入 legacy packet 事件，未知 opcode 在未启用 `rejectUnknownMessages` 时仍保持兼容投递。

`sensitiveFields` 是治理元数据，并不意味着自动完成字节级脱敏。原始载荷预览在解码前可能包含凭据或个人数据。生产配置应保持仅采集包头，或应用采集过滤器。解码后的可视化或导出层必须在持久化或远程导出前，对目录声明的字段执行脱敏。

SDK 的 `NetworkTrafficJsonExporter` 为首个编辑器工作流实现了这条边界：

- 解码值会被投影为兼容 JSON 的普通值，并以不区分大小写的方式递归脱敏名称与 `sensitiveFields` 匹配的公共字段或属性；
- 默认省略原始载荷预览，因为字节数据无法安全地按字段脱敏；
- 为敏感消息启用原始预览时，必须显式设置受控工作流标志，且编辑器窗口会显示二次确认；
- 未知、歧义和截断的数据包仍可连同解码错误作为元数据导出，而不会被静默猜测或部分解码。

在开发版本或编辑器中打开 `Window/AbilityKit/Network Diagnostics`。统一窗口包含 Connections、Routes 和 Traffic 三个页签。Connections 和 Routes 使用来自 `NetworkSdkDiagnosticsMonitor.Default` 的不可变快照；Traffic 保留有界检查、过滤、解码和受保护的 JSON 导出工作流。MOBA Room/Battle 和 Shooter Room/Battle 示例装配路径仅在 `UNITY_EDITOR || DEVELOPMENT_BUILD` 条件下启用载荷采集；Release 构建不会分配载荷预览。注入的 GameFramework `IConnection` 实例仍需由其所有者显式启用，因为 SDK 无法在构造后安全插入中间件。旧菜单项 `Window/AbilityKit/Network Traffic Monitor` 继续作为兼容别名保留。

### 解码器模块装配

每个协议包都持有一个显式且依赖局部化的解码器模块：

```csharp
var decoders = NetworkTrafficMonitor.Default.Decoders;
RoomProtocolDecoderModule.Register(decoders);
MobaProtocolDecoderModule.Register(decoders);
ShooterProtocolDecoderModule.Register(decoders);
```

应用程序在启用流量采集前，只调用其实际承载协议对应的模块。模块注册使用 `ProtocolPayloadDecoderRegistry.TryRegister`，因此重复启动、Unity 域重载和多个装配根均是安全的。注册遵循先到先得：宿主可以在协议包模块之前安装自定义解码器，协议包不会覆盖它。设计上刻意避免静态构造函数和程序集扫描，因为其执行顺序无法作为可靠的多项目装配契约。

Room、MOBA 和 Shooter 示例客户端仅在编辑器或开发版本的可观测路径中安装这些模块。测试会枚举已生成内置目录中的所有消息，并要求存在匹配的解码器注册，从而避免新增 YAML 消息在流量监视器中静默变为不可解码状态。

采样策略属于目录元数据。`NetworkTrafficCatalogSampler` 会根据 `ProtocolCatalogRegistry` 为每个物理会话创建过滤器。它使用目录 ID、传输方向、操作码和数据包标志解析唯一消息，并在分配载荷预览之前计算 `captureSampleRate`。采样键由按序号比较的 UTF-16 目录 ID、稳定连接 ID、重连代次、方向、操作码、序列号和标志组成。实现使用固定的 64 位 FNV-1a 哈希，绝不调用具有进程随机性的 `GetHashCode()`。采样率 `0` 和 `1` 是精确边界；未知或歧义消息默认保留，确保在没有唯一策略时采样不会静默丢弃流量。

`NetworkTrafficMonitor.SamplingMetrics.GetSnapshot()` 会公开已检查、已采集、已采样或过滤掉以及无法解析的总数。这些进程内计数适用于编辑器工具栏和宿主指标适配器；SDK 不会持久化它们。

## 8. 迁移规则

示例运行时包装必须将请求生命周期和数据包路由委托给框架所有者：

- 装配或注入 `NetworkSdkClient`；禁止在示例运行时代码中构造私有 `RequestClient`；
- 通过 SDK 或路由器接口订阅；禁止在示例包中新增操作码到处理器的字典；
- 接收 `IConnection` 的兼容构造函数必须立即将其包装为唯一且由自身持有的 SDK 客户端；
- 框架包（`network.runtime`、`network.sdk`、`network.room`）可以保留请求或路由适配器，因为这些抽象由它们持有；
- `SampleNetworkArchitectureTests` 扫描示例运行时的非测试源码，拒绝新增直接请求客户端或私有推送处理器映射。

1. 在不改变序列化的前提下盘点操作码和现有 DTO 类型。
2. 按项目和领域分别添加目录，并显式配对请求与响应条目。
3. 执行生成，并在采用生成的注册表之前修复所有校验错误。
4. 优先在诊断和工具边界使用 `ProtocolCatalogRegistry`，替换临时的操作码元数据查找逻辑。`ProtocolMetadataRegistry` 仅作为兼容投影；新装配应使用生成代码的 `CreateRegistry(ProtocolCatalogRegistry)`，避免维护第二套消息索引。
5. 为可视化注册项目持有的解码器，不得将项目 DTO 依赖移入共享运行时。
6. 仅在明确的开发或受控诊断配置中启用载荷预览。
7. 将载荷迁移为生成方式时，应添加自有的 `*.wire.yaml`，使其限定命名空间和类型与目录的 `payloadType` 一致；检查导出清单，并仅在线上兼容性测试通过后替换手写 DTO。
8. 同步 serializer 必须在应用启动边界通过 `WireSerializer.Install` 或 codec-specific installer 显式安装；默认拒绝静默覆盖，只有受控重配置才可指定 `replaceExisting`。工具存在 Protobuf backend 不代表运行时会自动安装 Protobuf serializer。

字段布局变更仍属于 IDL 迁移。操作码、方向、消息类型、编解码器或兼容范围的变更属于目录契约变更，必须递增 `revision`。删除或复用操作码需要显式兼容窗口；禁止在同一目录中静默复用。

## 9. 后续扩展

- Protobuf 确定性导出 backend 已用于验证 SPI；在作为生产跨语言契约前，仍需补齐 custom type package/import 规划与 `protoc` 编译门禁。
- 使用项目级覆盖扩展目录感知采样器，以支持传输层提供了公共包头标志无法表达的数据包类型信号的场景。
- 根据目录载荷元数据生成或校验解码器模块注册存根，同时将编解码实现保留在协议包内。
- 在 `INetworkTrafficBatchSink` 之上增加用于 HTTP 接入和持续集成构件文件的远程或持续集成接收端实现；SDK 仍刻意将凭据、重试和构件路径交由宿主集成层管理。
