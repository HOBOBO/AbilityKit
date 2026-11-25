# 06 恢复

`TinyRecoveryExample.Run()` 主动耗尽帧历史，观察 `NeedsFullSnapshot`，恢复权威状态，并验证帧预测恢复后哈希一致。它只使用公开的 Tiny 规则和帧同步 API。

Unity 战斗会话还会在超时和空房间恢复后保留待处理的创建命令 ID。使用原同步模式重试，即使首次响应丢失，也会定位同一个服务器房间。恢复到活动房间后会清除待处理的创建尝试；已关闭房间会拒绝重放其旧创建 ID。

从仓库根运行 `dotnet run --project src/AbilityKit.Demo.Tiny.ChapterSamples` 可执行可导入示例。独立的 `RecoverySample` 无需服务器即可分别运行 `history`、`overflow` 或 `mismatch`。运行 `./tools/verify-tiny-starter.ps1 -SkipUnity` 可验证完整章节：`recovery-sample.log` 记录三类本地故障，以及双客户端断线和完整恢复。溢出与不匹配驱动复用正式房间及 Tiny 复制源码；断线驱动使用正式战斗会话。
