# MOBA 验收测试 Scenario DSL 流程与演进指南

## 1. 目标

本指南用于沉淀当前 MOBA 验收测试从单技能输入升级到 scenario 驱动 DSL 后的使用流程、数据约定、调试方式与后续优化路线。

核心目标：

1. 支持真实 `World`、service、actor、位置、技能携带和时间轴释放。
2. 保持旧版单技能 JSON 期望文件兼容。
3. 让测试数据可被未来 Unity Editor Window、Web 工具、CLI/CI 共同消费。
4. 让设计、策划、开发可以围绕同一份 scenario JSON 复现技能行为。
5. 逐步沉淀为可视化 authoring + 自动执行 + 报告分析的完整验收闭环。

## 2. 当前实现范围

当前已经完成的能力：

- 扩展 acceptance DTO，支持 `scenario`、`actors`、`setupActions`、`timeline`、`stateExpectations`、`contextExpectations`。
- 保留旧 JSON 格式：旧的 `input/config/mustContain/mustNotContain/relationships` 仍可执行。
- harness 支持按 scenario 创建多个 actor/player。
- 支持 actor alias 到 actorId/playerId 的映射。
- 支持时间轴按 `atMs` 驱动技能输入与等待。
- 输入模型支持 actor、target、position、direction、phase、slot。
- `setupActions` 已接入测试工程内命令执行器，支持第一阶段环境准备命令：`spawn_actor`、`set_attr`、`move_to`、`add_buff`、`remove_buff`、`wait` / `tick`。
- trace exporter 会输出 scenario 相关字段，便于后续报告工具读取。
- assertion 支持 trace/state/context 三类断言，并支持基础 buff 状态断言。
- `AbilityKit.Game.UnitTests.csproj` 已通过编译验证。

## 3. 推荐测试文件组织

建议按能力维度和技能维度逐步组织：

```text
Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/
  Expectations/
    skill_10010101.expected.json
    skill_10010101.scenario.expected.json
    hero_mage_basic_combo.scenario.expected.json
    projectile_hit_chain.scenario.expected.json
  UnitTest/
    MobaAcceptanceModels.cs
    MobaAcceptanceRunner.cs
    MobaAcceptanceSetupActionExecutor.cs
    MobaAcceptanceExpectationAssert.cs
    MobaAcceptanceTraceExporter.cs
    MobaSkillConfigTestHarness.cs
```

命名建议：

- 单技能旧格式：`skill_{skillId}.expected.json`
- 新 scenario 格式：`skill_{skillId}.scenario.expected.json`
- 复杂战斗场景：`{domain}_{case}.scenario.expected.json`

## 4. Scenario JSON 基本结构

推荐新测试使用如下结构：

```json
{
  "caseId": "skill_10010101_projectile_hit",
  "description": "验证技能 10010101 对目标释放后生成 projectile 并造成伤害",
  "worldId": "moba_headless",
  "tickRate": 30,
  "accelerated": true,
  "scenario": {
    "actors": [],
    "setupActions": [],
    "timeline": [],
    "stateExpectations": [],
    "contextExpectations": []
  },
  "mustContain": [],
  "mustNotContain": [],
  "relationships": []
}
```

顶层与 `scenario` 内部字段同时存在时，runner 会优先读取 scenario 内部字段；这样既方便未来把 scenario 作为独立对象，也兼容较扁平的 JSON 写法。

## 5. Actors 约定

`actors` 用于声明进入游戏时需要生成或绑定的对象。

示例：

```json
{
  "scenario": {
    "actors": [
      {
        "alias": "caster",
        "playerId": "p1",
        "teamId": 1,
        "heroId": 1001,
        "position": { "x": 0, "y": 0, "z": 0 },
        "skills": [10010101]
      },
      {
        "alias": "target",
        "playerId": "p2",
        "teamId": 2,
        "heroId": 1002,
        "position": { "x": 6, "y": 0, "z": 0 },
        "skills": []
      }
    ]
  }
}
```

推荐约定：

- `alias` 必填，用于后续 timeline、state、context 断言引用。
- `playerId` 用于玩家输入绑定，建议使用 `p1`、`p2`、`p3`。
- `teamId` 用于敌我关系、碰撞、伤害规则验证。
- `position` 建议始终显式填写，避免测试依赖默认出生点。
- `skills` 用于声明 actor 携带技能，slot 顺序从 1 开始。

## 6. SetupActions 约定

