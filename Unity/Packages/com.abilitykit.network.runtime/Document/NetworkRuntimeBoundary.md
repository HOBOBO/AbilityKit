# com.abilitykit.network.runtime：通用网络协议层职责边界

## 目标
本包应定位为“通用网络运行时/协议层”，负责把**字节流/连接**转换为**可分发的网络消息**，并提供：

- 连接管理、心跳、重连、断线
- 收包/拆包/组包
- 线程模型（例如 DedicatedThreadDispatcher）
- 通用消息路由（messageId/opCode -> handler）
- 序列化/反序列化（protobuf/flatbuffers/自定义二进制等）

它不应包含：
- 具体业务世界（world）的语义
- 世界快照（snapshot）解码与处理策略
- “某个游戏”的 handler 注册表

## 与 world.snapshot 的边界
- `network.runtime` 负责：
  - 识别一条网络消息的类型
  - 将其解码为某个“消息 DTO / message envelope”（领域无关）
  - 按 messageId/opCode 路由给 handler

- `world.snapshot` 负责：
  - 在你已经拿到 `WorldStateSnapshot`（`opCode + payload`）之后
  - 如何把它 decode 为强类型 payload
  - 如何以 dispatcher/pipeline/cmdhandler 方式分发给处理器

换句话说：
- 网络层的 `opCode` 是“网络消息类型”概念
- 快照层的 `opCode` 是“世界快照类型”概念

在项目中两者可能使用同一个整数值域，但**语义层级不同**，不建议把它们混成同一层。

## 推荐的适配方式（把网络消息转成快照 envelope）
建议在业务层（例如 battle/session feature）完成适配：

1. `network.runtime` 收到网络消息
2. 业务层判断：该消息是否携带 world snapshot
3. 若是：构造 `ISnapshotEnvelope`（例如 `FramePacket`）
4. 调用 `FrameSnapshotDispatcher.Feed(envelope)`

优点：
- `network.runtime` 不依赖任何 world 语义
- `world.snapshot` 不依赖任何 transport
- demo/business 可以自由选择如何映射网络消息到 world snapshot

## 依赖方向建议
- `network.runtime` 可以被 server 与 Unity client 同时复用
- `network.runtime` 与 `world.snapshot` 之间建议保持“上层 glue 代码”连接，而非互相硬依赖

若未来确实需要一层更通用的“网络消息路由”抽象（不仅快照），可考虑新增一个更轻量的 routing 包（例如 `com.abilitykit.network.routing` 或 `com.abilitykit.protocol.routing`），但这应优先通过最小改造验证必要性。
