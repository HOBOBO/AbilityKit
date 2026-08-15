# AbilityKit 白盒测试 DSL 与测试平台设计方案

> 状态：设计草案（Design Draft）
> 关联：[MOBA 验收 Scenario DSL 指南](MobaAcceptanceScenarioDSLGuide.md)、[测试门禁与批量回归规范](AbilityKit测试门禁与批量回归规范.md)、[MOBA 视图层战斗正式化计划](MobaViewRuntimeBattleFormalizationPlan.md)
> 定位：本文是把现有"MOBA 验收 Scenario DSL"**提升为项目级、玩法无关的白盒测试 DSL**，并扩展到**Unity 引擎内带界面带网络的集成测试工具**的统一设计。

## 1. 目标与范围

把项目里已经分散存在的若干测试能力（MOBA 验收 DSL、`BattleTestScript`、Shooter Acceptance Spec、smoke harness、draft 生成器、门禁/CI），收敛成**一套统一的白盒测试体系**，让测试与开发可以用接近自然语言的方式组合测试环境，并自由扩展。

核心目标：

1. **一套 DSL、一份规范 IR、多个执行载体、一种验收结果**。同一份场景描述，可以跑在无头逻辑仿真、Unity 引擎内客户端、多客户端真实网络拓扑三种载体上，产出结构一致的 JSON 验收结果。
2. **纯白盒、可任意扩展**。对照第三方黑盒测试工具，本体系对项目内部是白盒：能直接调用界面逻辑、注入真实输入、监听任意网络事件、读内部状态。新增测试动词不需要改核心。
3. **支持 Unity 引擎内、带界面、带网络的完整流程测试**。可以像正常游戏一样给输入、监听网络事件、把客户端推进到特定界面或场景再给特定输入。
4. **模板化复用**。把"登录→匹配→选英雄→加载→开战"这类主流程沉淀为模板，测试在模板基础上插入自己的步骤。
5. **尽可能可控**。等待、断言、超时、重试、故障注入、观察分支等都做成确定性原语。
6. **AI 与持续回归友好**。AI 可定时巡检配置变更、生成测试草案；专属机器按计划持续跑回归。
7. **多范式组合**。脚本时间线（确定性回归）、触发器（声明式 oracle/不变量监控）、行为树（自适应对手/流程）三范式互补，**复用框架既有的 triggering 与 behavior 引擎**而非新造——详见 §17；端到端操作流程见 §18。

## 2. 设计原则

| 原则 | 含义 |
|---|---|
| **意图优先、隐藏实现** | DSL 表达"玩法/流程意图"与"验收结果"，不暴露 ECS/service/协议细节（沿用 [DSL 指南 §13](MobaAcceptanceScenarioDSLGuide.md)）。底层重构不破坏测试。 |
| **测试基建与生产解耦** | 测试驱动器、观察门面、故障注入都做成**测试侧专用**组件，不污染生产热路径。需要观察的生产事件，通过既有 C# 事件或显式探针暴露，不强改生产结构。 |
| **确定性优先** | 逻辑层载体追求位级确定（固定 seed / tickRate / 加速无头）；UI 与网络载体追求"意图确定 + 容差断言"，显式区分两套确定性 regime。 |
| **IR 即契约** | 规范场景 IR 是执行器与验收器共同的唯一消费对象；"做什么"与"期望什么"合在一份 IR 里，不拆成两套割裂格式。 |
| **JSON 结果是通用货币** | case / suite / gate 三级 JSON 产物贯穿执行、CI、平台展示，UI 只做可视化，不自持状态。 |
| **复用而非重造** | 已有的确定性 runner、trace 验收器、draft 生成器、门禁、Flow 编排、BattleTestScript 模型都是资产，本设计是"收敛 + 补缝"，不是另起炉灶。 |

## 3. 现状盘点：已有资产与缺口

下表把愿景逐项对照现状，区分**已有**与**需新增/补缝**。

| 能力域 | 现状 | 评估 |
|---|---|---|
| 场景 DSL（actors/setup/timeline/state/context/mustContain/relationships） | `MobaAcceptanceScenarioDSLGuide.md` + `MobaAcceptanceModels.cs` 已完整 | ✅ 规范 IR 的种子，需"去 MOBA 化"提升为玩法无关 |
| 完整 JSON 验收结果 | `MobaAcceptanceTraceExporter.BuildSummary` → `*_summary.json` / `_trace.jsonl` / `batch_summary.json` | ✅ 直接复用，扩展 coverage 维度 |
| 判定逻辑 | `passed = 必需trace命中 && 禁止trace缺席 && 期望动作执行 && 因果关系成立` | ✅ 泛化为多断言族合取 |
| 无头确定性执行 | `BattleTestScriptRunner` + `AutoTestRunner` + `ConsoleBattleBootstrapper`，固定 tickRate/seed | ✅ 逻辑层载体已就绪 |
| 跨载体脚本模型 | `BattleTestScript` + `IBattleTestScriptDriver`（注释明确写"Console / Unity EditMode / 未来 headless 均应适配此模型"） | ✅ 架构已预留，缺 Unity 实现 |
| 输入注入（Console） | `IInputFeature` + `AutoTestInputFeature`，可经 `SetAutoTestInput` 整体替换 | ✅ |
| 输入注入（Unity 战斗 HUD） | `IBattleHudInputSink`（`BattleContext` 实现）→ `BattleInputRuntime` → `BattleHudInputState` | ✅ 注入点正确，缺 `UnityBattleTestScriptDriver` |
| UI 导航（大厅/元界面） | `UIManager`（`Open/Close/TryGet/IsOpen` 按 string key） | ✅ 已是测试友好接缝 |
| UI 控件点击自动化 | 无 `SimulateClick`/可点击约定；战斗 HUD 经 uGUI 事件桥接 | ⚠️ 需定义"测试句柄"约定 |
| 网络事件观察 | `NetworkTransport` 8 个事件、`MultiplayerRoomFlowController.StateChanged`（10 态）、`IGatewayConnection.RegisterPushHandler(opCode)` | ✅ 事件面齐全，⚠️ 分散，缺统一门面 |
| 全局事件总线 | `EventDispatcher`/`GlobalEventDispatcher`（core 包，API 完备：类型安全/优先级/SubscribeOnce） | ⚠️ **零消费者，是死基建**；需决策是否启用 |
| 流程编排 | `com.abilitykit.flow`：Sequence/ParallelAll/Race/If/Switch/Timeout/Finally/Do/WaitUntil/AwaitCallback + `FlowRunner.Step` + `FlowWakeUp` | ✅ **极佳的测试编排宿主**，⚠️ 仅代码编写，无数据驱动反序列化 |
| 故障注入 | `ShooterFaultRetryPolicy`（smoke 专用 internal，回调内 throw 模式） | ⚠️ 需抽到共享包 + 结构化 `IFaultInjector` |
| 确定性比对 | `MobaReplayDeterminismHarness`/`MobaReplayValidator`（逐帧 1mm 容差）、`ShooterDeterminismSpecRunner`（state hash） | ✅ 复用 |
| Draft 生成 | `MobaAcceptanceDraftGenerator`（从配置自动生成期望草案） | ✅ AI 扩展测试的天然入口 |
| 门禁/CI | `tools/test-gates.json`（24 门禁）+ `run_test_gate.ps1` + GH Actions + 定时 cron + self-hosted runner | ✅ 加一个 `dsl-regression` 门禁即可 |
| 平台/界面 | 服务端 `GatewaySkillAcceptanceArtifacts.cs` 只读 HTTP API + AdminConsole（`App.vue`） | ⚠️ 只读浏览，需扩展为可编写/触发 |
| 基线/Approval | 仅 Shooter 协议 fixture 有 golden | ⚠️ 缺项目级"锁定基线、漂移报警"机制 |

