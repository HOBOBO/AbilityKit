# Tiny 验收

## 统一入口

```powershell
./tools/verify-tiny-starter.ps1                      # 完整（含隔离 Unity 批处理）
./tools/verify-tiny-starter.ps1 -SkipUnity            # 只跑 .NET 与 TCP
./tools/verify-tiny-starter.ps1 -FocusChapter 01      # 逐章短门禁
./tools/verify-tiny-starter.ps1 -FocusTurnRecord      # 只跑回合制与录制
./tools/verify-tiny-starter.ps1 -FocusTurnRecord -VerifyCrossProcess
./tools/verify-tiny-starter.ps1 -FocusChapter 10 -SkipUnity -ServerBundlePath <目录>
```

参数（`tools/verify-tiny-starter.ps1`，1042 行）：

| 参数 | 说明 |
|---|---|
| `-SkipUnity` | 跳过全部 Unity 阶段 |
| `-FocusChapter` | `ValidateSet('','01','02','03','04','05','06','09','10')`——**07 与 08 刻意不在内**（07 要完整门禁，08 是人工编辑练习） |
| `-FocusTurnRecord` | 只跑回合制 + 录制相关阶段 |
| `-VerifyCrossProcess` | 才运行 Hybrid/Frame/State/Turn 的双 Unity 进程 |
| `-CrossProcessModes` | 默认 `@('Hybrid','Frame','State','Turn')` |
| `-ServerBundlePath` / `-UseCompositionHost` | 用发布包跑真实 TCP；后者**要求**前者（否则 throw） |
| `-UnityExe` | **默认写死 `C:\Software\Unity 2022.3.62f3\Editor\Unity.exe`**——换机器必须显式传 |
| `-SiloPort 11170` / `-OrleansGatewayPort 30070` / `-TcpPort 4058` / `-HttpPort 5058` | 隔离端口 |

`-FocusChapter 01` 跑完 `Logic.Sample` 后会把 `tcp` 记为 `skipped` 并**提前返回**。

## 17 个阶段

```
protocol                        → compile-protocol-catalogs.ps1 -Check
                                  export-protocol-wire.ps1 -Projects room -Check
dependencies                    → 委派 verify-tiny-dependencies.ps1
dotnet                          → 测试/构建/运行（见下）
server-bundle                   → 可选：物化发布包
tcp                             → 启动 Host/Gateway + 各模式 smoke
tcp-process                     → 每模式两个独立 .NET 进程（房主/访客）
tcp-record                      → LiveRecord.Sample
tcp-turn                        → Turn.StateSample
unity-editmode / unity-playmode
unity-logic-editmode
unity-network-playmode          （真实 Gateway，同进程两套正式会话）
unity-turn-network-playmode
unity-consumer-playmode
unity-consumer-network-playmode
unity-consumer-turn-network-playmode
unity-cross-process             （仅 -VerifyCrossProcess）
```

每条 `Write-Phase` 都重写 `phases.json`（含 `status`/`updatedAtUtc`/可选 `detail`）；脚本顶部 `trap` 把仍为 `running` 的阶段标 `failed` 后重抛。**`-SkipUnity` 时 Unity 阶段记为 `skipped` 而不是省略**——所以 `phases.json` 永远枚举全集，可以据此判断"真跑了"还是"跳过了"。

`dotnet` 阶段默认（无 focus）会跑：`dotnet test` on `src/AbilityKit.Network.Room.Tests`、`src/AbilityKit.Demo.Tiny.Replication.Tests`、`AbilityKit.Orleans.Grains.Tests`（`--filter FullyQualifiedName~Tiny|~RoomStateStoreDeepCopyTests`）、`AbilityKit.Orleans.Gateway.Tests`（Release，`~RoomMembershipIntegrationTests`）；再 `dotnet run` `Logic.Sample`/`ChapterSamples`/`ProtocolEvolution.Sample`/`BattleStyles.Sample`/`Record.Sample`；再 `dotnet build` `Client`/`Turn.StateSample`/`Turn.ClientHarness`/`LiveRecord.Sample`/`StateSample`/`RoomSample`/`FrameSample`/`HybridSample`/`RecoverySample`。

## 证据目录

每次运行创建 `local/Logs/tiny-acceptance-<yyyyMMdd-HHmmss>/`，并把 `ArtifactsPath`/`UseArtifactsOutput` 指向其 `artifacts/` 子目录（**避免与其他构建共用 `bin/obj`**）。其中：

