# AbilityKit World Record - 设计说明
 
 本文描述 record 模块的核心设计目标、职责边界与数据流。
 
 ## 设计目标
 
 - 对确定性友好：payload 以 `byte[]` 形式存储，事件以 `FrameIndex` 为索引。
 - 模块化：不同领域写入不同 track。
 - 可扩展：通过“字符串名称 -> `RecordEventType` + codec”的方式新增事件类别。
 - 易工具化：记录可以被序列化（当前为 JSON 容器），并可被回放。
 
 ## 核心数据模型
 
 - `RecordSession`
   - 高层 facade。
   - 持有 `RecordContainer`，并提供：
     - `TryGetWriter(RecordTrackId, out IEventTrackWriter)`
     - `TryGetReader(RecordTrackId, out IEventTrackReader)`
     - `Serialize()` / `TryLoad(byte[])`
 
 - `RecordContainer`
   - 可序列化的根对象。
   - 主要包含：
     - `Meta`（可选）
     - `Tracks`（集合）
 
 - `RecordTrack`
   - 由 `RecordTrackId` 标识。
   - 内部存储 `EventTrack`（按 `FrameIndex` 索引事件）。
 
 - `RecordEvent`
   - `{ FrameIndex Frame, RecordEventType EventType, byte[] Payload }`。
 
 - `RecordEventType`
   - 由字符串名称稳定派生出的整数 id（`RecordEventType.FromName`）。
   - 不应依赖“枚举顺序”；应将其视为 id。
 
 ## 数据流概览
 
 ### 写入路径
 
 ```mermaid
 flowchart LR
     A[Gameplay / Simulation] -->|per frame| B[Codec.Encode]
     B --> C[IEventTrackWriter Append]
     C --> D[RecordContainer.Tracks]
     D --> E[RecordSession Serialize]
     E --> F[(bytes / file)]
 ```
 
 ### 加载 + 回放路径
 
 ```mermaid
 flowchart LR
     F[(bytes / file)] --> A1[RecordSession TryLoad]
     A1 --> B1[IRecordTrackReaderFactory]
     B1 --> C1[IEventTrackReader TryGetEvents]
     C1 --> D1[Replay Controller / Manual Loop]
     D1 --> E1[IReplayEventHandler Handle]
     E1 --> G1[Codec.Decode]
     G1 --> H1[Debug UI / Re-simulation / Validation]
 ```
 
 ## Track 组织方式
 
 Track 是“单一目的的时间序列”。推荐约定：
 
 - 将确定性输入与调试日志分离。
 - 一个领域一个 track（例如 lockstep inputs、world snapshots、battle logs）。
 - 除非消费者总是一起消费，否则避免把不相关的 event type 混在同一 track。
 
 典型 track id（仅约定示例）：
 
 - `lockstep.inputs`
 - `world.snapshots`
 - `world.deltas`
 - `battle.log`
 
 ## 事件类别与 Codec
 
 一个事件类别通常由以下三部分定义：
 
 - 字符串名称（例如：`"snapshots.world_delta"`）
 - 由名称 hash 得到的 `RecordEventType`
 - 将领域结构体与 `byte[]` 相互转换的 codec
 
 内置类别见 `RecordEventNames` / `RecordEventTypes`。
 
 ### 内置 snapshot 与 delta
 
 - `snapshots.world_state`
   - 全量 snapshot。
 - `snapshots.world_delta`
   - 增量 / 变更集（delta）。
 
 它们是语义上不同的事件类型，但可以复用同一种 payload 形状（`WorldStateSnapshot`）。
 
 ## 同帧多 Snapshot
 
 某些场景需要同一帧记录多条 snapshot（例如多子系统录制）。
 
 `IFrameReplaySource` 提供：
 
 - `TryGetSnapshots(frame, out IReadOnlyList<WorldStateSnapshot>)`
 - `TryGetSnapshot(frame, out WorldStateSnapshot)`（兼容；返回第一条）
 
 需要完整数据的消费者应优先使用 `TryGetSnapshots`。
 
 ## 回放机制
 
 `BasicReplayController`：
 
 - 使用 `IReplayClock` 推进帧。
 - 每消耗一帧：
   - 调用 `IEventTrackReader.TryGetEvents(frame, out events)`
   - 将每条事件分发给 `IReplayEventHandler.Handle(in RecordEvent)`
 
 回放本身不依赖具体事件类型；事件类型的解释应放在 handler/codecs 中。
 
 ## 扩展示例：战斗技能效果日志
 
 为了 Debug 可视化与复盘分析，建议将战斗效果记录为独立 track（而不是 world snapshot）。
 
 推荐做法：
 
 - Track：`battle.log`
 - Event types（示例）：
   - `battle.skill.cast`
   - `battle.buff.add`
   - `battle.damage`
 - Payload 常见字段：
   - `actorId:int`
   - `instanceId:int`（自增唯一 id）
   - `code:int`（技能/buff/子弹等配置表 id）
 
 这些事件应在“效果最终落地”的位置写入（例如伤害扣血、buff 真正添加），而不是在输入提交阶段。
 
 可运行参考见 `Samples~/BattleLogSample`。