**一句话结论**：地基（确定性 runner、trace 验收、draft 生成、门禁、Flow 编排、BattleTestScript 跨载体模型、UI/网络接缝）**基本都在**；缺的是把它们**收敛成统一 DSL 体系**，并补三块新工作：① 验收核心去 Unity/MOBA 化；② Unity 引擎内 UI+网络 carrier；③ 模板与平台。

## 4. 总体架构

一条贯穿的流水线：**编写 → 规范 IR → Carrier 执行 → 判定 → JSON 产物 → 平台/CI**。

```text
┌─────────────────────────────────────────────────────────────────────────┐
│  编写层（Authoring，"人的语气"）                                          │
│   简洁 YAML/文本  ·  Web 表单(AdminConsole 扩展)  ·  Unity Editor Window │
│   AI draft 生成器（读配置→生成候选场景）                                  │
└───────────────────────────────┬─────────────────────────────────────────┘
                                │ 编译/导出
                                ▼
┌─────────────────────────────────────────────────────────────────────────┐
│  规范场景 IR（TestScenario，唯一契约）                                    │
│   meta · extends(模板) · clients[] · setup · timeline ·                   │
│   uiActions · networkWatches · faults · expectations(trace/state/context │
│   /network/ui) · controllability(seed/tickRate/waitFor/timeout/retry)     │
└───────────────────────────────┬─────────────────────────────────────────┘
                                │ 按 carrier 字段分发
          ┌─────────────────────┼──────────────────────┐
          ▼                     ▼                      ▼
   ┌──────────────┐     ┌───────────────┐     ┌────────────────────┐
   │ 逻辑仿真载体  │     │ Unity 客户端载体│     │ 多客户端网络拓扑载体 │
   │ BattleTest   │     │ UnityBattle   │     │ smoke harness 泛化  │
   │ ScriptRunner │     │ TestScript    │     │ (真实 server+多端)  │
   │ + harness    │     │ Driver +      │     │ + IFaultInjector    │
   │ (无头/确定)   │     │ ITestUiDriver │     │                     │
   │              │     │ + NetObserver │     │                     │
   │ 位级确定      │     │ + Flow 编排    │     │ 意图确定+容差        │
   └──────┬───────┘     └───────┬───────┘     └──────────┬─────────┘
          └─────────────────────┴────────────────────────┘
                                ▼
┌─────────────────────────────────────────────────────────────────────────┐
│  统一判定器（Verifier）                                                   │
│   trace 树匹配 · state/context 断言 · network 事件断言 · ui 状态断言       │
│   passed = 所声明断言族全部成立（合取）                                    │
└───────────────────────────────┬─────────────────────────────────────────┘
                                ▼
┌─────────────────────────────────────────────────────────────────────────┐
│  JSON 产物（三级通用货币）                                                │
│   *_summary.json(case) · batch_summary.json(suite) · gate-summary.json(CI)│
└───────────────────────────────┬─────────────────────────────────────────┘
                                ▼
        AdminConsole 展示 · CI 门禁 · 专属机定时回归 · AI 巡检
```

三种载体的确定性 regime 不同，但**共用同一份 IR 与同一套验收 JSON**，这是统一性的关键。

## 5. 三层 DSL 模型

| 层 | 职责 | 形态 | 稳定性 |
|---|---|---|---|
| **编写层** | 人/AI 用接近自然语言写场景 | YAML 短文 + Web 表单 + Editor Window | 可演进，容错 |
| **规范 IR** | 唯一契约，执行器与验收器共同消费 | JSON（`TestScenario`） | 稳定，schema 版本化 |
| **执行语义** | 把 IR 翻译成可运行的控制流 | 载体内：逻辑层直接 tick；引擎内编译为 Flow 节点树 | 载体私有，可重构 |

**为什么分三层**：编写层可以随时换皮（YAML→表单→AI），不破坏执行；规范 IR 是冻结核，新动词只在 IR 加字段；执行语义随载体演进。这正是"任意扩展但不破坏验收"的结构保证。

### 5.1 编写层示例（YAML，读起来像流程描述）

```yaml
scenario: 小乔1技能命中残血目标并击杀
extends: [templates/standard_1v1_match.yaml]   # 复用标准对局主流程模板
seed: 1337
carrier: unity-client                            # 在引擎内跑（带界面+网络）
clients:
  - { role: caster, hero: xiaoqiao, team: 1 }
  - { role: target, hero: dummy,    team: 2, setup: [set_attr(hp, 200)] }

steps:
  - waitFor: screen(battle_hud)                  # 等战斗界面就绪
  - at 0ms:   cast(caster, skill=1, target=target)
  - waitFor: network(snapshot_applied)           # 等权威快照到达再断言
  - at 1200ms: wait

expect:
  - trace: SkillCast(caster, skill=1)
  - trace: DamageApply(target)
  - state: target.hp lte 0                        # 击杀
  - network: snapshot_received at_least 1
  - ui: panel(kill_feed).visible
```

YAML 编译器把上述文本翻译成 §6 的规范 IR JSON。编写层只负责"好写"，不参与执行。

### 5.2 执行语义层（引擎内载体用 Flow 宿主）

引擎内载体是异步、事件驱动的（要等界面打开、等网络包），**不该用固定毫秒轮询**。`com.abilitykit.flow` 已具备完整节点集，是天然宿主：

- `waitFor: screen(battle_hud)` → `WaitUntilNode(() => ui.IsOpen("battle_hud"))`
- `waitFor: network(snapshot_applied)` → `AwaitCallbackNode` 订阅 `NetworkTransport.StateSyncSnapshotPushed`，到包即完成，并由 `FlowWakeUp` 事件驱动重泵（无需轮询）
- `at 1200ms: wait` → `WaitSecondsNode`
- 超时保护 → 外层 `RaceNode(TimeoutNode(30), 主流程)`，避免卡死

逻辑层载体（无头）则保持现有 `BattleTestScriptRunner` 的固定 tick 循环——它本就是确定的，不需要 Flow。**Flow 只用于引擎内/多客户端这类异步载体**。

## 6. 规范场景 IR（TestScenario）

以 [DSL 指南](MobaAcceptanceScenarioDSLGuide.md) 的 scenario 结构为种子，去 MOBA 化并扩展。顶层结构：

```json
{
  "schemaVersion": 2,
  "caseId": "xiaoqiao_skill1_kill_lowhp_target",
  "description": "小乔1技能命中残血目标并击杀",
  "tags": ["smoke", "network-safe"],

  "carrier": "unity-client",
  "worldId": "moba_1v1",
  "seed": 1337,
  "tickRate": 30,
  "accelerated": true,

  "extends": ["templates/standard_1v1_match.json"],
  "params": { "map": "headless_1v1" },

  "clients": [
    { "role": "caster", "alias": "caster", "heroId": 1001, "teamId": 1, "skills": [10010101] },
    { "role": "target", "alias": "target", "heroId": 1002, "teamId": 2, "skills": [],
      "setup": [ { "action": "set_attr", "property": "hp", "value": 200 } ] }
  ],

  "setup":    [ /* 载体无关的初始化动作，沿用现有 setup action 词典 */ ],
  "timeline": [ /* 现有 atMs/press/release/wait/cast_skill 词典，向后兼容 */ ],

  "uiActions":      [ /* 新增：UI 驱动动作族，仅引擎内载体有效 */ ],
  "networkWatches": [ /* 新增：声明要观察/断言的网络事件 */ ],
  "faults":         [ /* 新增：故障注入声明 */ ],

  "expectations": {
    "trace":   [],   // 现有 mustContain/mustNotContain/relationships
    "state":   [],   // 现有 stateExpectations
    "context": [],   // 现有 contextExpectations
    "network": [],   // 新增
    "ui":      []    // 新增
  },

  "controllability": { "defaultTimeoutMs": 30000, "retry": { "count": 0 } }
}
```

