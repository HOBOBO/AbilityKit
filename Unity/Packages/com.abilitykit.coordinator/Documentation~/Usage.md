# Coordinator 使用指南

> 文档类型：使用指南
> 适用范围：`com.abilitykit.coordinator` 0.1.0，2026-11-25 仓库工作区 Runtime 与 .NET 镜像
> 核校状态：部分核校；配置、策略、桥接及 DTO/codec 的 E0 契约已对照源码；局部 E3 见第 7 节
> 事实基线：2026-11-25；不包含完整会话或 Unity 独立消费验收

## 1. 目标与前置条件

本包用于项目之间共享会话配置、世界驱动接口和数据边界。调用者拥有会话生命周期、连接、输入调度、固定步长时钟、同步算法、恢复与释放。本包没有可直接创建的 `SessionCoordinator`，也没有 Local/Remote/Hybrid adapter、transport 或运行循环。

版本、Unity 条件及直接依赖以 [package.json](../package.json) 为准；[运行程序集](../Runtime/com.abilitykit.coordinator.asmdef)引用 MemoryPack，项目需提供相容运行库及生成支持。当前 .NET 镜像使用 net10.0 和 MemoryPack 1.21.0，不能据此证明所有 Unity/MemoryPack 组合或独立安装已经通过。

## 2. 完整示例：本地配置与负载转换

以下完整 Console 程序引用本包及其依赖后可运行，不启动 world 或网络。它检查本地策略、远程混合策略以及现有 `EntityState` 的 MemoryPack 往返；预期输出 `coordinator local contracts passed`。

```csharp
using System;
using AbilityKit.Coordinator;
using AbilityKit.Coordinator.Core;

internal static class Program
{
    private static void Main()
    {
        var local = SessionConfig.CreateLocal(playerId: 1);
        local.WorldType = "battle";
        local.SyncMode = SyncMode.Hybrid;
        local.EnableClientPrediction = true;
        var localPolicy = local.ResolveRuntimePolicy();
        Require(localPolicy.EffectiveSyncMode == SyncMode.Lockstep);
        Require(!localPolicy.RequiresNetwork && !localPolicy.EnableClientPrediction);

        var remote = SessionConfig.CreateHybrid(1, "127.0.0.1", 9000, roomId: 42);
        remote.MaxPredictionAheadFrames = -1;
        var remotePolicy = remote.ResolveRuntimePolicy();
        Require(remotePolicy.RequiresNetwork && remote.ServerEndpoint.IsValid);
        Require(remotePolicy.EnableClientPrediction && remotePolicy.MaxPredictionAheadFrames == 0);

        var state = new EntityState { EntityId = 7, X = 1.5f };
        var bytes = CoordinatorPayloadCodec.Encode(in state);
        Require(CoordinatorPayloadCodec.TryDecode<EntityState>(bytes, out var decoded));
        Require(decoded.EntityId == 7 && decoded.X == 1.5f);
        Require(!CoordinatorPayloadCodec.TryDecode<EntityState>(Array.Empty<byte>(), out _));
        Console.WriteLine("coordinator local contracts passed");
    }

    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Contract check failed.");
    }
}
```

示例端点只用于配置判断，没有连接或端口探测。`IsValid` 成功不表示连通性。

## 3. 配置与派生策略

[SessionConfig](../Runtime/Session/SessionConfig.cs)是可变 struct；复制时值字段复制，`SubFeatures` 列表仍共享引用。项目应在会话装配前完成配置，并自行管理并发修改与验证。`ResolveRuntimePolicy()` 返回派生值，不修改原配置，不启动功能，也不验证地图、TickRate、端点或配置组合能否运行。

| 输入 | 派生行为 |
| --- | --- |
| `HostMode.Local` | 有效同步模式强制为 Lockstep，`RequiresNetwork=false`，不启用客户端预测 |
| `HostMode.Host` 或 Client | `RequiresNetwork=true`，即使 SyncMode 是 Lockstep；联网需求由宿主模式决定 |
| 有效模式为 Hybrid | `SupportsPrediction=true`；仅在配置另启用预测时 `EnableClientPrediction=true` |
| 实际启用预测 | 最大超前帧取 `Math.Max(0, MaxPredictionAheadFrames)`；否则为零 |
| gate/spawn 选项 | 透传要求；是否检查门控或执行出生由消费者实现 |

`Default` 产生新 SessionId、`WorldType="battle"` 和 30 TickRate；各 `Create*` 工厂不是从 `Default` 完整复制。使用 `CreateLocal`、`CreateHost`、`CreateStateSyncClient`、`CreateHybrid` 后，应按项目补齐 WorldType、地图、WorldId、端点和其他必要字段。正 TickRate 与世界创建条件需由装配者验证，不能仅调用工厂后直接假定配置完整。

[NetworkEndpoint](../Runtime/Session/NetworkEndpoint.cs)原样保存主机字符串与端口；`IsValid` 仅为主机非 null/空串且端口大于零，不修剪空白、不限定端口上界、不解析 DNS、不拨号。传输层仍需验证地址和端口。`None` 是默认未配置值，比较使用原始字符串和端口。

## 4. 宿主与世界驱动所有权

[ISessionCoordinatorHost](../Runtime/Session/ISessionCoordinatorHost.cs)提供创建 host、配置 `WorldCreateOptions`、注册服务、加载配置与创建出生数据的方法；`ISessionCoordinatorConfigPolicy.ConfigureSession(ref config)`允许项目装配前调整配置。这些只是调用契约，本包没有自动执行这些方法的总装器或默认调用顺序，资源清理也由调用方实现。