`setupActions` 用于进入游戏后、timeline 执行前做一次性准备。第一阶段命令只放在测试工程内执行：runner 负责识别 setup action，`MobaAcceptanceSetupActionExecutor` 负责分发命令，实际运行时变更继续委托 `MobaSkillConfigTestHarness` 与正式 runtime service。

当前支持命令：

| action | 语义 | 常用字段 |
|---|---|---|
| `wait` / `tick` | 推进测试世界时间 | `durationMs` |
| `spawn_actor` | 生成测试场景 actor / 召唤物 / 特殊实体，并注册 alias | `alias`、`kind`、`teamId`、`heroId`、`attributeTemplateId`、`unitSubType`、`mainType`、`sourceAlias`、`sourceKind`、`position` |
| `set_attr` | 修改 actor 属性 | `actorAlias`、`actorId`、`targetActorId`、`property`、`value` / `intValue` |
| `move_to` | 移动 actor 到指定位置 | `actorAlias`、`actorId`、`targetActorId`、`position` |
| `add_buff` | 对 actor 应用 buff | `targetAlias`、`actorAlias`、`sourceAlias`、`buffId`、`durationOverrideMs` |
| `remove_buff` | 从 actor 移除 buff | `targetAlias`、`actorAlias`、`sourceAlias`、`buffId`、`removeAll` |

示例：

```json
{
  "scenario": {
    "setupActions": [
      { "action": "wait", "durationMs": 100 },
      { "action": "move_to", "actorAlias": "target", "position": { "x": 4, "y": 0, "z": 0 } },
      { "action": "set_attr", "actorAlias": "target", "property": "hp", "value": 1000 },
      { "action": "add_buff", "targetAlias": "caster", "sourceAlias": "caster", "buffId": 1, "durationOverrideMs": 5000 },
      {
        "action": "spawn_actor",
        "alias": "summon_1",
        "teamId": 1,
        "heroId": 1,
        "attributeTemplateId": 1001,
        "unitSubType": 2,
        "mainType": 1,
        "sourceAlias": "caster",
        "sourceKind": "Summon",
        "position": { "x": 2, "y": 0, "z": 3 }
      }
    ]
  }
}
```

设计边界：setup action 是验收测试 DSL，不是生产玩法脚本。不要在 JSON 中暴露 service/method 细节，也不要在 runner 中直接堆业务分支；新增命令应优先扩展测试工程内 executor/harness。

## 7. Timeline 约定

`timeline` 是 scenario DSL 的核心，用于描述自动释放技能、等待、取消、蓄力等时序行为。

示例：

```json
{
  "scenario": {
    "timeline": [
      {
        "atMs": 0,
        "action": "press",
        "actor": "caster",
        "slot": 1,
        "target": "target",
        "position": { "x": 6, "y": 0, "z": 0 },
        "direction": { "x": 1, "y": 0, "z": 0 }
      },
      {
        "atMs": 100,
        "action": "release",
        "actor": "caster",
        "slot": 1,
        "target": "target",
        "position": { "x": 6, "y": 0, "z": 0 },
        "direction": { "x": 1, "y": 0, "z": 0 }
      },
      {
        "atMs": 500,
        "action": "wait"
      }
    ]
  }
}
```

推荐 action 语义：

| action | 语义 |
|---|---|
| `press` | 技能按下或瞬发输入 |
| `hold` | 技能持续按住或蓄力 |
| `release` | 技能释放 |
| `cancel` | 技能取消 |
| `cast_skill` | 语义化技能释放，当前会映射为 press |
| `wait` | 等待到对应时间点或等待指定持续时间 |

设计原则：

- `atMs` 表示相对 scenario 开始的绝对时间点。
- runner 根据 `tickRate` 把时间推进到目标帧。
- 同一 `atMs` 多个动作按数组顺序执行。
- 技能输入应始终带 `actor`；有目标技能应带 `target`。

## 8. Trace 断言

trace 断言用于验证技能配置链路是否执行到了关键节点。

示例：

```json
{
  "mustContain": [
    { "kind": "SkillCast", "configId": 10010101 },
    { "kind": "EffectExecution", "configId": 10020101 },
    { "kind": "ProjectileLaunch", "configId": 30010101 }
  ],
  "mustNotContain": [
    { "kind": "EffectExecution", "configId": 999999 }
  ],
  "relationships": [
    {
      "parentKind": "EffectExecution",
      "parentConfigId": 10020101,
      "childKind": "ProjectileLaunch",
      "childConfigId": 30010101
    }
  ]
}
```

