# 04 帧同步

`TinyFrameExample.Run()` 先预测一个空帧，再应用迟到的权威攻击。公开的 `TinyFrameSyncSession` 恢复前一状态、重演输入并返回 `Replayed`。若最终哈希与权威对局不同，示例会失败。

运行 Tiny Host 和 Gateway 后，可执行 `dotnet run --project src/AbilityKit.Demo.Tiny.FrameSample -- 127.0.0.1 4058 my-run`；也可用 `./tools/verify-tiny-starter.ps1 -SkipUnity` 启动隔离服务。项目编译此 UPM 源码并运行双客户端正式帧同步会话；查看 `frame-sample.log` 中的本地哈希、权威帧和回滚次数。`frame.log` 保留网络哈希收敛检查。
