# Room Protocol 使用指南

> 文档类型：使用指南
> 适用范围：`com.abilitykit.protocol.room` 0.1.0，2026-11-25 仓库工作区源码及 .NET 镜像
> 核校状态：部分核校；下列 DTO/codec、固定基线和工具的 E0 契约已对照源码；完整本地示例与既有消费者的 E3 结果见第 7 节
> 事实基线：2026-11-25；不包含真实 Gateway、Unity Player 或跨版本兼容验收

## 1. 目标与前置条件

本包定义跨客户端和服务端共享的消息形状，不执行登录、房间状态机、输入调度、快照导入或重连。版本、Unity 条件和直接依赖以 [package.json](../package.json) 为准。[运行程序集](../Runtime/com.abilitykit.protocol.room.asmdef) 另引用 MemoryPack；安装本包不能代替安装相容的第三方程序集和生成支持。.NET 镜像使用 MemoryPack 1.21.0，本地验证不证明任意 Unity/MemoryPack 组合都可用。

主要入口：

| 使用目的 | 随包源码 | 调用者另负责 |
| --- | --- | --- |
| Room 请求、响应、推送 | [操作码](../Runtime/Room/RoomGatewayOpCodes.cs)、[DTO](../Runtime/Room/WireRoomGatewayTypes.cs) | 请求身份、方向、包络、超时、认证与业务 `Success` |
| DTO 字节转换 | [WireRoomGatewayBinary](../Runtime/Room/WireRoomGatewayBinary.cs) | 消息类型选择、长度预算、异常与跨异步边界的复制 |
| 固定基线信封 | [FixedBaselineStateSyncProtocol](../Runtime/Room/FixedBaselineStateSyncProtocol.cs) | generation 隔离、基线持有、重建、导入和成功后提交 |
| 可靠事件 | [事件 DTO](../Runtime/Room/WireReliableBattleEventTypes.cs) | 去重、排序、应用和确认水位 |
| 时间同步消息 | [时间同步 DTO](../Runtime/GatewayTimeSync/WireTimeSyncTypes.cs)、[codec](../Runtime/GatewayTimeSync/WireTimeSyncBinary.cs) | 时钟采样与估计；消息存在不等于时钟同步完成 |
| 诊断解码 | [RoomProtocolDecoderModule](../Runtime/Room/RoomProtocolDecoderModule.cs) | 注册时机和缺失 decoder 处理；诊断不是业务执行 |

## 2. 完整示例：本地编解码

以下为完整 Console 程序，引用已装配本包及 MemoryPack 的工程即可执行；不连接服务端。预期输出 `room protocol local roundtrip passed`。

```csharp
using System;
using AbilityKit.Protocol.Room;

var request = new WireRoomAccountLoginReq
{
    AccountId = "local-account",
    ExpireSeconds = 60,
    KickExisting = false
};
var bytes = WireRoomGatewayBinary.Serialize(in request);
var decoded = WireRoomGatewayBinary.Deserialize<WireRoomAccountLoginReq>(bytes);
if (decoded.AccountId != request.AccountId || decoded.ExpireSeconds != 60)
    throw new InvalidOperationException("DTO roundtrip failed");

var generation = Guid.Parse("82ae1de2-cb91-4494-8e1d-5cb9f1046ea1");
var envelope = FixedBaselineStateSyncProtocol.Encode(generation, 1, 0, 1, new byte[] { 7 });
if (!FixedBaselineStateSyncProtocol.TryDecode(envelope,
        out var actualGeneration, out var snapshotId, out var baseId,
        out var projectionVersion, out var payload))
    throw new InvalidOperationException("Envelope rejected");
if (actualGeneration != generation || snapshotId != 1 || baseId != 0 ||
    projectionVersion != 1 || payload.Length != 1 || payload[0] != 7)
    throw new InvalidOperationException("Envelope roundtrip failed");
Console.WriteLine("room protocol local roundtrip passed");
```

这里只验证字节布局与返回值；`AccountLogin` 是消息示例，不证明账号具备权限。`baseId = 0` 是无前置基线的信封身份，payload 的 full/delta 业务含义仍由内层投影契约决定。

## 3. 消息身份与生成代码

操作码、方向、请求/响应或推送标志、catalog 与 payload 类型要一起匹配。相同操作码的 request/response 不代表同一 DTO；不能仅凭数字猜测类型。当前 `BeginLoading` 为 112、`LeaveRoom` 为 119。不要照抄旧 README 的冲突表。

[生成目录](../Runtime/Generated/MemoryPack/ProtocolCatalogs.g.cs) 及相关 DTO/codec 是生成输出，不手工修补。跨版本修改先核对仓库的协议源、生成入口和消费者；本包不以修改生成的 C# 文件代替发布契约。`RoomProtocolDecoderModule.Register` 使用 `TryRegister`，已有同身份 decoder 不会被覆盖；注册不访问网络，也不保证覆盖完整 catalog。已知 movement-proof request/response 的诊断覆盖缺口见第 7 节。

## 4. 编解码、内存与线程

