# Tiny 项目接入指南

## 独立服务端与验收

运行 `tools/publish-tiny-server.ps1 -OutputPath <目录>` 可生成可移动的 .NET 10 服务端包，包含 `host/`、`gateway/`、可编译的 `composition/` Host 组合工程和启动说明。发布产物使用同一次构建的 Tiny 规则与 Room 协议程序集；移动后不依赖源码目录。Host 默认注册 Tiny 实时玩法与 Turn；设置 `AbilityKit__Tiny__EnableTurn=false` 可以关闭 Turn。直接使用内置 Host 时先从 `host/` 启动，再从 `gateway/` 启动；需要注册项目玩法时，修改并运行 `composition/`，由它代替内置 Host。

`tools/create-tiny-validation-project.ps1 -Standalone -IncludeTurn` 生成独立 Unity 消费工程。服务端包与客户端消费工程是两个交付边界；服务端规则只有 Unity Logic 包中的一份源码，由 .NET 工程编译后随服务端发布。

仓库验收可运行 `tools/verify-tiny-starter.ps1 -FocusChapter 10 -SkipUnity -ServerBundlePath <目录>`，用发布包运行真实 TCP Turn 样例。完整验收在隔离工程中使用 `-batchmode -nographics`；Hybrid、Frame、State、Turn 分别启动两个独立 Unity 进程联机，证据保存在 `local/Logs/tiny-acceptance-*/unity-cross-{Mode}`。`phases.json` 汇总各阶段，XML 与 JSON 保留具体测试和联机结果。客户端在 Loading 和战斗订阅前验证启动清单与本地规则标识；更新玩法规则时应同步更新规则标识和服务端发布包。

只检查回合制与录制时使用 `-FocusTurnRecord`；再加 `-VerifyCrossProcess` 才运行 Hybrid、Frame、State、Turn 双 Unity 进程。`-FocusChapter 01` 至 `06`、`09`、`10` 用于逐章验证。所有 Unity 验证均为隔离工程的无头进程，不需要打开编辑器窗口或截图。

Tiny 是两名玩家、整数坐标移动和一次带冷却攻击的最小联机项目。它展示新项目如何接入 AbilityKit 的规则、Room、三种同步模式、恢复与 Unity 表现。技能、Buff 和复杂战斗治理属于更高阶示例。

## 依赖与边界

- Unity 2022.3。先安装 `com.abilitykit.demo.tiny.logic`，再安装 `com.abilitykit.demo.tiny` 及 `package.json` 声明的传递依赖。导入 `Samples~` 只复制教学代码，不改变包依赖。
- Logic 包不依赖 Unity：`TinyInput`、`TinyBattle`、状态编解码和哈希由 Unity 客户端与 .NET 服务端共用。不要在项目中复制第二份战斗规则。
- 本包负责客户端会话、输入和表现；Room wire、连接恢复、Loading 和回滚基础设施由对应 AbilityKit 包提供。
- Orleans Host/Gateway 及 `AbilityKit.Demo.Tiny.Server` 是配套的 .NET 服务端工程，不随 Unity 包分发。服务端须注册 `TinyServerGameplayModule.Create()`，开放 Room Gateway，并与客户端使用相同的 Logic、Room 协议版本。仅安装 Unity 包无法启动多人战斗。

## 最小项目入口

将 `Scenes/TinyDemoGameplayScene.unity` 和项目自己的大厅场景加入 Build Settings。项目完成认证后，持有真实账号与会话令牌，再调用：

```csharp
using System;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Demo.Tiny.View;

var launch = new DemoMultiplayerLaunchRequest(
    gatewayHost, gatewayPort, region, serverId,
    accountId, sessionToken, TimeSpan.FromSeconds(10));
TinyProjectLaunch.Open(launch, "YourLobbyScene");
```

`TinyProjectLaunch.Open` 会准备一次性启动请求并加载 Tiny 场景。场景中的 `TinyGameplayRoot` 连接 Gateway、恢复 Room 会话，并运行输入与角色 View 模块。返回时加载调用方指定的大厅；项目负责认证、服务器地址、场景注册和大厅生命周期。`com.abilitykit.demo.starter` 包的 `StarterScene` 只是可选入口，消费项目无需安装 Starter。

服务端组合根应将 Tiny 模块加入玩法目录，例如：

```csharp
var catalog = ServerGameplayModuleCatalog.Default
    .WithModule(TinyServerGameplayModule.Create());
```

