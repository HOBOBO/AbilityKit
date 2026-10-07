---
name: protocol-wire
description: 修改 MOBA、Shooter 或 Room 的协议目录、分组 Wire Schema v2、MemoryPack DTO、协议导出清单、解码器注册或兼容性基线时使用。
---

# 线协议开发流程

修改协议消息、载荷、opcode、序列化布局、生成 DTO 或协议导出时，均按本 Skill 执行。

## 权威来源

- 传输身份与策略：`Protocols/Catalogs/*.protocol.yaml`。
- 载荷字段与序列化布局：`Protocols/WireSchemas/*.wire.yaml`。
- Wire Schema 生成只适用于由目录管理的 `memorypack` 传输载荷。`custom-binary` 编解码器、仅用于旧格式解码的布局、临时格式化缓冲区和进程本地持久化记录仍归各自专用编解码器管理。
- 只使用分组的 Wire Schema v2：`schemaVersion: 2`、`projectId`、稳定的 `groupId`、组内共享的 `namespace` 和 `types[]`。
- 不新增 schema v1 或每个类型单独一个文件的 Wire 定义。

## 分组与依赖规则

- 按业务归属、命名空间和发布节奏分组。内聚的业务域放在同一组；归属或命名空间不同的内容分别建组。
- 项目内复用类型放入稳定的 `common` 组；跨项目类型必须有真实的共享归属。
- 自定义 `type` 字段默认进入生成依赖闭包。只有完全相同的值对象或枚举已由另一编译包持有时，才使用 `external: true`。
- C# `byte` 字段使用 `scalarType: uint8`。
- 在 Unity Protocol Workspace 中编辑类型所属的分组文档；用 `Add Type` 向选中组追加类型。编译器会保留同组其他类型并拒绝重名类型。不要拆成每类型一个文件。

## 兼容性规则

- 现有 `MemoryPackOrder` 必须原样映射为字段 `id`。
- 顺序布局的 MemoryPack ID 从零开始连续编号。调整顺序、删除、扩大类型或重编号前，必须完成兼容性评审。
- DTO 序列化字段只放在生成的 `*.MemoryPack.g.cs` 中。手写 partial 类型可包含构造函数、校验、常量、转换和编解码逻辑，不可重复声明序列化字段或 `[MemoryPackable]` 特性。
- 不手改生成的 DTO、目录装配代码、编解码器注册表、导出清单或兼容性基线内容。

## 运行时边界规则

- 网络接收边界使用带约束的重载 `ProtocolPayloadDecoderRegistry.Decode(catalogId, definition, payload, schemaVersion)`；或者在调用业务处理器前，为 `NetworkPacketRouter` 安装 `ProtocolPacketBoundaryValidator`。
- 旧重载 `Decode(catalogId, messageId, payload)` 仅供兼容和本地可信调用方使用。新的传输、分发、抓包及诊断代码不得使用它，因为它无法执行目录约束。
- 将 `ProtocolDecodeFailureKind` 和 `ProtocolPacketBoundaryFailureKind` 视为稳定的指标与策略分类，不根据异常文案分支。
- Wire Schema 文档中的 `schemaVersion` 表示 YAML 文档格式。线上契约协商使用各消息的 `minimumSchemaVersion`、`maximumSchemaVersion` 和 `ProtocolSchemaVersionNegotiator`；它们也不同于目录的 `revision`。
- 在建立连接或会话时协商目录与消息的 schema 范围，并把选定版本传给接收边界。目录注册辅助类不会自动完成传输握手。
- 正式目录握手使用生成的 `abilitykit.system` 目录及 `catalog-advertisement.request/response`。通过 `ProtocolCatalogAdvertisement.FromCatalogs` 构造载荷，并使用 `ProtocolCatalogAdvertisementCodec`；不要另造项目专用的 JSON 或二进制声明格式。
- 一条物理连接可声明多个目录。聚合载荷使用 `ProtocolCatalogRegistry.TryNegotiateAdvertisement`，单个目录使用 `ProtocolCatalogNegotiator.Negotiate`，并保存各结果的 `SelectedSchemaVersions`。未知的可选目录可以共存，但双方共有的目录必须兼容。
- 系统声明承载动态目录/消息集合，因此使用有边界的正式 `custom-binary` 契约。不要将它迁移成生成的业务 DTO，也不要把目录 `revision` 当作线协议版本比较。协商失败类型应作为稳定的策略与指标分类。
- 每条物理连接绑定一个 `ProtocolCatalogNegotiationSession`；每次重连时重置。业务握手验证远端身份后才能应用远端目录。若协商前不允许接收业务包，在接收边界启用 `requireNegotiated`。
- 当连接仍需登录而协商门禁已启用时，`bootstrapMessageIds` 仅明确列出登录和握手消息 ID；不要为放行认证而关闭整道门禁。

## 必要流程

```powershell
./tools/compile-protocol-catalogs.ps1
./tools/export-protocol-wire.ps1 -Projects shooter,moba

./tools/compile-protocol-catalogs.ps1 -Check
./tools/export-protocol-wire.ps1 -Projects shooter,moba -Check -Strict
dotnet test src/AbilityKit.Protocol.CatalogCompiler.Tests/AbilityKit.Protocol.CatalogCompiler.Tests.csproj
dotnet build src/AbilityKit.Protocol.Moba/AbilityKit.Protocol.Moba.csproj
dotnet build src/AbilityKit.Protocol.Shooter/AbilityKit.Protocol.Shooter.csproj
```

每个修改过的载荷族都要运行聚焦的黄金字节测试和往返测试。只有经过评审的 schema 确定成为新基线时才更新兼容性基线，并重新执行只读检查。
