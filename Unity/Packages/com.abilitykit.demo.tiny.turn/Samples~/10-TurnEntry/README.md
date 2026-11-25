# 10 回合制项目入口

将 `TinyTurnProjectLobbyEntry` 挂到自有大厅场景的 GameObject 上。
将已认证的 `DemoMultiplayerLaunchRequest` 传给 `Enter`，并将大厅和
`TinyTurnGameplayScene` 都加入 Build Settings。Turn 包负责房间、状态快照和动作流程；
项目负责登录与导航。

单独安装 `com.abilitykit.demo.tiny.turn`，主 Tiny 包不依赖它。
