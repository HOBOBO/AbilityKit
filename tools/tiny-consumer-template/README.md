# Tiny 独立消费项目

本项目由 `tools/create-tiny-validation-project.ps1 -Standalone` 生成。
项目内嵌 Tiny 的完整包依赖，包含自己的 `ConsumerLobby` 场景。
`TinyConsumerLobby.Enter(authenticatedLaunch)` 是项目调用 `TinyProjectLaunch.Open` 的入口。
项目不包含 Starter 包或场景。OpenUPM 通过
`org.nuget.system.runtime.compilerservices.unsafe@5.0.0` 提供 MemoryPack 所需的 Unsafe 程序集；
本项目的 `Assets` 中没有从源项目复制的 DLL。

此测试项目不执行身份认证。实际项目应从自己的登录流程取得有效的
`DemoMultiplayerLaunchRequest`，再将其传给 `Enter`。
服务器必须注册 `TinyServerGameplayModule.Create()`，并提供 Tiny 使用的房间网关操作。
需要可迁移的本地服务器时，用 `tools/publish-tiny-server.ps1 -OutputPath <directory>`
生成 `host/` 和 `gateway/`。发布的 Host 默认注册 Tiny Turn；
设置 `AbilityKit__Tiny__EnableTurn=false` 可禁用它。
包内 `Documentation~/IntegrationGuide.md` 说明了安装和项目接入步骤，不要求使用本仓库脚本。

`TinyConsumer.PlayMode.Tests` 程序集在无界面的 Unity 中检查大厅到 Tiny 再返回大厅的场景路径。
它使用测试令牌和不可用的本地端口，无需运行服务器即可验证场景装配。

`TinyConsumer.Network.PlayMode.Tests` 仅在验证脚本启动的 Gateway 运行时执行。
它使用真实场景根节点和第二个网络会话，检查状态同步、帧同步、混合同步的玩法与恢复。
结果写入 `tiny-consumer-network.json`；无需 Unity 窗口或截图。

使用 `-Standalone -IncludeTurn` 时，生成的项目还包含 `ConsumerTurnLobby` 和可选的 Turn 包。
其 `TinyConsumerTurnLobby.Enter(authenticatedLaunch)` 调用 `TinyTurnProjectLaunch.Open`；
`TinyConsumer.Turn.Network.PlayMode.Tests` 检查真实 Gateway 对局及返回路径。
默认独立项目不包含此大厅，也不依赖 Turn。

完整验证门禁还会为混合同步、帧同步、状态同步和回合制分别启动独立的房主与访客无界面 Unity 进程，
每个进程使用各自生成的项目和 `Library`。各 `unity-cross-{Mode}` 目录包含每个进程的 XML、
日志和 JSON，用于证明共享房间、权威结果及返回各自大厅的路径。
混合同步检查确认前的预测；帧同步和混合同步访客在重新进入场景后恢复基线。
这些检查不评估渲染画面质量。
