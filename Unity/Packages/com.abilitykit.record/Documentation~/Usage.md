# Record 使用契约与限制

安装条件和最小示例见[包入口](../README.md)。

## 选择记录入口

| 要记录的内容 | 用法 |
| --- | --- |
| 按帧组织的自定义事件、调试或战斗日志 | 选 `RecordSession` 和约定好的 `RecordProfile`；按 `RecordTrackId` 取 writer，编码负载后用 `Append` 写入。用 `Serialize()` 导出，用 `TryLoad(...)` 成功加载后再取 reader。 |
| 按帧记录输入、快照并供回放读取 | 使用 `FrameRecordCodecs` 对应的 writer/reader，按需通过 `FrameRecordReplaySource` 查询。不要用 `RecordSession.TryLoad` 读取 FrameRecord 文件。 |

## 轨道与回放

- 给轨道和事件类型采用稳定且明确的名字，读写双方使用相同的 ID 派生约定和负载 codec。按消费者拆分轨道，不把调试日志当作权威输入。自定义轨道见 [Battle Log Sample](../Samples~/BattleLogSample/Runtime/BattleLogRecordSample.cs)。
- 同一帧可以有多条快照。需要完整读取时使用 `TryGetSnapshots`；兼容接口 `TryGetSnapshot` 只返回第一条。消费方负责将记录匹配到自身的恢复流程，读取成功不表示模拟已恢复。
