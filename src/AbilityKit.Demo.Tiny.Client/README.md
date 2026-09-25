# Tiny TCP 验收客户端

仓库根目录的 `./tools/verify-tiny-starter.ps1 -SkipUnity` 会自动启动隔离端口上的 Host/Gateway，依次执行 State、Frame、Hybrid 双客户端 TCP 验收，并在结束时停止本次启动的服务。完整命令去掉 `-SkipUnity`，还会生成独立 Unity 工程并执行 Tiny EditMode 测试。测试日志和结果保存在 `local/Logs/tiny-acceptance-<时间戳>/`。

使用两个独立的 Network SDK 连接对本地 Orleans Gateway 执行真实网络验收。客户端创建 Tiny 房间、加入并准备两名玩家、完成分阶段 Loading、订阅同步、提交移动和攻击输入，最后让一名玩家重连并验证全量快照恢复。State 路径与独立 `AbilityKit.Demo.Tiny.StateSample` 工程共编译同一份程序和 UPM 02/03 样例源码。

Gateway 需要运行包含 Tiny 服务端模块与 Room 帧同步协议的版本。命令格式：

```powershell
dotnet run --project src/AbilityKit.Demo.Tiny.Client -- <主机> <Gateway端口> [账号前缀] [state|frame|hybrid|session-state|session-frame|session-hybrid]
```

例如：

```powershell
dotnet run --project src/AbilityKit.Demo.Tiny.Client -- 127.0.0.1 4057 tiny-check state
dotnet run --project src/AbilityKit.Demo.Tiny.Client -- 127.0.0.1 4057 tiny-check-frame frame
dotnet run --project src/AbilityKit.Demo.Tiny.Client -- 127.0.0.1 4057 tiny-check-hybrid hybrid
```

省略模式时默认使用 `state`。State 比对两端的权威状态；Frame 验证权威输入导致的回滚重放；Hybrid 验证本地输入在发送前完成预测、之后被权威帧确认，并验证远端回滚。交互式 Unity 接入位于 `com.abilitykit.demo.tiny`，本项目只承担无头网络验收。
