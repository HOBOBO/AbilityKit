# AbilityKit Record

`com.abilitykit.record` 提供按帧记录事件、序列化和读取回放数据的组件，不负责选择世界时钟、推进权威模拟或恢复游戏状态。

## 安装与接入

从项目配置的包来源安装本包；Unity 版本及依赖见 [`package.json`](package.json)。首先运行[使用契约](Documentation~/Usage.md#纯内存完整示例)中的完整例子，预期写入、序列化并读回一条自定义事件，不需要场景或文件。

随包 [Record Delta Sample](Samples~/RecordDeltaSample/Runtime/DeltaRecordSample.cs) 和 [Battle Log Sample](Samples~/BattleLogSample/Runtime/BattleLogRecordSample.cs) 保留为参考；两者当前均缺少自定义轨道初始化，会在取 writer 处提前返回，不能直接作为跑通证明。原因及正确登记方式见使用契约。

- [使用契约与限制](Documentation~/Usage.md)：如何选用记录入口、组织轨道和读取快照。
- [文档索引](Documentation~/README.md)：当前使用入口与随包样例；纯内存完整例子见使用契约。
- [变更记录](CHANGELOG.md)；[历史设计入口](Document/README.md)。
