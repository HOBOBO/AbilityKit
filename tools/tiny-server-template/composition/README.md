# 自定义 Host 装配

此项目基于已发布的 `../host/*.dll` 编译，不引用 AbilityKit 源码检出目录。
安装 .NET 10 SDK 后即可编辑和构建，再使用相同的 Orleans 配置
启动已发布的 `../gateway/AbilityKit.Orleans.Gateway.dll`。

```powershell
dotnet build TinyCustomHost.csproj
dotnet run --project TinyCustomHost.csproj
```

在源码仓库中，`tools/verify-tiny-starter.ps1 -FocusChapter 10
-SkipUnity -ServerBundlePath <bundle> -UseCompositionHost` 会使用已发布的 Gateway
和真实 Tiny Turn TCP 客户端验证此 Host。

将自有玩法程序集引用加入 `TinyCustomHost.csproj`，然后在 `ConfigureGameplay` 中调用
`catalog.WithModule(MyGameplayModule.Create())`。模块负责自己的房间描述、战斗运行时适配器、
世界蓝图和同步配置。Tiny 及可选的 Turn 继续注册，作为可运行的示例。
发布的二进制是统一的版本单元：升级时应一起替换 Host、Gateway、协议和玩法程序集。
