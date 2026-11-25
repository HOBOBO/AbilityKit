# 协议源文件格式

协议目录与线协议结构定义各有职责：

- `Catalogs/*.protocol.yaml` 分配传输标识并定义运行时策略。
- `WireSchemas/*.wire.yaml` 定义载荷字段和序列化兼容性。

分组线协议结构生成覆盖已登记到协议目录的 `memorypack` 传输载荷。
仅用于 `custom-binary` 编解码器、旧版解码、临时缓冲区或进程本地持久化的类型，
仍由相应专用实现负责，不计为缺失的线协议结构。

`Catalogs/system.protocol.yaml` 是交换一个或多个协议目录通告的正式引导契约。
其动态载荷由有界的 `ProtocolCatalogAdvertisementCodec` 自定义二进制编解码器负责；
应用不应在业务协议目录中重复定义此契约，也不应将其建模为线协议 DTO。

房间状态同步中，`subscribe-fixed-baseline-state-sync`（123）使观察者采用外层快照结构版本 4。
快照操作码和 `WireStateSyncSnapshotPush` 布局不变。
有界的 `FixedBaselineStateSyncProtocol` 编解码器负责不透明的 `Payload`：
先写入 16 字节 .NET GUID，再写入小端 int64 快照 ID 和基线 ID、int32 内层结构版本，
最后附上原始玩法载荷字节。基线 ID 为零表示完整快照；非零值指向同一流代际、同一世界中的某个完整快照。
事件游标与载荷操作码保留既有含义。旧订阅者继续使用操作码 103 和原有包络。

## 分组线协议结构

规范的线协议格式将同一项目、稳定业务域 `groupId` 和 C# 命名空间下的相关类型归为一组。
文档默认值应用于所有类型，类型可单独覆盖。每个类型仍有自己的字段 ID、
保留 ID、兼容性标识和生成源文件。

```yaml
# yaml-language-server: $schema=../wire-schema.schema.json
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
      - id: 1
        name: moveX
        scalarType: float
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

省略 `defaults` 时，默认值为 `version-tolerant`、`class` 和 `property`。
`types` 中的每一项均可覆盖默认值。分组文档至少包含一个类型，且类型名在文档内必须唯一。

`groupId` 在项目内唯一，使用小写字母、数字、点和连字符。应选择职责集中的业务域，
如 `battle`、`state-sync` 或 `room.auth`。项目内共享类型归入正式的 `common` 组；
跨项目共享类型归入专门的共享项目。类型的维护归属或发布节奏不同时应拆组。
不要在同一文档中混放协议目录消息和线协议类型。

C# `byte` 字段使用 `scalarType: uint8`。自定义 `type` 通常会加入生成依赖闭包。
仅当该确切类型已由其他包拥有并编译时（例如共享值对象或枚举），才设置 `external: true`；
字段保留声明的 C# 类型，当前项目不再生成另一份定义。
不要用 `external` 掩盖缺失的项目内线协议类型。

Unity 协议工作区将分组文档展开为独立类型。保存类型时只更新对应条目，并保留同组其他类型；
`添加类型` 为所选组暂存新类型，保存时追加。检查器显示类型的最终有效设置；
文档级默认值及 `projectId`、`groupId`、`namespace` 直接在 YAML 中编辑。
导出清单按所属组记录每个生成类型。

修改协议源文件后，重新生成并检查入库产物：

```powershell
./tools/compile-protocol-catalogs.ps1
./tools/export-protocol-wire.ps1 -Projects shooter,moba

./tools/compile-protocol-catalogs.ps1 -Check
./tools/export-protocol-wire.ps1 -Projects shooter,moba -Check -Strict
```
