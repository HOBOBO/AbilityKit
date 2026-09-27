# Tiny Unity 示例

通过 Unity Package Manager 导入 `Samples~/07-ProjectEntry` 可查看项目侧大厅入口装配；需要回合制时另装 `com.abilitykit.demo.tiny.turn` 并导入其 `10-TurnEntry`。仓库的 `verify-tiny-starter.ps1 -FocusChapter 01` 至 `06`、`09`、`10` 提供对应章节的短门禁；完整门禁使用独立工程和两个并行无头 Unity 客户端验证 State/Turn 的真实 Gateway 路径，证据在 `unity-cross-State/`、`unity-cross-Turn/`。无头证据不判断最终画面。

脱离源码仓库接入时先阅读 [包内接入指南](Documentation~/IntegrationGuide.md)。它说明客户端入口、服务端前提与逐章学习顺序。

## 教程入口

源码仓库的 `Docs/tutorials/tiny/00-从这里开始.md` 规定从纯规则到正式项目的 00–07 学习路径。纯规则已迁入独立的 `com.abilitykit.demo.tiny.logic` 包，其中有 `01 Logic` 样例；本包提供 02–06 的可导入样例。02–05 的同一份源码分别由 Room、State、Frame、Hybrid 独立双客户端工程验收；06 的本地片段由 `ChapterSamples` 运行，四类恢复故障由 `RecoverySample` 无头验收。

## 可重复验收

在仓库根目录运行 `./tools/verify-tiny-starter.ps1`。脚本依次检查协议目录与 Room wire schema，执行 Room 和 Tiny 复制器测试，构建客户端及服务端，启动独立端口上的 Host/Gateway，分别运行 Room、State、Frame、Hybrid 逐章双客户端样例、Recovery 四类故障验收，并验证三种同步模式及断线恢复。服务端仍运行时，隔离 Unity 网络 PlayMode 用两套正式会话及角色 View 验证三模式双端投影与访客恢复，结果写入 `tiny-network-playmode.json`。随后运行 Tiny EditMode/PlayMode、无 Starter 消费工程和独立 Logic 工程测试；结果保存在 `local/Logs/tiny-acceptance-<时间戳>/`。测试检查 Unity 对象状态，不检查最终画面。仅验证 .NET 和 TCP 时可传入 `-SkipUnity`。

`./tools/create-tiny-validation-project.ps1` 默认在 `local/Logs/` 生成带时间戳的独立工程，也可用 `-OutputPath` 指定外部目录；非空目录不会被覆盖。生成器将 Tiny/Starter 的本地包依赖闭包嵌入工程的 `Packages/`，manifest 不含源仓库绝对路径。MemoryPack 需要的 `System.Runtime.CompilerServices.Unsafe.dll` 由 `com.abilitykit.thirdparty.unsafe` 包提供，消费工程不从源码工程的 `Assets/Plugins` 补拷插件。工程生成后可整体移动到其他目录。Unity 2022.3.62f3 可打开其中的 `StarterScene`，也可用上述脚本执行 EditMode 测试。首次打开仍需获取 manifest 中声明的 MemoryPack 和 Unity 公共包。

两个可视化 Unity 客户端必须使用两个独立的工程目录，以免共用 `Library` 锁。可在仓库根目录执行：

```powershell
./tools/create-tiny-validation-project.ps1 -OutputPath C:\TinyClientA
./tools/create-tiny-validation-project.ps1 -OutputPath C:\TinyClientB
./Server/Orleans/tools/start_orleans_dev.ps1
```

分别用 Unity 打开两份工程的 `StarterScene`，使用不同账号登录；一端创建房间，另一端输入房间 ID 加入。双方准备后开战，分别在 State、Frame、Hybrid 模式检查移动、攻击伤害、权威帧与预测帧、回滚/校正计数，并断开一端连接验证恢复。`start_orleans_dev.ps1` 默认 TCP 端口为 4000，与 Starter 默认配置一致。此过程属于双 Unity 客户端的场景验收；EditMode 和无头 TCP 测试不能替代它。

战斗界面展示同步模式、连接状态、权威帧与预测帧，以及预测、回滚、快照校正、恢复请求和帧队列溢出计数。计数由复制器与通用 Room 帧收件箱提供，界面展示由 `TinyHudPresenter` 负责。

Tiny 是 AbilityKit 的最小联机战斗接入示例：战斗规则保持简单，房间、连接、同步、回滚和恢复尽量使用框架提供的能力。它用于展示正式项目的接入边界，而不是提供一套新的游戏框架。

## 代码结构

Tiny 分为独立 Logic 包和完整联机包，共使用三个程序集：

- `AbilityKit.Demo.Tiny.Logic`：确定性战斗规则，同时由 Unity 客户端、.NET 服务端和测试编译；不依赖 Unity 或表现层。
- `AbilityKit.Demo.Tiny.FrameSync`：将 Tiny 战斗状态接入框架的输入历史、快照与回滚协调器。
- `AbilityKit.Demo.Tiny.View`：Gateway 连接、Room 会话、输入和场景表现。