**向后兼容**：旧版扁平 `mustContain/stateExpectations` 字段仍可直接执行（runner 优先读 `expectations`，回退到扁平字段），与 [DSL 指南 §4](MobaAcceptanceScenarioDSLGuide.md) 的兼容策略一致。

### 6.1 字段增量说明

| 新增字段 | 语义 | 适用载体 |
|---|---|---|
| `carrier` | `logic-sim` / `unity-client` / `multi-client`，决定执行路径与确定性 regime | 全部 |
| `clients[]` | 多客户端拓扑；每个 client 一个角色 + 可选 setup 片段 | unity-client / multi-client |
| `extends` / `params` | 模板继承与参数化（见 §10） | 全部 |
| `uiActions` | UI 驱动动作族（见 §8.3） | unity-client |
| `networkWatches` | 声明观察的网络事件 + 计数/匹配约束（见 §8.2） | unity-client / multi-client |
| `faults` | 故障注入声明（见 §8.4） | 全部（逻辑层为 mock 注入） |
| `expectations.network` | 对网络事件的发生/顺序/计数的断言 | unity-client / multi-client |
| `expectations.ui` | 对界面状态（面板开合、控件可见/可用）的断言 | unity-client |
| `controllability` | 默认超时、重试策略 | 全部 |

## 7. 执行上下文 Carrier

| Carrier | 入口 | 确定性 | 用途 |
|---|---|---|---|
| **logic-sim** | `BattleTestScriptRunner` + headless harness | 位级确定（seed/tickRate/加速） | 技能/伤害/buff/位移等纯逻辑验收；最快、最稳、CI 友好 |
| **unity-client** | `UnityBattleTestScriptDriver` + `ITestUiDriver` + `ITestNetworkObserver`，Unity batchmode 或 Editor | 意图确定 + 容差 | 带界面、带输入、监听网络的完整流程；界面逻辑、表现层、客户端网络恢复 |
| **multi-client** | smoke harness 泛化（真实 server + 多端进程） | 意图确定 + 容差 + 确定性快照比对 | 多人联机流程、重连、房间状态机、权威快照收敛 |

`carrier` 字段决定 IR 编译成哪种执行树。三种载体产物格式统一（§11）。

## 8. Unity 引擎内 UI+网络集成测试工具（核心新增）

本节是把现有接缝拼成"引擎内白盒测试工具"的具体设计。所有驱动器都是**测试侧组件**，落在新测试包（建议 `com.abilitykit.testing.integration` 或扩展现有 testkit），不进生产程序集。

### 8.1 输入驱动：复用跨载体脚本模型

- `BattleTestScript` + `IBattleTestScriptDriver` 已是平台中立的脚本模型，注释明确支持多载体适配。
- Console 侧已有 `ConsoleBattleTestScriptDriver`。**新增 `UnityBattleTestScriptDriver`**：实现 `IBattleTestScriptDriver`，把 `Move/Skill` 步骤路由到 `IBattleHudInputSink`（`BattleContext` 已实现）——即 `ctx.SetHudMove(dx,dz)` / `ctx.SubmitHudSkillClick(slot)`，绕过 uGUI 直接写入 `BattleHudInputState`，与真实 HUD 输入走同一条 `BattleInputFeature.Tick` 消费链路。
- 这样**同一份 timeline 在 Console / Unity / 未来 headless-view 上输入语义一致**。

### 8.2 网络观察门面：`ITestNetworkObserver`

现状的网络事件分散在 `NetworkTransport`（8 个事件）、`MultiplayerRoomFlowController.StateChanged`、`IGatewayConnection.RegisterPushHandler`。**新增统一观察门面**，测试侧订阅这些既有事件，对外暴露按名字订阅的统一 API：

```csharp
public interface ITestNetworkObserver {
    IEventProbe On(string eventName);            // "connection_established" / "snapshot_pushed" / "auth_failed" / ...
    IEventProbe OnPush(uint opCode);             // 按 opCode 订阅任意 server push
    IEventProbe OnRoomState(params string[] states); // 房间状态机迁移
}
```

`networkWatches` 与 `expectations.network` 绑定到这些 probe 的计数/顺序/匹配。**决策：不复活死掉的 `GlobalEventDispatcher`**——它零消费者、强改生产风险高；测试门面直接订阅既有 C# 事件即可（符合"测试基建与生产解耦"原则）。`GlobalEventDispatcher` 仅在未来需要观察"没有自然 C# 事件的游戏逻辑事件"时，作为可选的补充发布通道，由具体事件按需启用。

### 8.3 UI 驱动：`ITestUiDriver` + 测试句柄约定

分两类界面：

**(a) 大厅/元界面（`UIManager` 管理的 panel）**：`UIManager` 已有 `Open/Close/TryGet/IsOpen` 按 string key 的接缝。`ITestUiDriver` 直接映射：

| DSL 动作 | 实现 |
|---|---|
| `ui.open(key)` | `uiManager.Open(key)` |
| `ui.close(key)` | `uiManager.Close(key)` |
| `ui.expect_open(key)` | `uiManager.IsOpen(key)` 断言 |
| `ui.click(panel, handle)` | 见"测试句柄约定" |

**(b) 控件点击（测试句柄约定，新增）**：uGUI 控件目前无统一可点击接口。**定义轻量约定**：需要被测试的 `UIPanel`/`UIWidget` 可选择实现 `ITestInteractable`，按 string handle 暴露命名交互点（按钮 id、输入框 id）。`ITestUiDriver.click(panel, "btn_start")` 经此约定触发，**不靠 uGUI 反射**。

```csharp
public interface ITestInteractable {
    void TestInvoke(string handle);        // 触发命名控件
    object TestRead(string handle);        // 读控件状态（文本/可用性）
}
```

**(c) 战斗 HUD**：不经 `UIManager`，直接走 §8.1 的 `IBattleHudInputSink`。

设计取舍：优先"绕过 uGUI 走逻辑接缝"（更稳、更快、不依赖布局），仅在**必须验证界面表现本身**时才走测试句柄/真实控件。这与"白盒"定位一致——白盒的意义正是能直达逻辑，而非模拟鼠标坐标。

### 8.4 故障注入：抽 `IFaultInjector`

把 `ShooterFaultRetryPolicy`（现 smoke 专用 internal）抽到共享测试包，提供结构化、可确定性调度的故障注入：

```json
"faults": [
  { "at": "before_reconnect", "kind": "io_exception", "count": 2 },
  { "at": 5000, "kind": "kill_connection" }
]
```

`at` 支持事件锚点（`before_reconnect` / `on_snapshot`）或时间点。逻辑层载体用 mock 注入；网络载体用真实的断连/延迟。

## 9. DSL 关键字目录（动词族）

延续 [DSL 指南](MobaAcceptanceScenarioDSLGuide.md) 已有动词（`setup`：spawn_actor/set_attr/move_to/add_buff/remove_buff/wait/tick；`timeline`：press/hold/release/cancel/cast_skill/wait），新增以下族：