推荐用法：

- `mustContain` 验证关键节点出现。
- `mustNotContain` 验证不应触发的分支未出现。
- `relationships` 验证父子链路、rootId、触发链路关系。
- trace 适合验证“配置是否走到正确链路”，不适合验证最终战斗状态。

## 9. State 断言

`stateExpectations` 用于验证 scenario 执行完成后的 actor 状态。

示例：

```json
{
  "scenario": {
    "stateExpectations": [
      {
        "alias": "target",
        "property": "hp",
        "comparator": "lt",
        "expectedFloat": 1000,
        "tolerance": { "x": 0.01, "y": 0.01, "z": 0.01 },
        "note": "目标应该受到伤害"
      },
      {
        "alias": "caster",
        "property": "position",
        "comparator": "eq",
        "expectedVector": { "x": 0, "y": 0, "z": 0 },
        "tolerance": { "x": 0.1, "y": 0.1, "z": 0.1 }
      }
    ]
  }
}
```

当前支持的 property：

| property | 说明 |
|---|---|
| `exists` / `present` / `bound` | actor 是否存在 |
| `actorId` / `id` | actorId 是否符合预期 |
| `hp` | 当前生命值 |
| `mana` | 当前法力值 |
| `maxHp` | 最大生命值 |
| `maxMana` | 最大法力值 |
| `teamId` | 队伍 ID |
| `buff` / `hasBuff` | 是否拥有指定 buff，使用 `expectedInt` 或 `expectedValue` 表示 buffId，使用 `expectedBool` 表示期望是否存在 |
| `buffCount` | 指定 buff 数量，使用 `expectedInt` 或 `expectedValue` 表示 buffId；不传 buffId 时统计全部 active buff |
| `position` / `transform.position` | actor 当前位置 |

当前支持 comparator：

| comparator | 说明 |
|---|---|
| `eq` 或空 | 等于，float/vector 使用 tolerance |
| `ne` / `neq` / `not_eq` | 不等于 |
| `gt` | 大于 |
| `gte` / `ge` | 大于等于 |
| `lt` | 小于 |
| `lte` / `le` | 小于等于 |

## 10. Context 断言

`contextExpectations` 用于从 trace record 中验证上下文传播是否正确。

示例：

```json
{
  "scenario": {
    "contextExpectations": [
      {
        "alias": "caster",
        "kind": "SkillCast",
        "property": "sourceActorId",
        "comparator": "eq",
        "note": "技能源 actor 应为 caster"
      },
      {
        "alias": "target",
        "kind": "EffectExecution",
        "property": "targetActorId",
        "comparator": "eq",
        "note": "效果目标 actor 应为 target"
      }
    ]
  }
}
```

当前支持的 property：

| property | 说明 |
|---|---|
| `exists` / `present` / `bound` | alias 是否能解析到 actor |
| `sourceActorId` | trace 中的 sourceActorId |
| `targetActorId` | trace 中的 targetActorId |
| `configId` | trace configId |
| `rootId` | trace rootId |
| `childCount` | trace childCount |

## 11. 推荐测试编写流程

### 11.1 新增一个技能验收 case

1. 明确技能要验证的最小场景：单目标、范围、弹道、buff、召唤物、位移等。
2. 在 `actors` 中声明 caster 和必要目标。
3. 在 `timeline` 中描述技能释放。
4. 在 `mustContain` 中声明关键 trace 节点。
5. 在 `stateExpectations` 中声明最终状态，例如目标扣血、caster 资源消耗。
6. 必要时增加 `contextExpectations` 验证上下文归因。
7. 运行 unit test 或 `dotnet build` 做基础编译验证。
8. 检查导出的 trace/summary artifact，补充缺失断言。

### 11.2 随测试导出可读溯源文本

单例和批量 runner 都支持通过同一个测试 options 开启人类可读的战斗效果溯源：

```csharp
var options = new MobaAcceptanceRunOptions
{
    ArtifactDirectory = "local/Logs/moba-acceptance",
    ExportArtifacts = true,
    TraceExport = new MobaAcceptanceTraceExportOptions(),
    Recursive = true
};

var batch = MobaAcceptanceRunner.RunExpectationDirectory(options, ExpectationDirectory);
```