[ILogicWorldDriverBridge](../Runtime/WorldBridge/ILogicWorldDriverBridge.cs)提供 Start/Stop、输入提交、单帧推进、当前帧与逻辑时间（秒）、实体状态采样。生命周期、输入保留/复制、线程、deltaTime 验证、是否清空队列和停止后行为取决于具体实现；不要对接口自行推断幂等性或线程安全。

[ILogicWorldDriveGate](../Runtime/WorldBridge/ILogicWorldDriveGate.cs)只返回是否可推进。设置 `RequireLogicWorldDriveGate` 不会自动寻找或调用它；宿主应明确检查点并避免 world 被两套循环重复推进。[ISpawnService](../Runtime/WorldBridge/ISpawnService.cs)同样是由宿主实现和调用的出生服务契约。

高频采样可实现 `ILogicWorldDriverStateBufferBridge`，并调用[状态缓冲扩展](../Runtime/WorldBridge/LogicWorldDriverStateBufferExtensions.cs) `driver.FillAllEntityStates(buffer)`：

- 扩展方法不清空调用者缓冲；需要当帧全量时先自行 `Clear()`，返回值是本次追加数量，不是 buffer 总数。
- driver 或 buffer 为 null 时抛 `ArgumentNullException`。
- 支持缓冲桥接时直接委托具体实现；否则调用旧 `GetAllEntityStates()` 并逐项追加，其返回数组及分配由实现承担，不保证零分配。
- 容量、实体顺序、跨线程使用和快照持有期由项目约定；复用 list 不等于内部 payload 也已复制。

## 5. 数据与负载契约

[PlayerInput](../Runtime/Data/PlayerInput.cs)保存 Frame、PlayerId、OpCode 与 byte[] Payload；操作码和载荷协议由玩法定义，没有内置 Move/Skill payload。构造不复制数组，也不执行输入验证或权限检查。

[SnapshotEntityState](../Runtime/Data/EntityState.cs)保存实体 ID、状态类型与 byte[]；[FrameSnapshotData](../Runtime/Data/FrameSnapshotData.cs)保存帧号、秒时间戳、快照种类、实体数组及自定义 payload。接受这些数组的构造函数保留引用；readonly struct 也不等于深度不可变。`EntityState[]` 兼容重载会转换为新的信封数组，与直接传 `SnapshotEntityState[]` 的路径不同。跨队列、异步或历史存储前，由调用者明确复制或转交所有权。

[CoordinatorPayloadCodec](../Runtime/Data/CoordinatorPayloadCodec.cs)直接使用 MemoryPack：

| API | 契约与失败行为 |
| --- | --- |
| `Encode<T>(in payload)` | 序列化并返回 byte[]；调用方提供可序列化类型，异常不被吞掉 |
| `Decode<T>(payload)` | null/空数组返回 default；其他数据直接反序列化 |
| `TryDecode<T>` | 仅对 null/空数组返回 false；非空畸形数据仍可能抛异常，不是无异常安全解析器 |
| `GetOpCode` | 读取 `[CoordinatorPayload]` 并缓存；缺失特性抛异常，null Type 抛 `ArgumentNullException` |
| `TryGetOpCode` | null Type 或无特性返回 false；不分配/登记全局唯一 opcode，也不检查冲突 |

`SnapshotEntityState.Create<T>` 未提供状态码时尝试读取特性，否则使用零；`TryGetPayload<T>` 只有在目标类型有特性时才比对 StateType，然后尝试解码。载荷上限、版本、类型白名单、认证与异常隔离需在接收边界完成。快照 DTO 本身不提供基线重建、回滚、导入或确认水位。

## 6. 迁移与排错

| 症状 | 处理 |
| --- | --- |
| 旧教程中的 SessionCoordinator/adapter 找不到 | 该实现已不在本包；使用当前项目会话装配与 SDK，勿从历史文档恢复一套重复循环 |
| 选择 Hybrid 却没有预测 | 检查 HostMode、派生策略和项目实现；配置选项不等于算法已经安装 |
| 远程端点“有效”却无法连接 | `IsValid` 只检查字段；检查具体 transport 的验证、拨号与认证 |
| 每次状态数持续增长 | 检查 caller-owned buffer 是否按业务需要清空，扩展采用追加语义 |
| TryDecode 仍抛异常或历史数据变化 | 分别处理非空坏数据异常与数组所有权；不要把 Try/readonly 当作安全或复制承诺 |

旧[Coordinator 设计](../Document/Coordinator会话协调模块开发设计文档.md)与 [ET 接入记录](../docs/ET-Integration-Guide.md)原路径保留，仅作历史背景，旧编译成功与状态表不是当前结果。

## 7. 验证范围

2026-11-25 核对上述源码，本包现有 .NET 契约测试 5/5 通过，第 2 节完整示例编译运行成功，属于限定 E3。现有测试重点覆盖输入、宿主模式派生策略和状态缓冲扩展；示例补充本地 payload 往返，不代表完整 host、网络、出生和恢复链都经过验证。

本批没有运行 Unity、独立 UPM 安装、真实连接、MOBA/Shooter 会话、帧同步重演或状态同步恢复验收，也没有新增 E4/E5 结论。当前包内没有会话实现，不能把配置与 codec 测试解释为完整会话可运行。
