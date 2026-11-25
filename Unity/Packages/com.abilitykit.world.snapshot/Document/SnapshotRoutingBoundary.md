# com.abilitykit.world.snapshot：快照路由/解码层职责边界

> 部分核校（2026-11-25）：保留原路径，已修正依赖、快照模型和项目适配器归属。当前注册、顺序、所有权与失败契约见[使用指南](../Documentation~/Usage.md)，本页不作为完整恢复能力证明。

## 目标

本包提供**与网络传输无关**的"世界快照（World Snapshot）"处理框架：

- 以 `opCode` 为键注册解码器（decoder）
- 将快照字节流解码为强类型 payload
- 以管线（pipeline）方式将 payload 分发给处理器（handler）

本包**不负责**：连接管理、可靠性、重连、线程模型、消息拆包/组包、非快照协议等"通用网络协议层"问题。

## 核心类型

| 类型 | 命名空间 | 说明 |
|------|----------|------|
| `FrameSnapshotDispatcher` | `AbilityKit.Core.Snapshots.Routing` | 按 opCode 分发快照 payload |
| `SnapshotPipeline` | `AbilityKit.Core.Snapshots.Routing` | 有序阶段管线，按 order 执行 |
| `SnapshotCmdHandler` | `AbilityKit.Core.Snapshots.Routing` | 命令型处理器 |
| `ISnapshotDecoderRegistry` | `AbilityKit.Core.Snapshots.Routing` | decoder 注册接口 |
| `ISnapshotPipelineStageRegistry` | `AbilityKit.Core.Snapshots.Routing` | 管线阶段注册接口 |
| `ISnapshotEnvelope` | `AbilityKit.Ability.Host` | 快照信封接口（来自 `world.networkfragments`） |

## 架构约束

直接依赖以 [package.json](../package.json) 为准；本包不直接声明 Network.Runtime，但有经依赖包到网络运行时的传递依赖，不能称为无网络依赖闭包：

| 包 | 依赖 | 说明 |
|----|------|------|
| `world.snapshot` | `core`, `host`, `world.di`, `framesync`, `networkfragments` | 路由/解码基础设施 |
| `world.statesync` | `snapshot`, `core`, `MemoryPack`, `Unsafe` | 本地历史、检查点及可选预测原语，不负责在线完整恢复 |
| `world.networkfragments` | `core`, `world.di`, `framesync`, `host`, `Network.Runtime` | 帧数据结构 |
| `host` | `core`, `world.di`, `framesync`, `Network.Runtime` | Host 框架、WorldStateSnapshot |

## 快照类型归属

### 通用快照（`host` 包）

`WorldStateSnapshot`（业务快照 opCode + payload 容器）定义在 `AbilityKit.Ability.Host`（`com.abilitykit.host` 包）。

### 业务快照（`world.statesync` 包）

`AbilityKit.Ability.StateSync.Snapshot.WorldStateSnapshot` 是本地历史使用的独立元数据模型，不是 Host 同名容器的完整实现，也不包含全部业务实体检查点。完整业务状态和导入契约由所属世界/会话定义，不能仅凭类名或全量标记推导恢复资格。

## 与 networkfragments 的关系

```
网络层收到帧数据
  → 构造 FramePacket（来自 networkfragments）
  → 项目适配器校验作用域并选择输入和快照路线
  → FrameSnapshotDispatcher.Feed()（来自 world.snapshot）
  → 路由到具体 handler
```

`FramePacket` 自身实现 `ISnapshotEnvelope`。具体 `FramePacketNetAdapter` 已在项目接入层，例如 MOBA 客户端会话，不属于本包或 `host.extension/Session` 的通用默认装配；由项目决定何时喂入 dispatcher。

`snapshot` 包不知道"谁负责收包/如何收包"，`networkfragments` 不知道"某个 opCode 的 payload 怎么 decode"。

## 非目标（明确不做的事情）

- 不处理 socket/kcp/websocket 的连接与收发
- 不处理网络消息的通用路由（message id -> handler）
- 不规定序列化格式（protobuf/flatbuffers/自定义二进制均可），只要求上层提供 decoder
- 不负责帧缓冲、抖动处理（由宿主选择缓冲和适配策略）
