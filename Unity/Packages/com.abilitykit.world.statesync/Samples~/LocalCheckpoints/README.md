# 本地检查点

在 Unity Package Manager 中导入 `Local Checkpoints`，从本地宿主调用 `LocalCheckpointExample.Run()`。无需场景、传输、会话或 MOBA 配置；返回 `true` 表示以下条件都满足：帧 10 的位置成功恢复、超过两帧容量后帧 10 被淘汰、历史帧按升序保留。

示例使用包内 `EntityRollbackState` 保存本地状态，不读取网络载荷。实际项目应由宿主在模拟边界调用捕获和恢复，稳定管理实体 ID，不把这份记录当作权威状态或输入确认。