该代码位于服务端工程，需引用相应 Orleans 玩法目录和 `AbilityKit.Demo.Tiny.Server`。完整 Host 注册还应遵循服务端工程已有的依赖注入配置。

## 逐章学习

| 顺序 | 可导入样例 | 新增的项目职责 | 本地可检查结果 |
| --- | --- | --- | --- |
| 01 | Logic 包的 `01 Logic` | 输入、Tick、快照、稳定哈希 | `TinyLogicExample.Run()` 比较恢复前后 40 帧哈希 |
| 02 | `02 Room` | 已认证账号、建房命令 ID、准备与开战 | 两名账号进入同一房间和战斗 |
| 03 | `03 State` | 协商 State、订阅、读取权威角色状态 | 两端读取同一权威帧；View 仅在完整基线后显示角色 |
| 04 | `04 Frame` | 规则状态的回滚适配与权威帧哈希 | `TinyFrameExample.Run()` 重放迟到输入并收敛 |
| 05 | `05 Hybrid` | 本地输入预测与周期快照校正 | `TinyHybridExample.Run()` 预测、确认并重放远端输入 |
| 06 | `06 Recovery` | 暂停输入、请求完整基线、恢复帧流 | `TinyRecoveryExample.Run()` 从历史耗尽恢复 |

先按 01→02→03 完成 State 主线，再选学 04 Frame、05 Hybrid。06 是三种模式的横切故障处理。02–06 的片段只展示本章新增 API；可运行联机状态机在 `Runtime/View/TinyBattleSession.cs`，不应从样例片段拼出第二套会话。

仓库内另有可选 09 Record/Replay 与协议版本演进练习、10 回合制与实时 Arena 规则样例。09 在离线录制之后增加真实 Gateway State 会话的权威帧录制与复演，并用 Room 能力协商拒绝不兼容 schema。10 在本地规则对比之后增加 `tiny-turn` 服务端玩法模块与双进程 State 客户端，验证回合所有权、重连和胜者快照。可选的 `com.abilitykit.demo.tiny.turn` 包提供回合制 Unity 场景、项目入口和输入界面；使用它时需单独安装该包，不会增加本包的 Record、技能或物理依赖。详见 `Docs/tutorials/tiny`。

## 运行与排错

使用两个独立 Unity 工程目录打开两个可视客户端，避免共用 `Library` 锁。连接同一 Gateway，用不同账号登录；一端创建房间，另一端通过 Room ID 加入，双方准备后由房主开始。分别选 State、Frame、Hybrid，检查 WASD 移动、空格攻击、HP、权威帧与预测计数，再断开一端验证全量恢复。

| 现象 | 首先检查 |
| --- | --- |
| 登录或建房失败 | Gateway 地址、端口、账号会话令牌、服务端 Tiny 模块注册 |
| 长时间 `Waiting for baseline` | Room 是否已进入战斗、World 是否匹配、全量快照请求与推送是否成功 |
| `Resynchronizing` | 帧历史、哈希、收件箱溢出以及全量快照是否覆盖缺口 |
| 返回大厅失败 | 大厅和 Tiny 场景是否都在 Build Settings，返回场景名是否正确 |

仓库开发者可运行 `tools/verify-tiny-starter.ps1`，它构建隔离工程并运行依赖检查、协议、.NET、真实 Gateway TCP、Unity EditMode/PlayMode 门禁；`-SkipUnity` 只运行无头 .NET 与 TCP 阶段。三模式 TCP 阶段分别启动两个独立 .NET 客户端进程，证据位于 `local/Logs/tiny-acceptance-*/process-{Mode}`；回合制证据位于 `process-Turn`，真实录制保存为 `tiny-live-record.bin`。`tools/create-tiny-validation-project.ps1 -Standalone` 生成不含 Starter 的消费工程。上述脚本属于源码仓库，不是 UPM 包的运行依赖。

完整 Unity 批处理还会在消费工程中从自有大厅进入 Tiny 场景，连接真实 Gateway，逐模式验证攻击投影、访客恢复与返回大厅。`tiny-consumer-network.json` 记录权威帧、预测帧和校正计数；Hybrid 应在发送确认前出现本地预测，State/Frame 不应出现。协议或同步 schema 不兼容时，能力协商应阻止订阅和输入；当前 Tiny 客户端要求 schema 版本 1。

无头 PlayMode 能证明对象、会话和 Gateway 行为；双进程 TCP 能证明独立客户端进程的联机闭环。二者都不能证明最终渲染观感。画面检查应在独立可视客户端中完成；不要把测试替身的投影断言当成可视双端联机证据。
