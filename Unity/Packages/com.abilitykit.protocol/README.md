# com.abilitykit.protocol — 序列化抽象（统一设计）

> AbilityKit 网络序列化的**唯一后端是 MemoryPack**。所有 wire 类型都是 `[MemoryPackable]` struct，字节兼容、跨平台一致。本文定义统一的序列化模型 —— 三层 API 按用途选，单一后端不混用。

## 统一后端：MemoryPack

| 项 | 说明 |
|---|---|
| **后端** | MemoryPack（唯一 —— 不混用 JSON/protobuf/手写二进制）|
| **wire 类型** | `[MemoryPackable] partial struct`，字段标 `[MemoryPackOrder(n)]` |
| **op-code 绑定** | `[ProtocolOpCode(opcode, direction, name)]` 标注 wire 类型 |
| **生成** | moba framesync 的 `Wire*` 类型 + `WireCustomBinary` 由 moba codegen 生成（`.g.cs`）|

## 三层 API（按用途选，不混用后端）

### 1. 直接 MemoryPack（热路径首选）
```csharp
byte[] bytes = MemoryPackSerializer.Serialize(value);
T value = MemoryPackSerializer.Deserialize<T>(bytes);
```
最快，无间接。战斗/快照/输入热路径用这个。

### 2. per-segment `Wire*Binary` facade（类型安全便捷封装）
```csharp
ArraySegment<byte> seg = WireRoomGatewayBinary.Serialize(in wireReq);     // room-gateway 段
T req = WireRoomGatewayBinary.Deserialize<T>(seg);                        // 泛型
// 或 typed overload（moba-framesync 段）：
var frameReq = WireCustomBinary.DeserializeSubmitFrameInputReq(payload);
```
- `WireRoomGatewayBinary`（`protocol.room`）—— 泛型 `Serialize<T>/Deserialize<T>` + 瞬态 buffer（`SerializeTransient`）+ `ArraySegment` 返回。
- `WireCustomBinary`（`protocol.moba`，codegen 生成）—— per-type typed overload（`Serialize(WireXxx)` / `DeserializeXxx()`）。
- **底层都是 MemoryPack 直调**（无 reflection）—— 字节与第 1 层完全一致。

### 3. pluggable 抽象（`WireSerializer` / `IWireSerializer`）—— 可替换后端
```csharp
byte[] bytes = WireSerializer.Serialize(value);   // → IWireSerializer.Current
T value = WireSerializer.Deserialize<T>(bytes);
```
- `protocol`（base）定义 `IWireSerializer` + 静态 `WireSerializer.Current`，并提供 `MemoryPackWireSerializer` 反射实现（reflection 找 MemoryPack DLL，Unity 友好）。
- 安装：`MemoryPackWireSerializerInstaller.InstallAsCurrent()`（启动时调一次）。
- **用途**：需要换序列化后端（测试 mock / alt format）时用。**不在热路径用**（reflection 间接开销）。

## 何时用哪层

| 场景 | 用 |
|------|---|
| 战斗/快照/输入热路径 | 第 1 层（`MemoryPackSerializer` 直调）或第 2 层（`Wire*Binary` facade）|
| 房间/会话 RPC | 第 2 层（`WireRoomGatewayBinary`）—— 有 typed overload + ArraySegment 便捷 |
| 需要可替换后端（测试/alt） | 第 3 层（`WireSerializer`）|
| 游戏状态 payload（需 delta/AOI/特殊编解码）| 游戏专属 codec（MemoryPack wrapper，如 `ShooterInputCodec`）|

## 新增一个 wire 类型的标准流程

1. 定义：
   ```csharp
   [ProtocolOpCode(YOUR_OPCODE, ProtocolDirection.ClientToServer, nameof(WireYourReq))]
   [MemoryPackable]
   public partial struct WireYourReq
   {
       [MemoryPackOrder(0)] public int Field1 { get; set; }
       [MemoryPackOrder(1)] public string Field2 { get; set; }
   }
   ```
2. 序列化：直接 `MemoryPackSerializer.Serialize(req)` 或经 segment facade。**不需要手写 codec** —— MemoryPack 自动处理。
3. （仅当载荷需要特殊处理，如 packed/pure-state delta、AOI 兴趣裁剪）才写游戏专属 codec。

## 不要

- ❌ 不要混用不同后端（全部 MemoryPack）。
- ❌ 不要在热路径用 `WireSerializer`（reflection）—— 用 `MemoryPackSerializer` 直调或 `Wire*Binary`。
- ❌ 不要手写非 MemoryPack 序列化（除非有 wire-compat 的明确理由，并标注）。
- ❌ 不要绕过 `[ProtocolOpCode]` 属性直接硬编码 op-code 数字 —— 用 `ProtocolMessageDescriptor<T>.RequireOpCode(direction)`。

## 相关包

- `com.abilitykit.protocol.memorypack` —— `MemoryPackWireSerializer` + `MemoryPackWireSerializerInstaller`（MemoryPack 实现 + 安装器）
- `com.abilitykit.protocol.room` —— room-gateway wire 类型 + `WireRoomGatewayBinary`
- `com.abilitykit.protocol.moba` / `.shooter` —— 各 demo 的 wire 类型 + codec
- 接入清单 → `Docs/design/07-NetworkSynchronization/07-MultiplayerSdkIntegrationGuide.md`
