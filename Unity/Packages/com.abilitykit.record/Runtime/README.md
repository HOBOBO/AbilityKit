# AbilityKit World Record（Runtime）

本包提供一套轻量、对确定性友好的记录容器与回放辅助能力。

主要用于记录按帧（`FrameIndex`）索引的事件，例如：

- Lockstep 输入
- 状态哈希采样
- 世界快照（全量）
- 世界增量 / 变更集（delta/change-set）
- 自定义的调试/回放事件流（例如：战斗技能效果）

## 核心概念

- `RecordSession`
  - 持有 `RecordContainer`，并提供 `TryGetWriter` / `TryGetReader`。
  - 提供 `Serialize()` / `TryLoad(byte[])`。
- `RecordContainer`
  - 可序列化的根对象，包含多个 `RecordTrack`。
- `RecordTrackId`
  - Track 标识，通常通过 `RecordTrackId.FromName("...")` 由名称派生。
- `IEventTrackWriter` / `IEventTrackReader`
  - 以 `FrameIndex` 为索引追加/读取 `RecordEvent`。
- `RecordEvent`
  - 单条事件：`{ FrameIndex, RecordEventType, byte[] Payload }`。
- `RecordEventType`
  - 由字符串名称稳定派生出的 int id（`RecordEventType.FromName`）。
- `BinaryObjectCodec`
  - 内置事件 codec 使用它对 payload struct 做编解码（配合 `[BinaryMember]`）。

## 内置事件类别

字符串常量位于 `RecordEventNames`，其 hash 后的常量位于 `RecordEventTypes`。

- `inputs.command`
- `state_hash.sample`
- `snapshots.world_state`
- `snapshots.world_delta`

对应的 codec 位于 `Runtime/Record/Adapters/EventCodecs`。

## Snapshot 说明

`IFrameReplaySource` 支持“同一帧多条 snapshot”场景：

- `TryGetSnapshots(FrameIndex frame, out IReadOnlyList<WorldStateSnapshot> snapshots)`
- `TryGetSnapshot(...)` 仍保留用于向后兼容，并返回第一条 snapshot。

## 快速上手（write -> serialize -> load -> replay）

典型使用流程：

1. 使用 `RecordProfile`、serializer、track factory 创建 `RecordSession`。
2. `TryGetWriter(trackId, out writer)` 获取 track writer。
3. 每帧编码并追加事件。
4. 调用 `Serialize()` 得到 bytes。
5. 新建另一个 `RecordSession` 并调用 `TryLoad(bytes)`。
6. 获取 track reader，使用 `BasicReplayController` 回放（或手动读取）。

可运行示例请见 samples。

## Samples

Samples 位于 `Samples~`，并使用独立 `.asmdef` 且 `autoReferenced=false`，以避免污染主程序集。

- `Record Delta Sample`
  - 记录并回放 `snapshots.world_delta`。
- `Battle Log Sample`
  - 演示将战斗技能效果作为自定义事件类型写入独立 track 并回放。
