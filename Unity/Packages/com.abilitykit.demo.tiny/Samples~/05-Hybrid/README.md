# 05 混合同步

`TinyHybridExample.Run()` 在确认前预测本地攻击，确认匹配的权威帧，再重演迟到的远端攻击。这是混合同步模式中的本地规则与回滚部分；网络会话和周期性完整快照由正式 Tiny 运行时提供。

运行 Tiny Host 和 Gateway 后，可执行 `dotnet run --project src/AbilityKit.Demo.Tiny.HybridSample -- 127.0.0.1 4058 my-run`；也可用 `./tools/verify-tiny-starter.ps1 -SkipUnity` 启动隔离服务。项目编译此 UPM 源码并运行双客户端正式混合同步会话；查看 `hybrid-sample.log` 中的本地哈希、权威帧、预测和远端回滚次数。`hybrid.log` 保留网络哈希收敛检查。