`TraceExport` 默认为 `null`；未携带该配置时不会生成可读溯源文本。显式传入后，每个 case 会额外生成 `{caseId}.trace.txt`。文本包含测试结论与覆盖缺口、节点类型统计、按 root 展开的父子树，以及每个节点的配置、角色、来源、帧、上下文和结束状态；批量执行后可以直接按 case 文件名定位，无需先解析 JSONL。机器分析仍使用原有的 `*_trace.jsonl` 与 `*_summary.json`。

### 11.3 调试失败 case

建议按顺序排查：

1. actor alias 是否能解析。
2. actor 是否携带技能，slot 是否正确。
3. timeline 的 `atMs` 是否足够让技能前摇、弹道、延迟效果完成。
4. trace 中是否出现 SkillCast / EffectExecution / ProjectileLaunch。
5. state 断言的 expected/tolerance 是否合理。
6. context 断言的 kind 是否匹配实际 trace kind。

### 11.4 什么时候用 trace，什么时候用 state

- trace：验证配置链路是否执行，如技能、效果、action、projectile 是否被触发。
- state：验证游戏最终结果，如 HP、Mana、Position、Team。
- context：验证归因和上下文传播，如 source、target、rootId。

推荐一个 case 至少包含：

- 1 个 SkillCast trace 断言。
- 1 个核心 EffectExecution 或 ProjectileLaunch trace 断言。
- 1 个 state 断言。
- 对复杂技能增加 context 断言。

## 12. 后续优化路线

### P0：补齐可用性

- 增加一到两个真实 scenario JSON 样例。
- `setupActions` 第一阶段命令已落地：`spawn_actor`、`set_attr`、`move_to`、`add_buff`、`remove_buff`、`wait` / `tick`。
- trace summary 中增加失败时的 nearest records 辅助定位。
- runner 输出更明确的 alias、actorId、timeline 当前帧日志。

### P1：提升表达能力

- 支持多段 timeline block，例如 `phase: prepare/cast/settle`。
- 支持循环动作，例如持续移动、周期输入。
- 支持区域断言，例如范围内目标数量、碰撞命中数量。
- 支持 projectile flight 断言，例如飞行距离、命中时间。
- 支持 buff state 断言，例如层数、剩余时间、owner/source。

### P2：工具化

- Unity Editor Window：用于选择技能、actor prefab/config、位置、目标，并生成 scenario JSON。
- Web 报告页：读取 summary/jsonl，展示 timeline、trace tree、state diff。
- CLI/CI：批量执行 scenario，输出机器可读报告。
- JSON Schema：为 scenario 文件提供编辑器提示与校验。

当前 Web 后台技能分析页的落地边界：

- `/api/admin/skills/acceptance/batch`：只读扫描 `batch_summary.json`、`*_summary.json`，并限制目录必须落在 `artifacts/` 根目录下，作为 Batch 概览、搜索、状态过滤、排序的数据源。
- `/api/admin/skills/acceptance/cases/{caseId}`：按 case 读取 summary 与 `*_trace.jsonl`，仅允许字母数字、`_`、`-`、`.` 组成的 caseId，用于 Trace Tree、断言分组、JSONL 预览。
- `/api/admin/skills/acceptance/run-plan`：返回受控执行入口边界，当前保持只读，仅描述 allow-listed script / CI job 的安全包装策略。
- `/api/admin/skills/analysis-model`：返回统一技能分析模型，当前已抽离为 provider 结构，把 runtime diagnostics 与 Scenario artifact 对齐到同一套 context、lineage、effect、assertion 视角。

Web 分析模型建议长期保持以下关联字段稳定：

| 字段 | 用途 |
|---|---|
| `battleId` | 关联真实运行态战斗实例 |
| `worldId` | 隔离房间、环境、批次 |
| `caseId` | 关联 Scenario artifact 复现入口 |
| `rootId` | 定位一次技能 / 触发链路根节点 |
| `nodeId` | 构建 trace tree 与定位异常节点 |
| `frame` | 对齐 runtime event、timeline、assertion |
| `actorId` | 关联 caster、target、owner |
| `skillId` | 聚合技能维度指标 |

执行入口正式开放前必须满足：

1. 浏览器只能提交 `scriptId` / `ciWorkflowId`，不能提交任意 shell 文本或任意文件路径。
2. 服务端维护 allow-list，并记录 `operationId`、`requestedBy`、`requestedAtUtcTicks`、`artifactDirectory`、`scenarioFilter`、`exitCode`、`logPath`。
3. 共享环境优先走 CI job，本机脚本仅作为开发环境便利入口。
4. Gateway 默认只读，必须通过环境配置显式开启受控执行。

