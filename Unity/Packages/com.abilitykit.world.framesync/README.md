# AbilityKit World FrameSync

本包提供帧编号、固定步进时间、按帧命令存储及可选的回滚与重演组件。宿主负责权威输入来源、帧就绪判定、玩法执行和异常恢复；本包不提供联机会话、状态同步基线或表现插值。

**最小接入**：从 [离线帧命令示例](Samples~/LocalFrameCommands/README.md) 开始。由宿主标记哪一帧已齐备，读取该帧命令后固定步进；无命令但已齐备的帧也要推进，未齐备的帧不能当作空帧。

| 目录 | 内容 |
| --- | --- |
| `Runtime/FrameSync` | 帧驱动、时间和命令缓冲 |
| `Runtime/FrameSync/Rollback` | 检查点、恢复顺序、命令日志和预测协调 |
| `Runtime/Host` | 输入提交与结算元数据边界 |
| [`Documentation~/`](Documentation~/README.md) | 接入、默认值及扩展边界 |
| [`Tests/`](Tests/README.md) | 本地类型化测试入口 |
| [`Document/`](Document/定点帧同步接入指南.md) | 保留旧文档路径 |

直接代码依赖 `AbilityKit.Core`、`AbilityKit.Deterministic`、`AbilityKit.World.DI` 和 `MemoryPack`，Unity 清单另声明 `Unsafe` 兼容依赖。Unity 最低版本见 `package.json`；仓库的 `.NET` 镜像使用同一份 Runtime 源码，以 `net10.0` 验证。本批只验收本地定向路径，不代表 Unity 或整包已完成发布验收。变更见 [CHANGELOG](CHANGELOG.md)。
