# AbilityKit Record

`com.abilitykit.record` 提供按帧记录事件、序列化和读取回放数据的组件，不负责选择世界时钟、推进权威模拟或恢复游戏状态。

## 安装与接入

从项目配置的包来源安装本包；Unity 版本及依赖见 [`package.json`](package.json)。在包的 Samples 中导入 **Record Delta Sample**，将 `DeltaRecordSample` 挂到对象上并调用 `Run()`；它演示用本地生成的增量事件走通写入、序列化和读取。[示例源码](Samples~/RecordDeltaSample/Runtime/DeltaRecordSample.cs)

- [使用契约与限制](Documentation~/Usage.md)：如何选用记录入口、组织轨道和读取快照。
- [变更记录](CHANGELOG.md)；[历史设计入口](Document/README.md)。
