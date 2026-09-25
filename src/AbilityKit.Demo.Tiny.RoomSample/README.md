# 02 Room 独立工程

本工程直接使用 Network SDK 的 Room 连接和 UPM `02-Room` 样例源码。它不引用 Tiny 规则、State 快照、FrameSync 或 Unity View。

先启动已注册 Tiny 服务端模块的 Orleans Host 与 Gateway，再从仓库根目录运行：

```powershell
dotnet run --project src/AbilityKit.Demo.Tiny.RoomSample -- 127.0.0.1 4058 my-unique-run
```

参数依次为 Gateway 主机、TCP 端口、可选账号前缀。工程创建两个独立连接，登录两个账号，建房、加入、准备、报告 Loading 完成，并确认同一战斗中有两个正确的玩家槽位。成功退出码为 0。仓库门禁 `./tools/verify-tiny-starter.ps1 -SkipUnity` 会启动隔离服务并写入 `room-sample.log`。