| 族 | 关键字 | 语义 | 适用载体 |
|---|---|---|---|
| **输入** | `cast`, `move`, `aim`, `release`, `cancel` | 经 `IBattleHudInputSink`/`IInputFeature` 注入 | 全部 |
| **UI 驱动** | `ui.open/close/click/fill`, `waitFor(screen)` | 经 `ITestUiDriver` | unity-client |
| **网络观察** | `waitFor(network.xxx)`, `capture(network)` | 经 `ITestNetworkObserver` | unity/multi-client |
| **等待/控制** | `wait(ms)`, `tick(n)`, `waitFor(state谓词)`, `if`, `repeat/until`, `race(timeout,…)` | 经 Flow 节点（引擎内）或 tick 循环（逻辑层） | 全部 |
| **断言** | `assert.trace/state/context/network/ui` | 五族断言，合取判定 | 全部 |
| **故障** | `injectFault(at, kind, count)` | 经 `IFaultInjector` | 全部 |
| **作用域** | `seed`, `tickRate`, `accelerated`, `carrier` | 确定性与载体声明 | 全部 |

**扩展纪律**（新增动词）：先在 IR 加字段 → 编写层加语法 → 载体内加 driver 分支 → 更新本目录表。不动既有动词语义。

## 10. 模板与复用

目标：主流程沉淀为模板，测试在模板上插入自己的步骤。

### 10.1 模板模型

- 模板是**部分场景**（命名步骤序列 + 期望 + 参数声明），存为 `templates/*.template.json`。
- 场景用 `extends` 引用模板，通过**阶段锚点**插入/覆盖：

```yaml
extends: [templates/standard_1v1_match.yaml]
inject:
  after: hero_picked        # 在模板的 hero_picked 阶段后插入
  steps:
    - cast(caster, skill=1, target=target)
    - waitFor: network(snapshot_applied)
override:
  - replace: loadout_default   # 替换模板的某个阶段
    with: [pick_hero(xiaoqiao)]
params: { map: headless_1v1 }
```

### 10.2 阶段即天然模板边界

`MultiplayerRoomFlowController` 的状态机（`LoggingIn/CreatingRoom/JoiningRoom/InLobby/LoadingAssets/WaitingForBattle/InBattle/...`）天然是阶段边界。**标准模板 = 登录→匹配→选英雄→加载→开战**这条主链；每个测试只需补"开战后的 timeline + 断言"。这与 [多人 SDK 记忆] 里 staged room flow 的分阶段 API 同构。

### 10.3 复用边界

- 模板只描述**流程骨架与公共期望**，不绑定具体英雄/技能（用 `params` 注入）。
- 测试可在任意阶段锚点 `inject` / `override` / `append`。
- 模板版本化（`schemaVersion` + 模板自身版本），避免上游改模板悄悄破坏下游测试。

## 11. 验收结果体系（统一 JSON）

直接复用现有 schema 并扩展 coverage 维度：

- **case**：`<caseId>_summary.json` + `<caseId>_trace.jsonl`（`MobaAcceptanceTraceExporter.BuildSummary`）。
- **suite**：`batch_summary.json`（`MobaAcceptanceRunner.BuildBatchSummary`）。
- **gate**：`gate-summary.json`（`run_test_gate.ps1`）。

`result.passed` 泛化为"所声明断言族的合取"：

```
passed = trace断言 && state断言 && context断言 && network断言 && ui断言
```

未声明某族时不计入（向后兼容）。`coverage` 块按族分别给出 `matched/missing/unexpected` 计数与明细，便于平台展示 diff。trace 树、网络事件时序、UI 状态快照都进 `diagnostics`，供失败定位。

新增字段建议保持 [DSL 指南 §12](MobaAcceptanceScenarioDSLGuide.md) 列出的稳定关联键（`battleId/worldId/caseId/rootId/nodeId/frame/actorId/skillId`），让网络事件与 UI 动作也能对齐到同一坐标系。

## 12. 平台与工具链

| 组件 | 现状 | 演进 |
|---|---|---|
| **AdminConsole** | 只读 artifact 浏览（`App.vue` + `adminConsoleStore` + 服务端 `GatewaySkillAcceptanceArtifacts.cs`） | 扩展为**可编写**：表单/树形编辑场景 → 导出规范 IR；保留 [DSL 指南 §12] 的安全约束（只提交 scriptId/ciWorkflowId，服务端 allow-list，记录 operationId/log） |
| **Runner CLI** | 各载体零散入口 | 统一 `dotnet` CLI + Unity `-executeMethod` 入口，入参 = 场景目录，产物 = 标准 artifacts |
| **JSON Schema** | 无 | 为 `TestScenario` 提供 schema，编写期校验 + 编辑器提示（[DSL 指南 P2]） |
| **Draft 生成器** | `MobaAcceptanceDraftGenerator`（从配置生成期望草案） | 扩展为 AI 巡检入口（§13） |
| **门禁** | `test-gates.json` 24 门禁 | 新增 `dsl-regression` 门禁：批量跑 `templates/** + scenarios/**`，产物进 CI |

执行入口安全边界（沿用 [DSL 指南 §12](MobaAcceptanceScenarioDSLGuide.md)）：浏览器只提交受控 `scriptId`；服务端 allow-list；共享环境优先走 CI；Gateway 默认只读。

## 13. 持续回归与 AI

| 能力 | 落地 |
|---|---|
| **专属机持续回归** | 一个 self-hosted runner（CI 已有 Unity PlayMode 用 self-hosted 的先例），按 schedule cron 跑 `dsl-regression` 门禁。确定性 seed 保证可信。 |
| **AI 定时巡检** | Draft 生成器定时扫配置变更（skills/effects/buffs/triggers 表）→ 生成候选场景草案 → 自动跑 → 通过的草案自动开 PR 给人 review → promote 为 `contract`/`golden`。失败/漂移的进报告。 |
| **Golden/Approval** | 新增"首次运行即基线、后续 diff 报警"模式：手写期望管正确性契约，approval 管行为漂移监控，互补。 |

这条闭环让"任意扩展测试"主要变成 review 而非从零写——正是"人的语气"最高效的用法。

## 14. 落地路线图

按依赖排序。**Phase 0 是地基**，与 [MOBA 视图层正式化计划](MobaViewRuntimeBattleFormalizationPlan.md) 及记忆中"session 层去 ScriptableObject 墙、扩 dotnet 覆盖"的工作同向，是本体系的前提。

