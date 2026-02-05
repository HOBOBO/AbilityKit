# com.abilitykit.world.snapshot：快照路由/解码层职责边界

## 目标
本包旨在提供**与网络传输无关**的“世界快照（World Snapshot）”处理框架，使上层业务可以用一致的方式：

- 以 `opCode` 为键注册解码器（decoder）
- 将快照解码为强类型 payload
- 以事件/管线（pipeline）方式将 payload 分发给处理器（handler）

本包不负责：连接管理、可靠性、重连、线程模型、消息拆包/组包、非快照协议等“通用网络协议层”问题。

## 核心概念
- `WorldStateSnapshot`
  - 代表一条“世界快照”的最小数据：`OpCode + Payload(byte[])`。

- `ISnapshotEnvelope`
  - 为快照附加上下文信息（例如 `WorldId`）。

- `FramePacket`
  - `ISnapshotEnvelope` 的一种实现，常用于帧同步/回放场景：
    - `WorldId`
    - `FrameIndex`
    - `Inputs`
    - `Snapshot?`

## 路由/解码基础设施
- `ISnapshotDecoderRegistry`
  - 注册 `opCode -> decoder<T>`。

- `FrameSnapshotDispatcher`
  - 通过 `Feed(ISnapshotEnvelope envelope)` 接收 envelope。
  - 若 envelope 中包含 `WorldStateSnapshot`，则按 `opCode` 找到对应 decoder 并分发。
  - **重要**：dispatcher 是 session/transport agnostic（不订阅任何网络事件）；它只是一个纯粹的“路由器”。

- `SnapshotPipeline`
  - 在 dispatcher 的基础上提供“有序 stage”机制。
  - 适合需要先 decode，再按多个阶段（order）处理的情况。

- `SnapshotCmdHandler`
  - 在 dispatcher 上注册“命令型 handler”（本质也是订阅某个 `opCode` 的 payload）。

## 推荐依赖方向（避免耦合）
建议保持以下单向依赖：

- 业务/示例（demo/game）
  - 依赖 `com.abilitykit.network.runtime`（网络层）
  - 依赖 `com.abilitykit.world.snapshot`（快照解码/路由层）

- `com.abilitykit.world.snapshot`
  - 不依赖 `com.abilitykit.network.runtime`
  - 原因：快照处理应可运行在 server/headless 环境，且不绑死到特定 transport。

- `com.abilitykit.network.runtime`
  - 不依赖快照语义（尽量不直接依赖 world.snapshot）。
  - 若需要产出快照 envelope，建议由上层（例如 battle/session feature）完成“网络消息 -> 快照 envelope”适配。

## 与 demo 的对接方式（示例）
推荐的对接点是：**在上层业务收到网络帧/消息后，构造 envelope 并 feed 给 dispatcher**。

例如：

- 网络层（或 battle logic session）触发 `OnFrame(FramePacket packet)`
- 业务层在回调内调用：
  - `_snapshots.Feed(packet)`

这样做的好处：
- `world.snapshot` 不需要知道“谁负责收包/如何收包”
- 网络层也不需要知道“某个 opCode 的 payload 怎么 decode”

## 非目标（明确不做的事情）
- 不处理 socket/kcp/websocket 的连接与收发
- 不处理网络消息的通用路由（message id -> handler）
- 不规定序列化格式（protobuf/flatbuffers/自定义二进制均可），只要求上层提供 decoder
