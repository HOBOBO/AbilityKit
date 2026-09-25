# Tiny Room + State 独立工程

本工程直接运行两个 Network SDK 客户端，使用 UPM 的 02 Room、03 State 样例源码和 Tiny Logic 包。`Program.cs` 是本章独立入口，不复用综合 TCP 客户端的入口。它不依赖 Starter、Unity View 或 FrameSync 客户端程序集。服务器仍需启动已注册 Tiny 模块的 Orleans Host 和 Gateway。

从仓库根目录运行：

```powershell
dotnet run --project src/AbilityKit.Demo.Tiny.StateSample -- 127.0.0.1 4058 my-unique-run
```

参数依次为 Gateway 主机、TCP 端口、可选账号前缀。服务端使用 `dev/local` 区域配置。工程先完成 02 的房间流程，再协商 State 能力、订阅并等待两端各自收到完整基线，最后提交攻击并比对同一权威帧的结果。`state-sample.log` 输出 `baseline=full`、帧号、客户端数和目标生命值。断线恢复由 06 章节及 `state.log`、`session-state.log` 验收。全部成功时退出码为 0，失败时为 1。

仓库门禁 `./tools/verify-tiny-starter.ps1 -SkipUnity` 会自动启动隔离服务并运行本工程；结果写入 `state-sample.log`。此工程验证的是无头网络与状态数据，不验证 Unity 画面。
