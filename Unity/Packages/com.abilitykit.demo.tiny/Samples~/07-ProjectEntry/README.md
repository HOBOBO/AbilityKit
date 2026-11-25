# 07 项目入口

将 `TinyProjectLobbyEntry` 挂到自有大厅场景的 GameObject 上。
项目完成玩家认证后，使用有效的 `DemoMultiplayerLaunchRequest` 调用 `Enter(authenticatedLaunch)`。
将大厅场景和 `TinyDemoGameplayScene` 都加入 Build Settings。
从 Tiny 返回时会加载此组件提供的大厅场景。

示例通过包内的 `TinyProjectLaunch` 和 `TinyGameplayRoot` 处理连接、房间恢复和玩法；
身份认证仍由项目负责。
