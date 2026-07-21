# Console Demo 运行与调试

> 基于当前源码核校（2026-07-20）。`src/AbilityKit.Demo.Moba.Console/` 已从扁平结构大幅扩展为分层架构，TargetFramework 升至 **net10.0**，自动测试从 7 项缩为 **3 项**，配置目录扩展为 **三套并行**（moba/luban/ability）。

## 项目结构

```
src/
├── AbilityKit.Demo.Moba.Console/        # .NET Console 可执行（主要开发入口）
├── AbilityKit.Demo.Moba.Core/           # 核心逻辑 + 共享测试脚本（Testing/）
├── AbilityKit.Demo.Moba.Share/          # 共享数据契约
├── AbilityKit.Demo.Moba.Infrastructure/ # 基础设施
├── AbilityKit.Demo.Moba.AI/             # AI
├── AbilityKit.Demo.Moba.Tests/          # 单测
└── AbilityKit.Demo.Moba.NetworkCondition.Tests/
```

`AbilityKit.Demo.Moba.Console/` 内部：

- `Program.cs` — 唯一入口
- `Bootstrap/` — `ConsoleBattleBootstrapper`（装配核心）+ `ConsoleConfigLoader` + `ConsoleLubanConfigLoader` + `ConsoleConfigWorldModule` + `MobaConfigDatabase`
- `AutoTest/` — `AutoTestRunner` + `AutoTestInputFeature` + `ConsoleBattleTestScriptDriver`
- `Battle/` — `Config/` `Context/` `ECS/` `Features/` `Flow/`（Phases + Steps）`Input/` `Prediction/` `Session/` `Sync/`
- `Platform/` — `Log.cs` + `Console/`（ConsoleOutput/InputSource/Renderer）
- `Presentation/` — 表现层胶水
- `Replay/` — `ReplayController` + `ShareReplayController`
- `Services/` — `ConsoleEffectExecutionService` + `MobaOpCode` + 编解码器
- `View/` — Console 表现层
- `Configs/` — 见下

## 源码位置规则

| 操作 | 去哪里 |
|------|--------|
| **阅读/修改 Unity 框架代码** | `Unity/Packages/` |
| **阅读/修改 Console Demo** | `src/AbilityKit.Demo.Moba.Console/` |
| **运行 dotnet** | `src/AbilityKit.Demo.Moba.Console/` |
| **修改共享测试脚本** | `src/AbilityKit.Demo.Moba.Core/Testing/` |

**禁止**：在 `src/` 创建与 `Unity/Packages/` 重复的源码文件。

## 运行

```powershell
cd src/AbilityKit.Demo.Moba.Console
dotnet run
```

需本地装 .NET 10 SDK。

## CLI 参数（Program.ParseArguments）

| 参数 | 模式 | 说明 |
|------|------|------|
| 默认（无参） | `StartTestMode` | 跑 `FullBattleScenario` 自动测试 |
| `--skill` | `StartSkillTestMode` | 跑 `SkillCastScenario`（slot 1, repeats 5） |
| `-r` / `--record [path]` | Record | 录制 |
| `-p` / `--replay` / `--play <file>` | Replay | 回放 |
| `-l` / `--list` | List | 列出 Records 下录像 |
| `--info <file>` | Info | 打印录像元信息 |
| `-t` / `--test` | Test | 显式测试模式 |
| `--trace` / `--debug` | Trace | `Log.EnableTrace()` |
| `-h` / `--help` / `-?` | Help | 帮助 |

## 自动测试（当前 3 项，**不是旧文档的 7 项**）

入口：`AutoTest/AutoTestRunner.cs`，`AutoTestResult.TotalCount => 3`。

| # | 名称 | 来源 | 判定 |
|---|------|------|------|
| 1 | **Initialization** | `TestInitialization` | bootstrapper / Flow / Context / EcsWorld / BattleView 非空 |
| 2 | **Phase Transition** | `TestPhaseTransition` | `TransitionTo("InMatch")` 后 `flow.CurrentPhase == "InMatch"` |
| 3 | **FullBattle 脚本** | `BattleTestScriptRunner.Run` | 脚本跑完无异常即 `Completed=true` |

旧文档列的 Frame Sync / Skill Cast / Damage Calculation / Cooldown System / Move System **不再是独立具名测试**，被合并进第 3 项 `FullBattle` 脚本（由 `BattleTestScenarioLibrary.CreateFullBattle` 生成：3 轮 MoveAndCast + 10 步 RandomMovement + slot 1/2/3 各 2 次 SkillCast）。