| 阶段 | 目标 | 关键工作 | 验收 |
|---|---|---|---|
| **P0 地基** | 验收核心去 Unity/MOBA 化 | `MobaAcceptanceModels`/`TraceExporter` 脱离 `UnityEngine.JsonUtility`（换 System.Text.Json），整数 ID 抽象为玩法无关别名；`MobaAcceptanceExpectation` scenario → `TestScenario` IR | 纯 dotnet 跑通一个场景并产出 summary.json |
| **P0 进度（2026-08-14）** | seam #1 已切 | dotnet 判定层已落地：`src/AbilityKit.Demo.Moba.Acceptance`（链接真实 `MobaAcceptanceModels.cs` + `AcceptanceJsonCodec` STJ + harness-free `AcceptanceVerifier`）+ `.Tests`（6 测试绿，`Gate=MobaAcceptanceDotnet`） | 判定不再依赖 harness；STJ 替换 JsonUtility 已验证；11 个真实 expected.json 全部可被 STJ 加载 |
| **P1 统一载体** | 三载体归一 | 抽 `ITestNetworkObserver` 门面；抽 `ShooterFaultRetryPolicy` → 共享 `IFaultInjector`；统一判定器接入 network 断言族；`dsl-regression` 门禁 | logic-sim 跑现有 11 个 expected.json 全绿 + 产物一致 |
| **P2 Unity 集成载体** | 引擎内 UI+网络工具 | `UnityBattleTestScriptDriver`（`IBattleHudInputSink`）；`ITestUiDriver`（`UIManager` + `ITestInteractable` 约定）；timeline→Flow 编译（`WaitUntil`/`AwaitCallback`/`Race(Timeout)`）；网络观察接线 | 一个"登录→开战→放技能→断言网络快照"引擎内场景跑通 |
| **P3 模板与平台** | 复用 + 可视编写 | 模板/继承/锚点模型；YAML 编写层编译器；AdminConsole 可编写扩展；JSON Schema；Editor Window | 标准对局模板被 ≥3 个测试复用 |
| **P4 AI/持续回归** | 自动化闭环 | Draft 生成器接配置巡检；self-hosted schedule；Golden/Approval 模式 | AI 周期产出并被 review promote 的草案 ≥1 |

### 14.1 P0 解耦 seam 级执行序列（harness 去 Unity 化）

`MobaSkillConfigTestHarness`（`moba.view.runtime`，Unity+NUnit+Entitas）是把验收从 Unity 搬到 dotnet 的真正阻断项。按依赖顺序逐 seam 切，每 seam 独立可发布 + 可测：

| Seam | 耦合 | 动作 | 风险 | 状态 |
|---|---|---|---|---|
| **#1 判定层** | `BuildSummary` 收 `MobaSkillConfigTestHarness` | 抽 `AcceptanceVerifier(expectation, records)`，harness-free；STJ codec 替 JsonUtility | 低（纯增量，零 Unity 风险） | ✅ 完成 |
| **#2 trace 契约** | `CaptureTraceRecords(harness,…)` 内联 harness | 定义 `ITraceSource`（dotnet 侧已落地 `FileTraceSource`/`NullTraceSource`）；harness 侧实现待 Seam #4 | 低 | 🟡 dotnet 侧完成；harness 侧待 #4 |
| **#3 trace 捕获纯化** | capture 里 `harness.Config` 反查名称/label | 抽 `TraceCapture.FromTree(trace, frameTime)` 纯函数，label 富化改为可选后置 | 低（`MobaTraceKind` 在 `moba.runtime` 已 dotnet 可编译） | 待办 |
| **#4 世界引导（大头）** | `Ability.Host.Extensions.Moba.CreateWorld` + `View.Config` + `EntitasAdapters` + `UnityEngine` | **首选 D1**：验收复用**已 dotnet 可跑的逻辑/console 世界**（`ConsoleBattleBootstrapper`，Entitas+services，无 view），加 trace 发射 + alias 装配；harness 降为逻辑世界的薄适配器。备选 D2：逐项剥离 view harness 的 Unity 依赖 | 高（D1 走通则小；D2 与 view 栈缠斗）。即记忆里的"session 层去 ScriptableObject 墙" | 🟡 D1 可行(架构预接缝)+第一切片已验证 |
| **#5 setup/timeline 驱动** | `MobaAcceptanceSetupActionExecutor` / `ExecuteTimeline` 调 harness API | 跟 Seam #4 一起搬（自身无 Unity 依赖，只调 harness） | 低 | 待办 |
| **#6 NUnit 解耦** | runner 用 `Assert.*` | 改为返回 summary/抛异常（dotnet 层已是返回式） | 低 | 待办 |

**即时 CI 价值**：仅 #1–#3 落地，即可在 CI 用 dotnet 判定器对 Unity 跑出的 `trace.jsonl` 做回归 diff（不依赖模拟器去 Unity 化）。#4 是唯一的大块，其余皆小。

**进展（2026-08-14）**：Seam #2 dotnet 侧 `ITraceSource` + `AcceptanceBatchRunner` 已落地（`AbilityKit.Demo.Moba.Acceptance`，8 测试绿）。批量 runner 扫期望目录、每用例经 `ITraceSource` 取 trace → `AcceptanceVerifier` → 汇总成生产同款 `batch_summary.json`（11 真实用例 76ms 跑完）。**已挂为 `moba-acceptance-dotnet` P1 门禁**（`tools/test-gates.json` + `.github/workflows/abilitykit-test-gates.yml` 的 job + 门禁规范文档 §3；dotnet-test step 按 `Gate=MobaAcceptanceDotnet` 过滤；contract validator 0 失败、本地实跑 8/8 绿 3.6s）。这是 `dsl-regression` 门禁的种子，行为覆盖随 trace fixture 增长（needs-trace 用例不拉低门禁）。**真实 trace 基线捕获（2026-08-14）**：`tools/capture_moba_acceptance_traces.ps1` 经 Unity 批量入口 `MobaAcceptanceWebCommand.RunDirectoryFromCommandLine` 把真实 `<caseId>_trace.jsonl` 灌入 `src/AbilityKit.Demo.Moba.Acceptance.Tests/Traces/`；`CompositeTraceSource` 真实 `Traces/` 优先、合成 `Fixtures/` 兜底——跑一次脚本 + 提交即可把 needs-trace 用例升级为真实回归判定（流程与纪律见 `Traces/README.md`）。**Seam #4（D1）可行性已验证 + 第一切片落地（2026-08-14）**：侦察确认 D1 可行且架构本就预接缝——console 世界 = 真实逻辑世界（`MobaWorldBootstrapModule`，纯 dotnet 零 Unity）、`MobaTraceRegistry` 在 console 跑技能时自动填充 trace 树（非 harness 专属）、host 扩展装配路径是纯逻辑（harness 的 Unity 耦合来自 JsonUtility/NUnit/View.Config 而非世界装配）。新增 `LiveSimTraceSource : ITraceSource`（`src/AbilityKit.Demo.Moba.Tests/Smoke/`，复用 console smoke 管线跑真实 sim → 读 trace → `MobaAcceptanceTraceRecord[]`），2 个闭环测试绿：**期望 → 真实 dotnet sim → trace → `AcceptanceVerifier`**，全程无 Unity。剩余切片：**切片 2（setupActions）已落地（2026-08-14）**——`LiveSimSetupActionExecutor`（`src/AbilityKit.Demo.Moba.Tests/Smoke/`，五动词 spawn_actor/set_attr/move_to/add_buff/remove_buff + wait/tick 忠实移植自 harness 512-611 行，直接 Resolve console 世界服务，端到端测试绿）；剩完整 timeline 动词映射（切片 3）与 multi-actor aliasing（切片 4），体量 SMALL-MEDIUM。**切片 3（timeline 动词）已落地（2026-08-14）**——`LiveSimTimelineRunner`（镜像 `MobaAcceptanceRunner.ExecuteTimeline/ExecuteAction`：atMs 排序推进 sim 时钟；wait/tick → tick；环境动词转 setup executor（切片 2）；技能动词 press/release/hold/cancel/cast_skill/skill_input → 富 `SkillInputEvent`（含 press-aim 归一化）→ `PlayerInputCommand` → `IMobaInputCoordinator.TrySubmit`，与 harness 完全同一提交路径；提交后 Tick(1) 镜像 `AdvanceAcceptedSkillInput`）。端到端测试绿：timeline press → 真实 SkillCast/EffectExecution trace。LiveSim 全家 4/4 绿。**剩切片 4（multi-actor aliasing/世界引导）**，随后可做收敛件 `LiveSimAcceptanceScenarioRunner`（期望 → actors 装配 + setup + timeline → live trace → `AcceptanceVerifier`，needs-trace 清零路径）。**收敛件已落地（2026-08-14，切片 4 走了捷径）**：`LiveSimAcceptanceScenarioRunner`（`Moba.Tests/Smoke/`）——actors[] 里带 playerId 的绑 console 本地玩家（`DisableActorBrain` 抑制 AI 游走）+ 其余 spawn_actor 合成，无需完整 multi-actor 世界引导；期望坐标经**锚点平移**（默认 (-15,0,0)，console 原型图的开阔走廊）适配 console 地图（原点区有 Center Blocker、|x|≥17.5 是墙——期望坐标本是为 harness 空旷测试世界写的）。**已实证（plumbing 级）**：SkillCast/EffectExecution/全部 expectedActions/BuffApply + dash 全程 8 单位直线穿过 target。**hit-chain 根因与修复（2026-08-14）**：真实期望 `skill_10010101_scenario` 的完整 live verdict 已跑通。根因是 **console 配置漂移**——`trigger_source_manifest.json` 从 `skills` 类别加载 trigger，而 `Configs/ability/triggers/skills/trigger_10010101.json`（及 hit trigger 10010111）是陈旧副本（缺 `pass_through_walls`/`motion_group_id`/`hit_trigger_plan_id`，是旧 `shoot_projectile` 设计；旧 dash 默认约束只扫 WorldMask 不扫 Unit，故穿过单位不判 hit）。**修复 = 从 Unity 权威副本同步这两份 `skills/` 配置**。排障链（供未来参考）：几何轨迹采样 → 脑抑制 → Collider/CollisionId 组件 → 碰撞世界查询（UnitMask 可查到 target）→ 配置清单→ 定位到陈旧副本。**LiveSim 全家 5/5 绿，收敛测试已断言 `verdict.passed`。**

