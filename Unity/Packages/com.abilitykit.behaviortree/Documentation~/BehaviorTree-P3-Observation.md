# 行为树 P3 观测与离线调试

## 范围

P3 扩展既有 V2 观测、控制器、时间轴和布局能力，保持运行时行为树语义。编辑器仍通过 `IBtTreeDebugView` 主动拉取数据；运行时树不感知窗口、记录器或回放界面。

## 采样边界

`BtObservationSettings` 负责共享默认值和限制：

- 默认采样间隔：`0.2` 秒。
- 允许采样间隔：`0.01` 至 `10` 秒。
- 默认时间轴容量：`200` 个采样。
- 允许时间轴容量：`1` 至 `10000` 个采样。

`BtObservationController.SampleIntervalSeconds` 和 `TimelineCapacity` 可在运行时修改。非正值抛出异常；过大值会被限制，以保持编辑器内存增长有界。

## 记录 DTO

观测记录通过稳定 DTO 类序列化，不保存运行时或编辑器对象图：

- `BtObservationRecordingDto`
- `BtObservationSnapshotDto`
- `BtObservationNodeDto`
- `BtObservationBlackboardDto`
- `BtObservationStringMapEntryDto`

DTO 将枚举保存为整数、映射保存为已排序键值数组、黑板值保存为平行的基本类型数组，使 JSON 结构确定，避免依赖字典序列化。

## 核心 API

通过 `BtObservationRecording.ToJson`、`TimelineFromJson`、`SnapshotToJson`、`SnapshotFromJson`、`ExportToFile` 和 `ImportTimelineFromFile` 交换捕获数据。使用 `BtObservationOfflineReplay` 的 `Seek`、`StepPrevious`、`StepNext` 和 `JumpToLatest` 浏览导入的离线会话。

观测窗口通过工具栏提供入口：

- `Interval` 和 `Capacity`：实时采样设置。
- `Export Recording`：导出时间轴 JSON。
- `Import Replay`：加载离线回放。
- `Previous`、`Next` 和 `Latest`：回放浏览控制。

## 规模说明

P3 复用 GraphView 投影集合、覆盖层缓冲及轮询时的控制器注册表列表，减少重复状态投影。快照仍为不可变深拷贝，防止后续运行帧修改已导出记录及冻结或历史视图。

性能预算测试覆盖 100、500 和 1000 节点采样及大量黑板键，通过 `TestContext` 记录耗时，仅断言宽松上限，以发现意外的平方级退化，避免 CI 绑定过紧的绝对耗时。

## 诊断

`BtEditorDiagnostics.AnalyzeObservation` 报告观测采样数、容量、间隔、断开后保留历史的状态及最大容量风险。诊断保持信息或警告等级，因为观测设置不影响导出的行为树运行时语义。