辅助类：
- 场景定义：`src/AbilityKit.Demo.Moba.Core/Testing/BattleTestScriptRunner.cs`
- 脚本库：`src/AbilityKit.Demo.Moba.Core/Testing/BattleTestScript.cs`（`BattleTestScenarioLibrary`，7 个预制脚本：SimpleMovement / RandomMovement / SkillCast / MoveAndCast / FullBattle / StressTest / ViewPresentationRisk）
- Console 驱动：`AutoTest/ConsoleBattleTestScriptDriver.cs`

## 日志 API（Platform/Log.cs，路径未变）

`Log` 是 `public static class`，ns `AbilityKit.Demo.Moba.Console.Platform`。

**LogLevel 枚举（14 个值，比旧文档多）：**

```csharp
public enum LogLevel {
    Trace = 0, Debug = 1, System = 2, Config = 3, Phase = 4, Battle = 5,
    Skill = 6, Damage = 7, Sync = 8, Input = 9, View = 10,
    Prediction = 11, Warning = 12, Error = 13
}
```

默认 `_minLevel = LogLevel.Battle`。

**核心 API（旧文档 3 个 API 仍然有效，新增 DisableTrace）：**

```csharp
Log.SetMinLevel(LogLevel level);          // 设最低级别
LogLevel Log.MinLevel { get; }
Log.EnableTrace();                         // 等于 SetMinLevel(LogLevel.Trace)
Log.DisableTrace();                        // 新增：等于 SetMinLevel(LogLevel.Battle)
```

**额外能力（旧文档未提）：**

- 每个频道都有 `Log.Xxx(string)` 和 `Log.Xxx(string format, params object[] args)` 两个重载
- 频道方法：`System / Phase / View / Sync / Input / Prediction / Battle / Skill / Damage / Buff / Projectile / Area / Entity / Config / Debug / Warn / Error / Trace`
- 调试专用：`Log.Attribute(...)` 输出 `[ATTR]` 到 Debug；`Log.Cooldown(...)` 输出 `[CD]` 到 Debug
- `Log.SetOutput(IOutput)` / `Log.AddSink(ILogSink)` / `RemoveSink` / `ClearSinks` / `GetSinks`
- `Log.Separator()` / `Log.Title()` / `Log.Clear()`

## 日志频道与前缀（ConsoleOutput.GetPrefix）

17 个频道及其前缀：

```
[SYS] [PHASE] [VIEW] [SYNC] [INPUT] [BATTLE] [SKILL] [DMG] [BUFF]
[PROJ] [AREA] [ENTITY] [CONFIG] [DEBUG] [WARN] [ERROR] [TRACE] [PRED]
```

输出格式：`{前缀} {消息}`（带 ConsoleColor 着色）。

## 帧同步日志

`[SYNC]` 频道仍然存在（`Log.Sync(...)` + 前缀 `[SYNC]`）。**默认级别 `Battle` 下会输出**（`Sync` 映射到 `LogLevel.Warning`，`Warning(12) >= Battle(5)` 为真）。

真实日志样本（取自 `Battle/Sync/FrameSyncAdapter.cs`）：

```
[SYNC] [FrameSync] Initialized - Mode: <Mode>, LocalActorId: <id>
[SYNC] [FrameSync] Frame: 300, State: InMatch, Actors: 5         # 每 300 帧输出一次（约 10s @30FPS）
[SYNC] [FrameSync] Disconnected
```

注意消息内部还自带 `[FrameSync]` 子前缀。

## 配置文件（三套并行）

| 配置目录 | 用途 | 加载入口 |
|---------|------|---------|
| `Configs/moba/` | 业务运行时（characters/skills/buffs 等） | `ConsoleConfigLoader.MobaConfigDir = "moba"` |
| `Configs/luban/` | Luban 生成 | `ConsoleLubanConfigLoader`（`resourcesDir = "luban/moba"`） |
| `Configs/ability/` | 触发器规则（trigger_*.json / rules / trigger_sources） | （Plan JSON） |

### `Configs/moba/` 当前清单（精选；新增项标 NEW）