### 14.2 横切能力：工业化补齐（perf 回归 + 覆盖率）

| 项 | 现状 | 落点 | 状态 |
|---|---|---|---|
| **性能回归基线比对** | `AbilityKit.Benchmarking` 已产出 median/P95/P99 + determinism digest 的 `BenchmarkReport`；新增 `src/AbilityKit.Runtime.Benchmarks.Compare` 读 golden baseline vs 当前 run，按阈值判时间/分配 drift，输出 verdict + JSON + CI 退出码 | 跑完 `runtime-performance-measurement` 门禁的 benchmark 后调比对器；golden baseline 入仓 | ✅ 工具已建+验证（PASS/FAIL 退出码 0/1）；待挂门禁 |
| **覆盖率度量** | 暂无 | dotnet 测试加 Coverlet 收集、按模块报告；先在白盒验收层（`AbilityKit.Demo.Moba.Acceptance.Tests`）试点 | ⏳ 待办 |

perf 比对用法（挂门禁时包一层 ps1）：

`dotnet run --project src/AbilityKit.Runtime.Benchmarks.Compare -- --baseline <golden.json> --current <run.json> [--metric mean|median|p95|p99|max] [--threshold-pct 15] [--alloc-threshold-pct 20] [--strict-digest] [--out <path>]`

## 15. 风险与未决

| 风险 | 说明 | 缓解 |
|---|---|---|
| **P0 去 Unity 化** | JsonUtility + DTO + 判定已验证可移植（spike + `AcceptanceVerifier` 库，成本小）；真正大头是 `MobaSkillConfigTestHarness` 的世界引导（§14.1 Seam #4） | 优先走 D1：验收复用已 dotnet 可跑的逻辑/console 世界，harness 降为薄适配器；与"session 层去 ScriptableObject 墙"合并 |
| **`GlobalEventDispatcher` 是死基建** | 零消费者，是否启用需决策 | 本设计选"测试门面直订阅既有 C# 事件"，不复活；按需才补发布 |
| **UI 测试句柄约定是新增组织成本** | 需 panel 作者 opt-in `ITestInteractable` | 从被测面板逐步铺开；优先用逻辑接缝绕过 uGUI |
| **多客户端载体非位级确定** | 真实网络/时序 | 明确容差 regime；关键路径用 `MobaReplayValidator` 逐帧比对兜底 |
| **Flow 仅代码编写** | 场景→Flow 需新增数据驱动反序列化 | 仅引擎内载体需要；逻辑层不走 Flow，范围可控 |
| **平台可编写入口安全** | Web 触发执行有滥用风险 | 严格沿用 [DSL 指南 §12] allow-list + operationId 审计 |

## 16. 关键实现文件索引

| 文件 | 角色 |
|---|---|
| `MobaAcceptanceModels.cs` | 规范 IR 的种子（→ `TestScenario`） |
| `MobaAcceptanceRunner.cs` | 场景执行入口（→ 统一 runner） |
| `MobaAcceptanceTraceExporter.cs` | 判定 + summary/trace 产物组装 |
| `MobaAcceptanceExpectationAssert.cs` | trace/state/context 断言（→ 加 network/ui 族） |
| `MobaAcceptanceDraftGenerator.cs` | Draft 生成（→ AI 巡检入口） |
| `BattleTestScript.cs` / `BattleTestScriptRunner.cs` | 跨载体脚本模型 + 确定性 tick |
| `IBattleTestScriptDriver` | 载体适配契约（→ 新增 Unity 实现） |
| `IBattleHudInputSink.cs` / `BattleContext.Input.cs` | Unity 战斗输入注入点 |
| `UIManager.cs` | 大厅/元界面 UI 导航接缝 |
| `NetworkTransport.cs`（8 事件） / `MultiplayerRoomFlowController.cs`（StateChanged） / `IGatewayConnection.cs`（RegisterPushHandler） | 网络事件观察源（→ `ITestNetworkObserver` 统一） |
| `com.abilitykit.flow`（`FlowGraph`/`FlowRunner`/`AwaitCallbackNode`/`FlowWakeUp`） | 引擎内载体编排宿主 |
| `com.abilitykit.triggering`（`TriggerPlanDsl`/`PredicateExprDsl`/`NumericExpressionCompiler`/`WithContinuous`/`WithPeriodic`/`[TriggerActionType]`/JSON source） | 测试 oracle/不变量监控引擎（§17.1） |
| `com.abilitykit.behavior`（`BehaviorManager`/`BehaviorRuntime`/`ABehaviorExecutor`/`BTreeBlackboardBridge`）+ BT 编辑器 | 自适应测试行为引擎（§17.2） |
| `EventDispatcher.cs` / `GlobalEventDispatcher.cs` | 死基建，按需启用 |
| `ShooterFaultRetryPolicy.cs` | 故障注入源（→ 抽 `IFaultInjector`） |
| `MobaReplayDeterminismHarness.cs` / `MobaReplayValidator.cs` | 确定性快照比对 |
| `GatewaySkillAcceptanceArtifacts.cs` + `App.vue`/`adminConsoleStore` | 平台（→ 可编写扩展） |
| `tools/test-gates.json` / `run_test_gate.ps1` / `.github/workflows/abilitykit-test-gates.yml` | 门禁/CI（→ 加 `dsl-regression`） |

## 17. 多范式测试组合：脚本时间线 + 触发器 + 行为树

测试本身是「条件→动作」「决策」「脚本」的混合问题。框架已有的两个成熟引擎正好提供后两种范式，与现有 timeline（确定性脚本）互补，**本设计复用它们，而非新造**。

