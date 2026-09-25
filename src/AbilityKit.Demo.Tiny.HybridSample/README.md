# 05 Hybrid 独立工程

本工程共编译 UPM `05-Hybrid/TinyHybridExample.cs`，先验证本地预测、权威确认和迟到远端输入重放并得到确定性哈希，再通过正式 `TinyBattleSession` 运行两个客户端的 Hybrid 联机路径。网络与会话驱动引用 `AbilityKit.Demo.Tiny.ClientHarness` 类库，不复制 Tiny 的正式 Runtime。

先启动已注册 Tiny 模块的 Orleans Host 和 Gateway，再从仓库根目录运行：

```powershell
dotnet run --project src/AbilityKit.Demo.Tiny.HybridSample -- 127.0.0.1 4058 my-unique-run
```

成功日志包含权威帧号、`chapterHash`、本地主控预测计数及远端回滚计数。仓库门禁 `./tools/verify-tiny-starter.ps1 -SkipUnity` 会自动启动隔离服务并保存 `hybrid-sample.log`。断线恢复由 06 的综合验收负责。
