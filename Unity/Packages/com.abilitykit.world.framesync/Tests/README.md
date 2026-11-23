# 包级验收

仓库的 `src/AbilityKit.World.FrameSync.Tests` 直接引用 Runtime 源码，并链接包内 `Samples~/LocalFrameCommands/LocalFrameCommandExample.cs`，避免维护两份示例。`LocalFrameCommandExampleTests` 验证示例及缺帧读结果的隔离；`FrameCommandBufferTests` 验证并发读取、键替换、排序及回收。

在已备齐本地依赖时，从仓库根目录执行：

```powershell
dotnet test src/AbilityKit.World.FrameSync.Tests/AbilityKit.World.FrameSync.Tests.csproj --no-restore --filter 'FullyQualifiedName~LocalFrameCommandExampleTests|FullyQualifiedName~FrameCommandBufferTests'
```

本地验收只使用当前仓库内存对象，不抓包、不解析网络载荷、不查询线上水位、不检查外部进程。Unity Editor 编译与完整回滚验收不在该命令范围内。
