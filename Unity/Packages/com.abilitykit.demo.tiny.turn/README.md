# Tiny 回合制（可选）

此包添加双人回合制对局，不改变主 Tiny 包。`Runtime/Logic/TinyTurnBattle.cs` 是唯一规则源；.NET Turn.Core 项目编译同一文件。

在服务器注册 `TinyTurnGameplayModule`。Unity 客户端将已认证的 `DemoMultiplayerLaunchRequest` 和返回场景传给 `TinyTurnProjectLaunch.Open(request, returnScene)`。场景负责创建或加入房间、准备与加载、状态快照投影、攻击、重连和返回导航。提交成功响应表示动作已入队；应等待权威快照确认回合结果。

在仓库运行 `tools/create-tiny-validation-project.ps1 -IncludeTurn`，生成包含此包及场景的隔离测试项目。`tools/verify-tiny-starter.ps1` 以批处理模式运行连接 Gateway 的 PlayMode 测试；`-SkipUnity` 仅覆盖 .NET 回合制客户端与服务器路径。

通过 Unity Package Manager 导入 `Samples~/10-TurnEntry`，查看项目自有大厅入口示例。完整无界面门禁还会启动两个独立的 `-Standalone -IncludeTurn` Unity 项目并连接同一 Gateway，检查两个客户端的权威结果和返回路径。`-FocusChapter 10` 运行较短的 .NET 与 TCP 章节检查。