`TinyGatewayClient` 使用通用的 `RoomGatewayConnectionSession`，只保留 Tiny 输入载荷和快照缓存策略。登录、请求响应、推送解码与操作码由 `RoomGatewayWireSessionClient` 和生成的 Room 协议负责。`TinyBattleSession` 使用通用 Room 流程和同步能力协商，并通过 `ITinyBattleGateway` 与传输隔离，便于脱离场景测试。`TinyGameplayRoot` 通过可选的 `AbilityKit.Game.View.Runtime` 模块宿主运行输入和角色表现模块。房间创建、玩家槽位选择及 Tiny 输入策略仍由示例负责。

目录按职责划分：独立 Logic 包保存规则；本包的 `Runtime/FrameSync` 保存回滚适配，`Runtime/View` 保存会话与表现，`Runtime/View/Loading` 和 `Runtime/View/Sync` 保存项目侧策略。连接恢复、命令 ID、Loading 阶段、全量快照游标和类型化 Room 调用位于 `com.abilitykit.network.room`。

## 运行

启动包含 Tiny 服务端模块及 Room 帧同步协议的 Orleans Host 和 Gateway。打开 `StarterScene`，在 `com.abilitykit.demo.starter/Configs/StarterConfig.asset` 中配置 Gateway 地址和端口。使用不同账号启动两个 Unity 客户端：一方创建房间，另一方输入房间 ID 加入；双方准备后，由房主开始。WASD 移动，空格攻击；大厅中的 Back 返回 Starter，战斗中的 Back 会断开客户端连接。

不使用 Starter 的项目需将 Tiny 场景和自己的大厅场景加入 Build Settings，先完成认证并构造 `DemoMultiplayerLaunchRequest`，再调用 `TinyProjectLaunch.Open(launch, "YourLobbyScene")`。该入口会同时准备场景 Bootstrap 所需的玩法及多人请求，Tiny 场景一次性消费会话，返回时加载项目指定的大厅场景。运行 `./tools/create-tiny-validation-project.ps1 -Standalone` 可生成不含 Starter 的最小消费工程；其大厅脚本展示项目侧调用，PlayMode 测试验证进场与返回。服务端 Tiny 模块由 `AbilityKit.Demo.Tiny.Server` 在 Host 组合根注册。

Tiny 场地由代码生成，不需要外部资源。Loading 步骤在报告完成前验证启动清单；有资源的项目可替换 `ClientLoadingPipeline` 的资源准备步骤。

## 同步模式

创建房间时可选择 **State**、**Frame** 或 **Hybrid**。模板 ID 写入房间启动标签，加入方根据服务端快照协商同步能力。

| 模式 | 客户端行为 | 全量快照用途 |
| --- | --- | --- |
| State | 展示服务端权威状态 | 常规状态同步 |
| Frame | 根据权威输入帧推进战斗；迟到输入可触发回滚重放 | 订阅基线、断线或失配恢复 |
| Hybrid | 提交本地输入时立即预测，再用权威帧确认或回滚校正 | 订阅与恢复，并每五个服务端帧定期校验 |

Hybrid 收到与历史哈希一致的周期快照时保留后续预测；不一致时恢复到权威状态。帧哈希失配或回滚历史缺失时，客户端请求新的全量快照，恢复期间暂停提交帧输入。

## 无头验收

TCP 验收客户端使用两个账号及与 Unity 相同的 Room API。Gateway 需包含 Room 帧同步操作码 `121`、`122`、`9006`。

```powershell
dotnet run --project src/AbilityKit.Demo.Tiny.Client -- 127.0.0.1 4057 tiny-state state
dotnet run --project src/AbilityKit.Demo.Tiny.Client -- 127.0.0.1 4057 tiny-frame frame
dotnet run --project src/AbilityKit.Demo.Tiny.Client -- 127.0.0.1 4057 tiny-hybrid hybrid
```

Frame 验证双方权威结果收敛；Hybrid 验证本地即时预测与后续权威结果收敛。迟到输入的重放由 04/05 本地样例及复制器测试断言，因为网络中的周期快照可能先覆盖该帧。三种模式均验证断线后的全量基线恢复。`AbilityKit.Demo.Tiny.Replication.Tests` 会将 Unity 使用的同一份 `TinyFrameReplication` 源码投影到 .NET 测试中。

统一验收另包含 `session-hybrid-mismatch`：无头驱动向一端注入一次本地故障快照，断言 Hybrid 原位校正，并在真实服务端周期快照到达后核对两端与权威状态的同帧哈希。结构化结果写入 `hybrid-mismatch.json`，与 `hybrid-mismatch.log` 一起保存在当次验收目录；故障注入器只在 .NET 验收驱动中。
