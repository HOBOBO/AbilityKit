# Tiny 服务器发布包

此发布包包含已发布的 .NET 10 Host 和 Gateway 应用，运行时不依赖源码检出目录。
在目标机器安装 .NET 10 ASP.NET Core 运行时，然后从各自目录先启动 Host，再启动 Gateway：

```powershell
cd host
dotnet AbilityKit.Orleans.Host.dll
```

```powershell
cd gateway
dotnet AbilityKit.Orleans.Gateway.dll
```

Host 注册 Tiny 状态同步、帧同步和混合同步，默认也注册 Tiny Turn。
设置 `AbilityKit__Tiny__EnableTurn=false` 可禁用 Turn，而无需改变共享 Tiny 战斗规则。
两个应用使用本发布包中配套的规则和协议程序集；不要单独替换某个 DLL。
`bundle.json` 记录房间协议和两个 Tiny 规则程序集的 SHA-256 哈希。
发布工具还会检查 Host 与 Gateway 使用的房间协议二进制是否完全一致。

`composition/` 目录包含可独立编译的 Host 项目，通过明确的 `ConfigureGameplay` 注册点
接入项目自有模块。它基于已发布的 DLL 构建，无需访问源码检出目录。

本地默认 Orleans 网关端口为 30000，客户端 TCP Gateway 端口为 4000，HTTP 端口为 5001。
可通过 `AbilityKit__Orleans__*`、`AbilityKit__Gateway__Tcp__Port`、`TcpGateway__Port` 和
`AbilityKit__Gateway__Http__Port` 环境变量覆盖。用于本地开发以外的环境前，
应配置持久化存储和公网访问。

Unity 消费项目通过 `tools/create-tiny-validation-project.ps1 -Standalone -IncludeTurn`
单独生成。在源码仓库运行 `tools/verify-tiny-starter.ps1 -FocusChapter 10 -SkipUnity
-ServerBundlePath <bundle>`，可用真实 TCP 回合制客户端示例检查已发布服务器。
添加 `-UseCompositionHost` 后，同一示例将连接编译后的 `composition/TinyCustomHost`。