| 文件 | 状态 |
|------|------|
| `characters.json` | 存在，含 `SkillIds` + `PassiveSkillIds` |
| `skills.json` | 存在，字段含 `CastFlowId` / `LevelTableId` / `SkillButtonTemplateId` |
| `buffs.json` | 存在；另有 `dtbuff.json` |
| `projectiles.json` | 存在；另有 `projectile_launchers.json`（NEW） |
| `attribute_templates.json` | 存在（带下划线，富字段）；**同时存在** `attributetemplates.json`（无下划线，Luban 风格） |
| `passive_skills.json` | NEW |
| `skill_flows.json` | NEW |
| `skill_level_tables.json` | NEW |
| `skill_button_templates.json` | NEW |
| `presentation_templates.json` | NEW |
| `effect_plans.json` / `effects.json` | NEW |
| `emitters.json` | NEW |
| `summons.json` | NEW |
| `continuous_processes.json` / `continuous_tag_templates.json` | NEW |
| `search_query_templates.json` | NEW |
| `tag_templates.json` | NEW |
| `component_templates.json` | NEW |
| `motion_groups.json` | NEW |
| `gameplays.json` / `models.json` | NEW |
| `aoes.json` / `ongoing_effects.json` / `attr_types.json` / `battle_start.json` / `demo_tbitem.json` / `spawn_summon_action_templates.json` | 存在 |

### 技能 ID 命名规则（仅 `Configs/moba/characters.json` 成立）

- `characters.json` 条目：`Id=1001 廉颇` → `SkillIds=[10010101, 10010201, 10010301]` / `PassiveSkillIds=[10010000]`
- `skills.json` 含对应 Id：`10010101`(爆裂冲撞) / `10010201`(熔岩重击) / `10010301`(天崩地裂)
- 规律：`{charId 4 位}{slot/branch}`
- 注意：`Configs/luban/characters.json` **没有 SkillIds**，此映射**只对 moba 版成立**

## 关键模块（旧 vs 新）

| 旧 skill 引用 | 当前实际状态 |
|---------------|-------------|
| `Platform/Log.cs` | **仍存在**（API 扩展，见上） |
| `Services/SkillExecutor.cs` | **已删除**（无对应类；技能执行走 `SkillCastCoordinator` + `SkillPipelineRunner`，但这些类在 `Unity/Packages/com.abilitykit.demo.moba.runtime/` 而非 Console 项目） |
| `Services/BattleServices.cs` | **已删除** |
| `Battle/ConsoleInputFeature.cs` | **路径已变**为 `Battle/Input/ConsoleInputFeature.cs` |
| `AutoTest/AutoTestRunner.cs` | **仍存在**，但 3 项（非 7 项） |

新增的编排核心（旧 skill 完全没有）：

- `Bootstrap/ConsoleBattleBootstrapper.cs` — Program 实际持有的装配核心（替代旧 Program 直接编排）
- `Battle/Flow/BattleFlow.cs` + `Phases/` + `Steps/` — 阶段机（Idle/Connect/CreateOrJoinWorld/LoadAssets/Prepare/InMatch/End）
- `Battle/Sync/` — `FrameSyncAdapter` / `StateSyncAdapter` / `HybridSyncAdapter` + `SyncAdapterFactory`
- `Battle/Session/ConsoleSessionOrchestrator.cs`
- `Replay/` — 录制/回放（`ReplayController` + `ShareReplayController`）

## 故障排查

| 问题 | 排查方法 |
|------|---------|
| 技能配置未加载 | 检查 `Configs/moba/skills.json` 中 Id 是否与 `characters.json` 的 `SkillIds` 匹配；区分 moba 与 luban 版 |
| 冷却不生效 | 确认 `skills.json` 的 `CooldownMs > 0` 且配置被 `ConsoleConfigLoader` 加载 |
| 输入无响应 | 检查 `ConsoleBattleContext.State` 是否为 `InMatch`；看 `Battle/Input/ConsoleInputFeature.cs` |
| 帧不同步 | `Log.SetMinLevel(LogLevel.Sync)` 看完整 `[SYNC]` 日志；检查 `Battle/Sync/FrameSyncAdapter` |
| net10.0 缺失 | 旧 SDK 装不了，需升级到 .NET 10 |
| 旧测试名称找不到 | Frame Sync / Skill Cast 等不再是独立测试，看 `FullBattle` 脚本结果 |

## 参考文档

- `Docs/通用技能系统架构设计.md`
- `Docs/AbilityKit.Moba.Console视图层架构设计.md`
- `Docs/MOBA技能管线模块开发设计文档.md`