| API 或对象 | 当前实现与调用约束 |
| --- | --- |
| `Serialize<T>(in T)` | MemoryPack 生成新的字节数组，以 `ArraySegment<byte>` 返回；只含 DTO，不含连接帧或额外状态包络 |
| `Deserialize<T>` | 空数组/空 span 返回 `default`；非空坏数据可抛异常。返回 `default` 不代表合法消息，不是通用安全验证接口 |
| `ReusableMemoryPackSerializationBuffer` | 非线程安全、无 `Dispose`，不是 ArrayPool 租约；扩容或长度变化仍可分配 |
| buffer 的 `SerializeTransient<T>` / `SerializeTransientSegment<T>` | 前者返回临时精确长度数组，后者借用可复用写缓冲片段；`WireRoomGatewayBinary.SerializeTransient` 门面将前者包装为 ArraySegment。均按“下次序列化前有效”使用，保存、异步发送或转线程前复制 |
| snapshot transient 特化 | .NET 与 Unity 条件分支不同，不能以本机结果承诺跨端相同分配或零 GC |
| `WireStateSyncSnapshotPushDecodeBuffer`、`WireReliableBattleEventPushDecodeBuffer` | 复用解码对象；返回 struct 仍可引用内部 list/数组，下次 Decode 可复用或改变它们。需长期保留时复制所需嵌套数据，不能只复制外层 struct |

复用缓冲区仅由一个串行调用者持有；不要在异步请求并行、回调重入或多个连接间无保护共用。业务 DTO 的字符串、列表与字节数组也不因外层为 struct 而成为不可变深快照。调用者在网络边界另设载荷预算、捕获可预期解码失败并检查响应业务字段。

## 5. 固定基线信封与恢复边界

`FixedBaselineStateSyncProtocol.SchemaVersion` 当前为 4，表示外层 payload 是固定基线信封，不是玩法投影版本。固定头为 36 字节，总长度上限为 4 MiB；内层投影版本由独立字段保存。

`Encode` 只检查 payload 长度并复制字节，不校验 generation/快照 ID 等语义。因此 Encode 成功的字节也可能被 TryDecode 拒绝。TryDecode 要求：

1. 总长度在 36 字节至 4 MiB 之间，generation 非空。
2. `snapshotId > 0`，`0 <= baseSnapshotId < snapshotId`。
3. `0 <= projectionSchemaVersion < 4`。

成功时复制内层 payload；失败时部分 out 参数可能已赋值，以 bool 为准，失败结果不可提交。codec 不检查客户端是否持有对应基线，不重建差量、不导入世界，也不更新接收游标。

所属状态同步会话应先验证身份和基线，再重建完整权威状态、成功导入，最后提交快照位置/确认。帧同步则按权威输入帧推进，并使用该链自己的检查点与重演契约；不能把 snapshot ID、可靠事件水位、帧同步确认帧或状态同步输入确认互换。是否附带状态推送不决定主同步模式。

## 6. 辅助工具、排错与迁移

- [RoomGatewayIds](../Runtime/Room/RoomGatewayIds.cs) 的数值房间 ID：空白输入为 0，非空按原始 UTF-16 字符计算稳定 64 位哈希，哈希 0 改为 1；不 trim，不可逆、不保证无碰撞，不用于认证。
- [RoomLaunchManifestHash](../Runtime/Room/RoomLaunchManifestHash.cs)：引用 ordinal 排序、metadata 按 key ordinal 排序后按源码定义的分行格式作 UTF-8 SHA-256，结果为小写十六进制。重复引用保留、值不规范化；输入中换行/分隔符须由上层约束，hash 不证明资源存在、签名或运行时兼容。
- DTO 能解码但 `Success=false`：先检查响应类型和业务 Message，不把字节往返当成服务端接受。
- 上一帧对象莫名变化：检查是否保存了 reusable buffer 的临时结果；复制嵌套字段后再跨帧持有。
- 固定基线解码成功但无法恢复：检查 generation、基线缓存、投影版本、完整状态重建及导入事务；不要提前推进游标。
- 从旧 README 迁移：版本从包声明读取，使用 `RoomGatewayOpCodes` 常量，详细契约统一使用本文。没有在本批更改操作码或线上布局。

## 7. 验证范围与关联文档

2026-11-25 核对上述源码、生成 catalog 与消费者；.NET 本地完整示例 1/1 通过，Room 选定测试 77/77、SDK 149/150；具体命令与日志记录在仓库 D3 支撑批次。SDK catalog diagnostic 既有用例指出 `subscribe-state-sync-movement-proof.request/response` 缺 decoder；业务 codec 与诊断 decoder 的覆盖不能混称。本批不补 runtime 或生成物。

未发现独立的本包测试工程，不虚构包级 suite；消费者测试也不覆盖每个 DTO 或所有坏数据。未运行完整 gate、Unity Editor/Player、Mono/IL2CPP、隔离安装、真实 Gateway、跨版本部署、性能或 CI。证据仅为 E0 与本地局部 E3，不新增 E4/E5。

返回[文档索引](README.md)；安装前提见[包入口](../README.md)。
