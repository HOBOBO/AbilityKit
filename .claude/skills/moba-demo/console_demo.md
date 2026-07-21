# Console Demo（src/AbilityKit.Demo.Moba.Console）

基于源码核校（2026-07-20）。net10.0，3 项自动测试，4 种运行模式。

## 入口

`Program.cs`，4 模式：

| CLI | 模式 | 说明 |
|-----|------|------|
| 默认 | `StartTestMode` | 跑 `FullBattleScenario` 自动测试 |
| `--skill` | `StartSkillTestMode` | 跑 `SkillCastScenario`（slot 1, repeats 5） |
| `-r` / `--record [path]` | Record | 录制 |
| `-p` / `--replay` / `--play <file>` | Replay | 回放 |
| `-l` / `--list` | List | 列出 Records 下录像 |
| `--info <file>` | Info | 打印录像元信息 |
| `-t` / `--test` | Test | 显式测试模式 |
| `--trace` / `--debug` | Trace | `Log.EnableTrace()` |
| `-h` / `--help` | Help | 帮助 |

## 内部结构

```
src/AbilityKit.Demo.Moba.Console/
├── Program.cs
├── Bootstrap/     ConsoleBattleBootstrapper（装配核心）+ ConsoleConfigLoader + ConsoleLubanConfigLoader
│                 + ConfigurationLoader + ConsoleTextAssetLoader + MobaConfigDatabase
│                 + ShareComponentsInitializer + DirectCallInputSink + IBattleBootstrapper
├── AutoTest/      AutoTestRunner + AutoTestInputFeature + ConsoleBattleTestScriptDriver
├── Battle/
│   ├── Config/    BattleStartConfig / BattleStartPlan / DeterministicHash
│   ├── Context/   ConsoleBattleContext
│   ├── ECS/       Components/ + Entities/ + BattleEntityQuery
│   ├── Features/  Context/ + Debug/ + Handlers/ + Hooks/ + Hud/ + Sync/ + View/ + SubFeatureBase + HandlerInterfaces
│   ├── Flow/      BattleFlow + FeatureHost + PhaseHost + PhaseContext + ModuleHost + ModuleInterfaces + IBattleFlow
│   │              + Phases/{Idle,Connect,CreateOrJoinWorld,LoadAssets,Prepare,InMatch,End}
│   │              + Steps/（对应每个 Phase 的 Steps）
│   ├── Input/     ConsoleInputFeature + ConsoleInputHandler + ConsoleOpCode + IInputFeature
│   ├── Prediction/ Handlers/ + Motion/{MotionPredictor, MotionDescriptors}
│   ├── Session/   ConsoleSessionOrchestrator + ConsoleSessionState + ConsoleSessionHooks
│   └── Sync/      FrameSyncAdapter + StateSyncAdapter + HybridSyncAdapter + SyncAdapterFactory + IBattleSyncAdapter
│                  + ServerConnectionConfig + View/SampleBuffer
├── Platform/      Log.cs（14 LogLevel）+ Abstractions/IPlatformAbstractions + Console/{Output, InputSource, Renderer}
├── Presentation/  ConsolePresentationCuePresenter + IAreaViewManager + IHudPresentation + IVfxManager
├── Replay/        ReplayController + ShareReplayController
├── Services/      ConsoleEffectExecutionService + MobaOpCode + SimpleMoveCodec + SkillInputCodec + SkillInputEvent
├── View/          ConsoleBattleView + ConsoleViewBinder + ConsoleViewFactory + ConsoleViewTimeline
│                 + ConsoleEntityDisplayService + ConsoleAreaViewSystem + ConsoleProjectileDisplayService
│                 + ConsoleFloatingTextSystem + ConsoleBattleViewEventSink + IConsoleViewBinder
└── Configs/       见 configs.md
```

## BattleFlow 阶段机

```
Idle → Connect → CreateOrJoinWorld → LoadAssets → Prepare → InMatch → End
```

每个 Phase 在 `Battle/Flow/Phases/` 下一个类，Steps 在 `Battle/Flow/Steps/` 下对应。

## 3 项自动测试（**不是旧文档的 7 项**）

入口 `AutoTest/AutoTestRunner.cs`，`TotalCount = 3`：

| # | 名称 | 判定 |
|---|------|------|
| 1 | Initialization | bootstrapper/Flow/Context/EcsWorld/BattleView 非空 |
| 2 | Phase Transition | `TransitionTo("InMatch")` 后 `flow.CurrentPhase == "InMatch"` |
| 3 | FullBattle 脚本 | `BattleTestScriptRunner.Run` 跑完无异常即 `Completed=true` |

旧文档的 7 项（含 Frame Sync / Skill Cast / Damage / Cooldown / Move）**已合并进第 3 项 FullBattle 脚本**。

## Program.cs 装配调用链

```
Program.Main(args)
    ↓ ParseArguments
    ↓
new ConsoleBattleBootstrapper(...)   // Bootstrap/ 装配核心
    ↓
bootstrapper.Setup()                 // 建 Context/Flow/View/Sync/Input
    ↓
AutoTestRunner.Run() OR ReplayController.Run() OR SkillTestRunner.Run()
    ↓
每帧 bootstrapper.Tick(dt)
```

详见 `Bootstrap/ConsoleBattleBootstrapper.cs`。

## 日志

`Platform/Log.cs`，14 个 LogLevel（Trace=0...Error=13），频道前缀 17 个：`[SYS]/[PHASE]/[VIEW]/[SYNC]/[INPUT]/[BATTLE]/[SKILL]/[DMG]/[BUFF]/[PROJ]/[AREA]/[ENTITY]/[CONFIG]/[DEBUG]/[WARN]/[ERROR]/[TRACE]/[PRED]`

详见 ability-kit skill 的 console_demo/README.md。
