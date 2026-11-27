# Tiny 项目接入指南

> 文档类型：使用指南
> 适用范围：`com.abilitykit.demo.tiny` 0.0.1；Unity 和依赖声明见 [package.json](../package.json)。
> 核校状态：部分核校；已对照包声明、项目入口、场景和三种模式的客户端边界；无 Starter 的进场与返回通过 Unity PlayMode，未复验独立注册表安装或 Gateway 联机。
> 事实基线：2026-11-27（仅限上述章节）

## 依赖与边界

- 安装本包时解析 `package.json` 的依赖闭包。独立 Logic 包提供确定性规则，由 Unity 与 .NET 共用；不要在项目中复制第二份规则。导入样例不是安装依赖的替代方式。
- 运行实时联机需要已部署的 Room Gateway 和在 Host 注册的 `TinyServerGameplayModule`，并且客户端的 Tiny 规则及 Room 协议与服务端匹配。服务端不随本包交付；应向服务端维护者取得 Gateway 地址和所需的账号认证信息。
- 本包负责场景、会话、输入和表现；项目负责认证、会话令牌管理、可用的服务端和大厅生命周期。

## 最小项目入口

1. 将 [`TinyDemoGameplayScene`](../Scenes/TinyDemoGameplayScene.unity) 和项目大厅场景加入 Build Settings。
2. 通过 Package Manager 导入 [`07 Project Entry`](../Samples~/07-ProjectEntry/README.md)，将 `TinyProjectLobbyEntry` 挂到项目大厅的对象上。登录成功后向 `Enter` 传入已有的 `DemoMultiplayerLaunchRequest`。
3. 样例调用 [`TinyProjectLaunch.Open(launch, returnScene)`](../Runtime/View/TinyProjectLaunch.cs)。入口检查令牌、账号、地址和返回场景名非空，端口须在 1–65535；令牌是否有效由服务端验证。场景侧消费一次性启动请求，返回时加载指定大厅。不要直接打开 Tiny 场景代替项目启动入口；独立消费项目无需安装 Starter。
4. 两名玩家连接同一 Gateway：一端建房，另一端用房间 ID 加入，双方准备后由房主开始。可视客户端应使用不同工程目录，避免共用 `Library`。进场后用 WASD 移动、空格攻击，观察角色 HP、权威帧和同步计数。

## 同步与恢复

| 房主选择 | 正常进场后应看到 | 全量快照用途 |
| --- | --- | --- |
| State | 完整状态基线后显示角色；本例不做本地状态预测 | 权威状态与恢复基线 |
| Frame | 根据权威输入帧推进规则；迟到输入可触发重演 | 订阅、断线或失配恢复，不替代帧时钟 |
| Hybrid | 本地输入先预测，再由权威输入帧确认 | 在订阅与恢复之外，周期快照还用于校验或校正 |

Hybrid 在本示例中是帧输入预测加周期状态快照，不是独立的状态同步预测回滚。教学样例只展示局部用法，完整会话使用 [`TinyBattleSession`](../Runtime/View/TinyBattleSession.cs)。

## 排错

| 现象 | 先检查 |
| --- | --- |
| 进不了场景，或不能返回 | 两个场景是否在 Build Settings、返回场景名和启动参数是否正确 |
| 显示 `Tiny launch session is missing.` | 通过大厅登录后重新调用 `Open`；场景不会自行创建已认证请求，也不能重用上次已消费的启动请求 |
| 登录、建房或 Loading 失败 | 地址、端口、会话凭据以及玩法注册；规则标识与 Room 协议应和服务端一致 |
| 长时间显示 `Waiting for baseline` | Room 是否已进入战斗、订阅的 World 是否匹配、全量快照是否到达 |
| 显示 `Resynchronizing` | 是否发生历史耗尽、哈希失配或帧队列溢出；检查是否收到恢复所需的全量基线 |

无头结果不能代替最终画面和双端可视交互检查；这些需要用实际客户端验证。