### P3：正式化验收平台

- 引入 case tags：`smoke`、`regression`、`balance`、`network-safe`。
- 支持 golden trace 对比。
- 支持录制/回放真实战斗片段生成 scenario。
- 接入配置变更影响分析：技能表、效果表、buff 表变更后自动找相关 case。

## 13. DSL 设计边界

建议保持 DSL 面向“玩法意图”和“验收结果”，不要过早暴露底层 ECS/service 细节。

推荐：

```json
{ "action": "add_buff", "actor": "target", "buffId": 2001 }
```

不推荐：

```json
{ "service": "MobaBuffService", "method": "Apply", "args": [] }
```

原因：

- DSL 应该稳定，底层 service 可以重构。
- 未来 Web/Editor 工具更容易理解意图型动作。
- 测试 case 更接近设计和策划语言。

## 14. 当前关键实现文件

| 文件 | 职责 |
|---|---|
| `MobaAcceptanceModels.cs` | JSON DTO / scenario DSL 数据结构 |
| `MobaSkillConfigTestHarness.cs` | headless world 创建、actor/player alias、输入提交、测试环境命令底层辅助 API |
| `MobaAcceptanceRunner.cs` | legacy/scenario 路由、setup/timeline 执行 |
| `MobaAcceptanceSetupActionExecutor.cs` | 测试工程内 setup action 命令分发器，承载第一阶段环境准备命令 |
| `MobaAcceptanceExpectationAssert.cs` | trace/state/context 断言 |
| `MobaAcceptanceTraceExporter.cs` | trace 与 summary artifact 导出 |
| `GatewaySkillAcceptanceArtifacts.cs` | Web 后台只读 artifact 扫描、case 读取、run-plan 边界描述，包含路径约束、结构化错误与常量化文件元数据 |
| `GatewaySkillAnalysisModelProvider.cs` | Web 后台统一技能分析模型 provider，集中维护模型版本、阶段与关联字段 |
| `GatewaySkillDiagnostics.cs` | Web 后台 runtime diagnostics 占位入口，委托 provider 输出统一分析模型 |
| `App.vue` / `adminConsoleStore.ts` / `skillAcceptanceAnalysis.ts` | AdminConsole 技能分析、Scenario 报告、Trace Tree、执行边界与统一模型展示 |
| `AbilityKit.Game.UnitTests.csproj` | 当前 unit test 编译入口 |

## 15. 最小推荐样例

```json
{
  "caseId": "skill_10010101_minimal_scenario",
  "description": "最小 scenario：caster 对 target 释放 1 号技能",
  "worldId": "moba_headless",
  "tickRate": 30,
  "accelerated": true,
  "scenario": {
    "actors": [
      {
        "alias": "caster",
        "playerId": "p1",
        "teamId": 1,
        "heroId": 1001,
        "position": { "x": 0, "y": 0, "z": 0 },
        "skills": [10010101]
      },
      {
        "alias": "target",
        "playerId": "p2",
        "teamId": 2,
        "heroId": 1002,
        "position": { "x": 6, "y": 0, "z": 0 },
        "skills": []
      }
    ],
    "timeline": [
      {
        "atMs": 0,
        "action": "press",
        "actor": "caster",
        "slot": 1,
        "target": "target",
        "position": { "x": 6, "y": 0, "z": 0 },
        "direction": { "x": 1, "y": 0, "z": 0 }
      },
      {
        "atMs": 600,
        "action": "wait"
      }
    ],
    "stateExpectations": [
      {
        "alias": "target",
        "property": "exists",
        "comparator": "eq"
      }
    ],
    "contextExpectations": [
      {
        "alias": "caster",
        "kind": "SkillCast",
        "property": "sourceActorId",
        "comparator": "eq"
      }
    ]
  },
  "mustContain": [
    { "kind": "SkillCast", "configId": 10010101 }
  ]
}
```

## 16. 维护建议

- 每次扩展 DSL 字段后，同步更新本指南和 JSON 示例。
- 每次新增 action/property/comparator 后，同步补充表格说明。
- 保持 legacy 测试和 scenario 测试并行一段时间，等 scenario 稳定后再逐步迁移旧 case。
- 所有复杂技能优先补 scenario case，再考虑 UI 工具化。
- 工具化之前先确保 DSL 足够稳定，否则 Editor/Web 工具会频繁返工。