| 文件/目录 | 内容 |
|---|---|
| `phases.json` | 各阶段状态 |
| `process-{Mode}/owner.json`、`guest.json` | 双 PID、房间/战斗绑定、攻击结果、访客恢复 |
| `process-Turn/` | 回合制双进程证据 |
| `unity-cross-{Mode}/` | 每模式一对独立无头 Unity 进程的 XML/日志/JSON |
| `tiny-network-playmode.json`、`tiny-turn-network.json`、`tiny-consumer-network.json`、`tiny-consumer-turn-network.json` | Unity 网络 PlayMode 结果 |
| `hybrid-mismatch.json` / `.log` | 注入一次本地故障快照后的原位校正 |
| `tiny-live-record.bin` / `.json` | 真实 Gateway 录制 |
| `room-sample.log`、`state-sample.log`、`session-{state,frame,hybrid}.log`、`recovery-sample.log` | 各章 .NET 样例日志 |

> 文档小瑕疵：`Docs/tutorials/tiny/09` 让读者把录制写到 `local/Logs/tiny-record.bin`，而门禁写的是 `local/Logs/tiny-acceptance-<runId>/tiny-record.bin`。两者都合法（`Record.Sample` 接受 `[output.bin]` 参数），只是位置不同。

## **CI / gate 现状：没有**

- `tools/test-gates.json`（30 个 gate，P0=3 / P1=19 / P2=8）**零处提及 Tiny**；`tools/run_test_gate.ps1` 同样零命中。Tiny 的整套验收是**手动入口**，`verify-tiny-starter.ps1` 是**叶子**——`tools/` 里没有任何东西调用它。
- `.github/workflows/` **已整体删除**（末次提交 `3952d9b2`「暂时删除ci检测」，删掉 772 行的 `abilitykit-test-gates.yml`）。被删的 workflow 里原本有 `tiny-starter`（PR 跑 `-SkipUnity`）与 `tiny-unity`（`workflow_dispatch` + `run_tiny_unity`，跑完整批处理）两个 job——**设计本身是对的，只是被一并删掉了**。
- `Docs/tutorials/tiny/07` 已同步声明「当前仓库没有 Tiny 的 CI workflow」。
- `src/AbilityKit.Demo.Tiny.Replication.Tests` 是真实 xunit 工程但**不在任何 gate**——只在验收脚本第 541 行被引用。

**所以 Tiny 的准确证据等级是：E3（有测试、本轮 .NET 侧实测通过）+ E4（有历史验收产物），E5 = 无。** 不要把"有 1042 行验收脚本"读成"有 CI 阻断"。

## 生成隔离消费工程

| 脚本 | 产物 |
|---|---|
| `tools/create-tiny-validation-project.ps1` | 拉**传递闭包**的本地包到 `<project>/Packages/`，manifest 不含源码仓库绝对路径。默认带 Starter；`-Standalone` 去掉 Starter 并用 `tools/tiny-consumer-template/Assets` 的 `ConsumerLobby.unity`；`-IncludeTurn` 加回合制包与场景。拒绝输出到源码 `Unity/` 树内或非空目录 |
| `tools/create-tiny-logic-validation-project.ps1` | 零依赖兄弟版：**只**复制 `tiny.logic` 一个包 + 其 samples，证明章 01 单独可跑（先断言该包依赖为空，否则 throw） |
| `tools/publish-tiny-server.ps1` | `-OutputPath <dir> [-Configuration]`：发布 Host → `host/`、Gateway → `gateway/`，**校验两边 `AbilityKit.Protocol.Room.dll` 哈希一致**（不同则 throw），写 `bundle.json`（`roomProtocol` / `tinyRules`=`AbilityKit.Demo.Tiny.Core.dll` / `turnRules`=`Turn.Core.dll` 的 SHA256），再把 `tools/tiny-server-template/` 拷进 `composition/` 并构建 `TinyCustomHost.csproj` |

`tools/tiny-consumer-template/` 共 792 行 C#：`TinyConsumerLobby`（14 行，调 `TinyProjectLaunch.Open`）、`TinyConsumerPlayModeTests`（43 行，**无需服务器**）、`TinyConsumerNetworkPlayModeTests`（264 行，需 Gateway）、`Turn~/`（Turn 大厅 + 150 行网络测试 + 307 行跨进程测试）。

