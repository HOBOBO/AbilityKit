# 本地快照历史

通过 Unity Package Manager 导入 `Local Snapshot History`。调用 `LocalSnapshotHistoryExample.Run()` 使用默认稀疏后端，调用 `Run(useRingStorage: true)` 使用 Core 的环形后端；两个入口验证相同的容量、重复帧覆盖、帧零与副本隔离契约。

这个例子保存的是宿主已经完成提交的快照元数据，不执行导入、固定基线合成或局部预测。使用 `TryGet` 区分帧号 `0` 与缺记录，不把查询结果当作权威确认。需要本地实体捕获和恢复时，另见 [本地检查点](../LocalCheckpoints/README.md)。
