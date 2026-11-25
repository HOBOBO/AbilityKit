# AbilityKit World Snapshot

`com.abilitykit.world.snapshot` 提供与传输无关的快照解码、类型化派发和有序处理管线。输入是调用方交付的 `ISnapshotEnvelope`，不是 socket 字节流；路由成功不等于权威状态已导入或游标已提交。

## 安装与首次使用

从项目配置的包来源安装本包；版本、Unity 最低版本和直接依赖以 [package.json](package.json) 为准。本包不自带具体玩法 decoder。

1. 为快照操作码注册 decoder，明确目标 payload 类型。
2. 注册类型化订阅或 pipeline stage，并保存注册令牌。
3. 由宿主串行调用 `FrameSnapshotDispatcher.Feed`；退出时撤销订阅，再释放装配实例。

[使用指南](Documentation~/Usage.md)提供可在本地构造信封的完整示例，以及重复注册、异常与释放边界。

## 文档与限制

- [文档索引](Documentation~/README.md)
- [原职责边界入口](Document/SnapshotRoutingBoundary.md)：保留旧路径，当前使用契约转到新指南。
- [变更记录](CHANGELOG.md)

本包不校验网络协议版本、会话代际或固定基线，不创建实体，不选择帧同步或状态同步模式。世界恢复资格和权威提交由调用方负责。
