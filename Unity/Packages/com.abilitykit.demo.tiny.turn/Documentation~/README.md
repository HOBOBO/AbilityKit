# Tiny Turn 使用指南

> 文档类型：使用指南
> 适用范围：`com.abilitykit.demo.tiny.turn` 0.0.1；Unity 与依赖声明见 [package.json](../package.json)。
> 核校状态：部分核校；已对照入口、场景和客户端行动流程，未复验独立安装、Unity 或 Gateway 联机。
> 事实基线：2026-11-27

## 前置条件

- 项目须能解析 `package.json` 中的依赖；本包不要求安装实时 Tiny 包或 Starter。
- 需要可连接的 Room Gateway，以及在 Host 上注册的 `TinyTurnGameplayModule`（玩法 ID：`tiny-turn`）。服务端不随本 Unity 包交付；实时 Tiny 的模块不能代替它。
- 将本包的 [`TinyTurnGameplayScene`](../Scenes/TinyTurnGameplayScene.unity) 和项目大厅场景加入 Build Settings。项目负责登录、保管令牌、提供 Gateway 地址与返回场景。

## 进场与验收

1. 从 Package Manager 导入 [`10 Turn Project Entry`](../Samples~/10-TurnEntry/README.md)，把 `TinyTurnProjectLobbyEntry` 挂到项目大厅对象上。
2. 登录成功后将 `DemoMultiplayerLaunchRequest` 传给 `Enter`；样例调用 [`TinyTurnProjectLaunch.Open(launch, returnScene)`](../Runtime/View/TinyTurnProjectLaunch.cs)。场景消费一次性启动请求，返回时加载项目指定的大厅。
3. 两名玩家连接同一 Gateway，一端建房、另一端用房间号加入；双方准备后由房主开始。等到权威状态允许行动，轮流攻击并观察 HP、轮次与胜者，最后验证返回大厅。

## 行动与排错

[`TinyTurnSession`](../Runtime/View/TinyTurnSession.cs) 使用 State 模板的权威快照显示轮次与胜者。提交成功只表示请求已入队，不表示行动已经结算；等待权威快照推进后再行动。没有动作权限时先检查当前快照中的回合归属与提交状态。

无法进场时先检查请求是否含有效会话、账号和地址，场景是否已加入 Build Settings；房间或战斗无法加载时，检查 Host 是否为该 Gateway 注册了 `tiny-turn`、客户端规则和 Room 协议是否与服务端匹配。