## Unity 测试源码的 .NET 侧编译验证

Tiny 的 Tests/（Editor/PlayMode/NetworkPlayMode）没有任何 .NET 投影——src 里没有工程编译它们，所以悬空引用类缺陷（如 2026-09-28 的 cursor CS0103）只有 Unity 编译才暴露。离线做法：建一次性探针工程（Unity 桩 + Compile Include 全部测试文件与 View 运行时源码 + nunit 包），装配名设为 AbilityKit.Demo.Tiny.Editor.Tests 即可访问 internal 成员；缺 Unity API 时按真实形状补桩（响亮失败方向）。参考实现曾位于 local/Logs/tiny-editor-probe/（gitignored，可按本节描述重建）；共享桩在 tools/unity-host-probe/UnityStubs.cs，已含 GameObject/Transform/MonoBehaviour/SceneManagement/GUILayout 等通用成员。

## 工具调用关系（谁调谁）

`verify-tiny-starter.ps1` 是**唯一编排者**，也是**叶子**（`tools/` 里没有任何东西反过来调它）：

| 调用点 | 被调 |
|---|---|
| `:501` | `verify-tiny-dependencies.ps1`（`dependencies` 阶段） |
| `:713` `-IncludeTurn` | `create-tiny-validation-project.ps1` → `$outputRoot/unity-project` |
| `:806` `-Standalone` | → `$outputRoot/consumer-project` |
| `:856` `-Standalone -IncludeTurn` | → `$outputRoot/consumer-turn-project` |
| `:1005` | `create-tiny-logic-validation-project.ps1` → `$outputRoot/logic-project`；随后**断言 `Packages/manifest.json` 不含仓库根路径或 `file:`**，证明生成工程没有指回源码仓库的路径 |
| `:327`（`foreach role in owner,guest`） | `create-tiny-validation-project.ps1 -Standalone -IncludeTurn`，供跨进程双 Unity 阶段用 |

每次 `create-tiny-validation-project.ps1` 调用后都紧跟 `Assert-TinyPackageDistribution`。两个 `create-*` 脚本是**纯叶子**，自己不调用任何东西。

`tools/tiny-server-template/README.md` 与 `composition/README.md` 推荐的服务端定向验证命令是
`./tools/verify-tiny-starter.ps1 -FocusChapter 10 -SkipUnity -ServerBundlePath <bundle> [-UseCompositionHost]`——`FocusChapter '10'` 分支与 `ServerBundlePath` 块可组合，命令有效。

## 依赖边界检查

`tools/verify-tiny-dependencies.ps1`（63 行）两半：

1. **csproj 引用漂移**：XML 解析 `ProjectReference` 并做**集合相等**断言——但**只覆盖 10 个项目**（`Logic.Sample`、`RoomSample`、`StateSample`、`FrameSample`、`HybridSample`、`Record.Sample`、`ProtocolEvolution.Sample`、`BattleStyles.Sample`、`Turn.StateSample`、`LiveRecord.Sample`）。**未覆盖**：`Core`、`FrameSync`、`Turn.Core`、`ChapterSamples`、`Client`、`ClientHarness`、`Turn.ClientHarness`、`RecoverySample`、`Replication.Tests`。
2. **Unity package.json 边界**：`tiny.logic` 必须零依赖；`tiny` 必须依赖 `tiny.logic` + `world.framesync`、**不得**含 `record`、不得含匹配 `skill|buff|projectile` 的依赖、不得含 `demo.tiny.turn`；`tiny.turn` 依赖集必须恰好五项（见 [packages_overview.md](packages_overview.md)）。

## 非目标（文档自己声明的，别拿无头结果顶替）

- **画面/交互**：所有 Unity 验收都是隔离工程的 `-batchmode -nographics`，**不截图**。无头测试只证明对象、会话与 Gateway 行为，**不证明最终渲染观感**。
- **两台可视化 Unity 客户端的联机**：需两个独立工程目录 + 人眼验收，属独立场景验收。`TinyGatewayPlayModeTests` 与跨进程测试都在**同一或各自无头进程**内，不等于两份可视化工程通过 `TinyGameplayRoot` 的完整界面交互。
- 双进程 TCP 证明的是"两个独立 .NET 客户端连同一 Gateway"，**不是**"两台 Unity 客户端"。