| 范式 | 本质 | 最适合 | 确定性 | 复用的现有引擎 |
|---|---|---|---|---|
| **脚本时间线** | 按 atMs 的确定性输入脚本 | 回归基线、位级复现、技能链路验收 | 位级（逻辑层） | `BattleTestScriptRunner` / `ExecuteTimeline` |
| **触发器 Triggering** | 声明式「条件→动作」反应式断言 / 运行期不变量 | 性质监控、事件驱动步骤、oracle | 反应式（随观测） | `com.abilitykit.triggering`（TriggerPlan/谓词表达式/JSON source） |
| **行为树 BT** | 程序式自适应决策 | 自适应对手、分支流程、多角色涌现 | 涌现（非位级） | `com.abilitykit.behavior` + BT 编辑器 |

一句话：**timeline 保证"可复现的回归"，trigger 抓"你没料到的性质违反"，BT 提供"真实/压力/探索覆盖"**。三者组合，不是互斥。

### 17.1 触发器作测试 oracle / 不变量监控器

TriggerPlan 的语义是"当谓词成立 → 执行动作"，这恰好是测试所需：

- **反应式断言**：`当 target.hp 跌破 50% → 断言 buff 2001 已施加`。
- **运行期不变量**：用 `WithContinuous`/`WithPeriodic` 跑连续 TriggerPlan，每 tick 检查 `无 actor.hp < 0`、`客户端状态哈希 == 服务端`、`弹道飞行不超过 N 帧`——一违反即记入 verdict。这是**手写期望抓不到的回归**的运行期性质检查器。
- **事件驱动步骤**：`on SkillCast / on ProjectileHit → 触发下一步`，替代固定 `atMs`（对真实网络/界面时序更鲁棒）。

复用点（零新基建）：
- **测试动作注册**：`[TriggerActionType("test_assert"/"test_record"/"test_fail", ...)]` —— 与现有 testkit 的 `test_wait` 完全同机制，测试侧声明新动作类型即可。
- **条件求值**：`NumericExpressionCompiler`（中缀→RPN）+ `NumericValueRefDsl.Var("actor","hp")`/`.Blackboard(...)`/`.Payload(...)` —— 直接拿来做测试条件，支持 `base*2+atk*0.5` 这类公式。
- **可读编写**：触发器用 JSON source 格式（`shoot_projectile`/`add_buff` 这类可读动作名），经 `TriggerPlanSourceParser` 编译——测试触发器与技能触发器**同一份可读 JSON**。

IR 扩展：`expectations.invariants[]`（连续 TriggerPlan，可读 JSON）、`triggerSteps[]`（事件驱动 timeline 替代）。违例进 `coverage.invariantViolations` 并入 verdict（§11 新增 `invariant` 断言族）。

### 17.2 行为树作自适应测试行为

BT 把"木偶 target"升级成"会走位/反击/用技能的真实对手"，并承担分支测试流程：

- **自适应对手**：测"技能能否命中走位目标"——target 跑走位/闪避 BT，远比固定时间线真实。
- **多角色涌现**：per-role BT（caster=进攻连招 BT，target=防守走位 BT）→ 两个小 BT 组合出大量涌现场景，免手写每个输入。适合压力/探索测试。
- **分支测试流程**：room 状态机已是 HFSM/BT 形态；测试流程本身也可写成 BT（`if 房间=Loading → X；if InBattle → Y`）。

复用点（零新基建）：
- `BehaviorManager`/`BehaviorRuntime`/`ABehaviorExecutor` 跑 BT；**`BTreeBlackboardBridge` 就是测试上下文**（别名、观测值、故障态都进黑板）。
- 条件节点（`ConditionHP`/`ConditionMP`）读游戏状态做测试分支；动作节点（`ActionSkill`/`ActionAttack`）直接当测试输入。
- **BT 编辑器 → 可视化测试流程编排工具**，复用现有编辑器基建，非程序员也能拖拽组合测试流程。

IR 扩展：`clients[].behavior`（引用 BT 资源 id + 黑板初值），与既有 `clients[].role` 并列。

### 17.3 组合：一份场景 = 脚本基线 + 触发 oracle + 自适应 BT

```yaml
scenario: 刺客秒杀走位法师
clients:
  - { role: caster, hero: assassin,  behavior: bt_assassin_combo }   # 进攻 BT
  - { role: target, hero: mage,      behavior: bt_mage_kite      }   # 走位 BT
timeline:
  - at 0ms: start_battle        # 仅定开战点，其余交给 BT
invariants:                      # 触发器：运行期性质
  - { when: "target.hp <= 0", within_ms: 3000, assert: "caster.alive == true" }  # 3s 内击杀且未受反击
  - { continuous: "all.actor.hp >= 0" }                                          # 不变量：无负血
expect:
  - trace: DamageApply(target)     # BT 驱动的伤害仍走 trace，自动被验收
  - invariant: all_passed
```

要点：**BT/触发器调用的技能/效果本就走 trace 树**，所以现有 trace 验收自动覆盖 BT/触发器驱动的行为——不需要为 BT 单造断言。

### 17.4 与判定的衔接 + 载体适用

- 三范式产物都汇入统一 verdict：timeline 的 trace/state、trigger 的 invariant 违例、BT 驱动的最终状态，统一按 §11 多断言族合取。
- 载体适用：logic-sim 跑 timeline + 触发器（tick 驱动 TriggerPlan，BT 可选）；unity-client / multi-client 跑全三范式（BT 经 behavior runtime，触发器经 triggering，timeline 经 driver）。
- **确定性边界要显式**：timeline 是位级契约（进 golden）；trigger/BT 是行为覆盖（进 contract/approval，带容差）。混用时在 case `tags` 标 `deterministic` / `explorative`，门禁分层处理。

## 18. 操作流程（端到端）

一名测试/开发从"想验证一个性质"到"它进持续回归"的完整流程：

```text
 ① 立意          ② 选范式              ③ 编写                 ④ 本地试跑
 明确要验证      确定性回归→timeline    YAML/表单/Editor       选载体（logic-sim 最快）
 的性质         性质监控→trigger       复用模板(§10)          看 summary.json + trace tree
                真实对抗→BT            AI draft 生成草案       + 不变量违例
                混合→组合(§17.3)              │                       │
                                                 └─────────┬─────────────┘
                                                           ▼
                              ⑤ 诊断  →  ⑥ 收敛为基线  →  ⑦ 进回归  →  ⑧ AI 巡检闭环
                         按 coverage.missingTraceNodes    定 contract/golden    加场景目录 + 挂     配置变更→draft→自动跑
                         / invariantViolations / 状态diff 漂移走 approval 锁基线  dsl-regression 门禁  →通过开 PR→review→promote
```

**① 立意**：先想清"验证什么性质"——技能配置链路 / 运行期不变量 / 业务流程 / 联机收敛。性质类型决定后面选哪个范式。

**② 选范式**（决策表）：

| 想验证 | 首选范式 | 载体 |
|---|---|---|
| 技能/伤害/buff 链路是否走对 | timeline + trace 验收 | logic-sim（位级） |
| 某性质在整个运行期恒成立 | trigger 不变量 | logic-sim / multi-client |
| 真实对抗下能否命中/取胜 | BT 自适应对手 | unity-client / multi-client |
| 登录→匹配→开战流程 | timeline + 房间状态机断言 | unity-client / multi-client |
| 联机收敛/重连 | timeline + network 断言 | multi-client |

**③ 编写**：YAML（最自然）编译到规范 IR；或 AdminConsole 表单 / Editor Window；主流程复用模板（§10），只写差异步骤；不确定时让 AI draft 生成草案再改。

**④ 本地试跑**：默认先用 logic-sim 载体（最快、最确定）；看 `*_summary.json` 的 `result`/`coverage`、trace tree、不变量违例清单。

**⑤ 诊断**：失败按优先级定位——`coverage.missingTraceNodes`（链路没走到）→ `invariantViolations`（性质被破）→ state diff（终值不对）→ network/ui 断言。区分"期望写错"还是"代码回归"。

**⑥ 收敛为基线**：确定性链路定 `contract`/`golden`；行为类（BT/联机）用 approval 锁当前输出为基线，后续 diff 报警。

**⑦ 进回归**：场景文件入场景目录；挂 `dsl-regression` 门禁（§12）；CI 每变更跑、专属机按计划跑（§13）。

**⑧ AI 巡检闭环**：技能/效果/buff 表变更 → draft 生成器扫影响面 → 生成/更新候选场景 → 自动跑 → 通过的开 PR → 人 review → promote 为 contract/golden。

**范式如何嵌入流程**：编写阶段声明 `invariants`/`behavior`（③）；试跑阶段三范式同跑，判定阶段不变量违例并入 verdict（⑤）；AI 巡检既生成 timeline 草案，也能从技能配置反推应加的不变量（⑧）。

## 19. 工业化对标与缺口

把本体系对照工业化游戏测试规范做一次审计。核心结论：**架构层完全符合业界共识（确定性仿真 + 数据驱动 fixture + 分层载体 + BT 机器人 + CI 门禁 = 战斗/联机游戏的标准做法），差异化与风险集中在 DSL 平台 / AI 层**——这部分是顺势的前瞻押注，非现行标准。

### 19.1 设计 ↔ 业界标准对标

| 本设计 | 业界对应 / 标准 | 符合度 |
|---|---|---|
| 分层载体（logic-sim / client / multi-client） | 游戏测试金字塔（unit→sim→功能→联机）；Unity EditMode/PlayMode；Unreal Gauntlet 多进程 | ✅ 标准 |
| 确定性重放 + 状态哈希（`MobaReplayDeterminismHarness`） | DST/lockstep 重放哈希回归；业界称其为 CI 回归"金标准" | ✅ 最佳实践 |
| 数据驱动技能验收（`expected.json` + trace 树） | 技能/卡牌/英雄 fixture 测试（炉石、Riot 英雄回归） | ✅ 标准模式 |
| 无头逻辑仿真 + BT 测试机器人 | 业界自动化机器人主流形态（Riot/Ubisoft/EA 都在用） | ✅ 标准，踩中趋势 |
| CI 门禁 P0/P1/P2 + 定时 + self-hosted | BVT / nightly 回归 + 自动化农场 | ✅ 标准 |
| 内容/数据校验门禁 | 线上服务型游戏数据校验流水线 | ✅ 标准 |
| triggering 作运行期不变量/oracle | DST 式"确定性 run 内断言不变量"（Antithesis 范式） | ✅ 思路成熟 |
| **白盒 DSL 编写层（YAML/UI）给测试/策划用** | 非标准——多数工作室用工程师写代码测试（xUnit/Gauntlet） | ⚠️ 前瞻，非标准 |
| **AI 从配置生成测试草案** | 前沿——EA SEED 深度 RL、Riot RL/模仿学习 bot、LLM 生成代理 | ⚠️ 领先，但顺势 |
| **三范式统一进一份 IR** | 工作室通常把重放回归/机器人/性质检查做成各自孤立系统 | ⚠️ 异常统一（差异化 + 过度工程风险） |

### 19.2 术语对齐（glossary）

| 本文术语 | 业界对应 |
|---|---|
| carrier（logic-sim / unity-client / multi-client） | 测试金字塔层（unit / sim / functional / MP） |
| trace 不变量 / invariant | property test / DST 不变量 |
| approval 基线 | baseline / golden 回归（视觉/行为） |
| golden `expected.json` | fixture / 数据驱动验收 |
| determinism digest / replay hash | 状态哈希回归（lockstep DST） |
| `dsl-regression` 门禁 | BVT / nightly regression |
| BT 测试机器人 | 自动化 bot（Riot / Ubisoft / EA） |
| perf 基线比对（§14.2） | 性能回归门禁（perf budget / regression gate） |

### 19.3 诚实缺口（相对工业化全套）

1. **性能回归门禁** — §14.2 工具已建，待挂 `runtime-performance-measurement` 门禁。
2. **覆盖率度量** — 缺；计划 dotnet 测试接 Coverlet，先在白盒验收层试点。
3. **视觉/UI 回归（截图 diff）** — 缺。
4. **Soak/长跑稳定性 + 崩溃泄漏检测** — shooter 有雏形，非通用门禁。
5. **线上不变量监控** — 目前仅在测试期跑；工业化做法还会在生产 build 埋不变量，与 CI 同机制。

### 19.4 方向校准建议

- **地基按工业标准收紧**：先把 P0/P1（确定性 sim 去耦合、fixture 验收、门禁）做到 Riot/Antithesis 水准——ROI 最高、最规范，也是平台层站住的前提。
- **DSL 平台层当"有节制押注"**：业界教训是自研 QA 工具常因工程师不采用而废；底层复用标准引擎、DSL 薄层、意图级屏蔽重构是对的方向，但要**早拿真实测试/策划试用**。
- **术语对齐工业词汇**（§19.2）降低团队与外部沟通成本。

### 19.5 参考

- [Unity Test Framework](https://unity.com/how-to/automated-tests-unity-test-framework) · [Unreal Automation Test Framework](https://dev.epicgames.com/documentation/unreal-engine/automation-test-framework-in-unreal-engine) · [Using Unreal Automation at Riot (GDC)](https://www.youtube.com/watch?v=3ftOkc-cA7U)
- [Riot: Automated Testing for League of Legends（~10 万用例/天）](https://www.riotgames.com/en/news/automated-testing-league-legends) · [Riot Games AI Practices](https://www.aitrace.org/company/riot-games)
- [EA SEED: 用深度强化学习做游戏测试](https://www.ea.com/seed/news/automated-game-testing-deep-reinforcement-learning) · [Ubisoft La Forge 学习型测试 bot](https://www.ubisoft.com/en-us/studio/laforge/news/4bmoklgq9Hynfa87doKFfQ/artificial-intelligence-through-learning-or-pavlovian-algorithm)
- [Antithesis: Deterministic Simulation Testing](https://antithesis.com/docs/resources/deterministic_simulation_testing/) · [WarpStream: DST for our entire SaaS](https://www.warpstream.com/blog/deterministic-simulation-testing-for-our-entire-saas) · [Gaffer on Games: Deterministic Lockstep](https://gafferongames.com/post/deterministic_lockstep/) · [BugNet: 调试 lockstep desync](https://bugnet.io/blog/how-to-debug-desync-in-deterministic-lockstep-games)

## 20. 维护建议

- IR 字段每次扩展同步更新本文 §6/§9 与 JSON Schema，并保持旧字段回退兼容。
- 新增载体/动词先落本文路线图对应阶段，再写实现，避免接缝漂移。
- 模板变更走 `schemaVersion` + 模板版本，下游测试通过 CI 报漂移。
- 测试侧组件（driver/observer/ui-driver/fault-injector）集中在专用测试包，**禁止**反向依赖进生产程序集。
- 与 [DSL 指南](MobaAcceptanceScenarioDSLGuide.md)、[门禁规范](AbilityKit测试门禁与批量回归规范.md)、[视图层正式化计划](MobaViewRuntimeBattleFormalizationPlan.md) 保持交叉引用同步。
