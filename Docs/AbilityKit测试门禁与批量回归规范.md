# AbilityKit 测试门禁与批量回归规范

## 1. 目标

AbilityKit 当前同时包含 Unity UPM 包源码、纯 C# runtime、MOBA/Shooter 示例、服务端实验工程和工具链。为了避免大型项目常见的“功能继续堆叠但基础链路已回归”的问题，测试流程必须从零散命令升级为统一门禁制度。

本规范的目标是：

1. 把单元测试、contract 测试、smoke 测试和批量回归测试分层管理。
2. 明确哪些门禁是继续开发前必须通过的阻断项。
3. 为后续 CI、批量回归、阶段性收口提供统一入口。
4. 让每个关键功能都有可追踪的测试标签和文档依据。

统一执行入口是 [`tools/run_test_gate.ps1`](../tools/run_test_gate.ps1)，门禁清单是 [`tools/test-gates.json`](../tools/test-gates.json)。

## 2. 门禁分层

| 层级 | 类型 | 典型门禁 | 触发时机 | 失败处理 |
| --- | --- | --- | --- | --- |
| P0 | Development Blocker | `precheck`、`moba-console-smoke` | 每次继续功能开发前、提交相关改动前 | 必须立即修复，不继续写新功能 |
| P1 | Contract Blocker | `core-stability`、`runtime-contracts`、`moba-content-contracts`、`moba-xiaoqiao-unity` | 核心包、网络、DI、表现运行时、内容配置或跨模块契约变化后，以及需要确认 Unity 权威技能结果时 | 修复契约破坏，或同步更新设计文档和测试预期 |
| P2 | Regression Baseline | `regression` | 大范围重构、合并前、阶段性收口、候选发布前 | 作为合并/发布阻断项处理 |

### 2.1 P0：继续开发前置门禁

P0 关注最短反馈链路。任何影响 MOBA console、战斗入口、表现层可测试性、runtime 输入链路的改动，都必须至少通过 `moba-console-smoke`。

推荐命令：

```powershell
powershell -ExecutionPolicy Bypass -File tools\run_test_gate.ps1 -Gate moba-console-smoke
```

如果只是确认本地继续开发状态，运行默认门禁：

```powershell
powershell -ExecutionPolicy Bypass -File tools\run_test_gate.ps1
```

### 2.2 P1：运行时契约门禁

P1 关注跨模块契约是否被破坏，例如：

- Record 协议兼容性、StateSync/FrameSync 状态与所有权契约。
- Host、Triggering、Context 生命周期和失败语义。
- Attributes/Modifiers 正确性，以及 Attributes 已预热 dirty recompute 的严格零分配契约。
- 核心 UPM 包生产程序集引用与直接 `package.json` 依赖声明的一致性。
- 网络 runtime 协议行为、World DI 生命周期和通用表现运行时 contract。

核心包改动推荐运行：

```powershell
powershell -ExecutionPolicy Bypass -File tools\run_test_gate.ps1 -Gate core-stability
```

网络、DI 或表现运行时边界改动推荐运行：

```powershell
powershell -ExecutionPolicy Bypass -File tools\run_test_gate.ps1 -Gate runtime-contracts
```

### 2.3 P2：批量回归门禁

P2 是阶段性收口标准，用于大范围重构或合并前。它不替代 P0/P1，而是在 P0/P1 已经稳定后，用来确认主要纯 C# 测试面没有明显回归。

推荐命令：

```powershell
powershell -ExecutionPolicy Bypass -File tools\run_test_gate.ps1 -Gate regression
```

## 3. 当前门禁清单

门禁定义以 [`tools/test-gates.json`](../tools/test-gates.json) 为准。当前门禁包括：

| 门禁 | 层级 | 负责人域 | 作用域 | 阻断点 |
| --- | --- | --- | --- | --- |
| `precheck` | P0 | Core/MOBA Runtime | 本地开发、MOBA console、战斗入口 | 继续功能开发、提交 runtime/console 相关改动前 |
| `moba-console-smoke` | P0 | MOBA Runtime/Presentation | MOBA console、表现层可测试性、战斗 smoke、技能 trace | 继续 MOBA 表现层/runtime 后续开发前 |
| `moba-codegen` | P1 | MOBA Runtime/Compile Time | MOBA Source Generator、Analyzer、Unity package 所有权 | 合并 MOBA 代码生成、静态分析或生成清单改动前 |
| `core-stability` | P1 | Runtime Platform | Record、StateSync、FrameSync、Host、Triggering、Context、Attributes、Modifiers、核心 UPM 直接依赖与 Attributes 零分配契约 | 合并核心包、生命周期、同步或 Attributes/Modifiers 热路径改动前 |
| `runtime-contracts` | P1 | Runtime Platform | 网络 runtime、World DI、Game View Runtime | 合并 runtime contract 变化前 |
| `moba-content-contracts` | P1 | MOBA Content Pipeline | 包资源所有权、TriggerPlan 聚合漂移、配置资源可加载性、跨表有效时序 | 发布 MOBA 内容或合并配置/资源改动前 |
| `regression` | P2 | AbilityKit Engineering | 核心包、MOBA、Shooter、runtime contracts 批量回归 | 大范围重构、候选发布、批量合并前 |
| `moba-xiaoqiao-unity` | P1 | MOBA Runtime/Unity Test | 小乔四个 Unity EditMode 权威用例及其落盘产物 | 宣称最新小乔 Unity 结论前 |

查看完整门禁：

```powershell
powershell -ExecutionPolicy Bypass -File tools\run_test_gate.ps1 -List
```

## 4. 配置规范

### 4.1 顶层字段

[`tools/test-gates.json`](../tools/test-gates.json) 的顶层字段用于描述门禁体系本身：

| 字段 | 含义 |
| --- | --- |
| `schemaVersion` | 配置结构版本。脚本和文档演进时递增。 |
| `owner` | 门禁体系维护责任域。 |
| `defaultGate` | 不传 `-Gate` 时默认执行的门禁。 |
| `documentation` | 对应规范文档路径。 |
| `policies` | P0/P1/P2 的统一规则说明。 |
| `gates` | 实际门禁列表。 |

### 4.2 单个门禁字段

每个门禁必须包含以下信息：

| 字段 | 必填 | 说明 |
| --- | --- | --- |
| `name` | 是 | 命令行使用的稳定门禁名，使用 kebab-case。 |
| `level` | 是 | P0/P1/P2。 |
| `owner` | 是 | 维护责任域，不要求是个人。 |
| `description` | 是 | 一句话说明门禁目标。 |
| `scope` | 是 | 该门禁覆盖的功能域。 |
| `requiredBefore` | 是 | 哪些行为前必须通过该门禁。 |
| `failurePolicy` | 是 | 失败后的阻断和处理策略。 |
| `steps` | 是 | 实际执行步骤。 |

### 4.3 Step 类型

当前支持以下 step：

| kind | 用途 | 必要字段 |
| --- | --- | --- |
| `dotnet-build` | 构建指定项目 | `name`、`project` |
| `dotnet-test` | 测试指定项目 | `name`、`project`，可选 `filter` |
| `powershell-script` | 执行仓库内 PowerShell 契约或验收脚本 | `name`、`script`，可选 `arguments`、`timeoutSeconds` |
| `unity-editmode-test` | 运行 Unity batchmode EditMode 测试并固定落盘 `command/log/xml` 产物 | `name`、`projectPath`，可选 `testPlatform`、`testFilter`、`editorPath`、`extraArgs` |
| `gate` | 嵌套执行另一个门禁 | `name`、`gate` |

## 5. xUnit 标签规范

门禁过滤依赖 xUnit `Trait`，规则如下：

| Trait | 用途 | 示例 |
| --- | --- | --- |
| `Gate` | 硬门禁标签，用于 `dotnet test --filter` | `MobaConsoleSmoke` |
| `Category` | 功能域或测试类型 | `Smoke`、`MobaConsole`、`Presentation`、`RuntimeContract` |
| `Feature` | 可选，具体功能名 | `ViewTimeline`、`SkillCastTrace` |

新增关键功能时必须遵守：

1. 至少补一个主链路测试。
2. 如果该功能会阻断后续开发，必须标记 `Gate`。
3. `Gate` 名使用 PascalCase，门禁名使用 kebab-case。
4. 测试加入门禁后，同步更新 [`tools/test-gates.json`](../tools/test-gates.json) 和本文档。

当前 MOBA console smoke 使用：

```csharp
[Trait("Gate", "MobaConsoleSmoke")]
[Trait("Category", "Smoke")]
[Trait("Category", "MobaConsole")]
```

## 6. 测试优先开发流程

### 6.1 改动前

1. 判断影响域：console、runtime、view runtime、network、DI、Shooter、Unity package。
2. 选择最小必要门禁。
3. 如果没有可覆盖该影响域的门禁，先补测试或把新测试纳入已有门禁。

### 6.2 开发中

1. 优先抽出可测试 seam，避免逻辑只能在 Unity 场景中验证。
2. 主链路优先接入 smoke 或 contract 测试。
3. 每完成一个小功能点，先跑相关 P0/P1 门禁，再继续下一批功能。
4. 新增功能默认遵循“先补测试、再扩实现、再跑门禁”的顺序。
5. 如果一项改动会影响后续多人协作或 CI 定时检查，必须同步补充门禁标签或门禁配置。

### 6.3 提交、合并或继续开发前

1. 普通小改动：至少运行 `precheck`。
2. MOBA 表现层或 console 链路改动：运行 `moba-console-smoke`。
3. 核心包、生命周期、同步或 Attributes/Modifiers 热路径改动：运行 `core-stability`。
4. network/DI/view runtime contract 改动：运行 `runtime-contracts`。
5. 大范围重构或阶段收口：运行 `regression`。
6. CI 定时检查失败后，必须先修复失败门禁，再继续合并或继续开发。

## 7. 失败处理流程

门禁失败时按以下流程处理：

1. 停止继续写新功能。
2. 确认失败类型：编译失败、测试失败、测试过滤无命中、配置错误、环境问题。
3. 如果是编译失败，先修复最小项目构建。
4. 如果是测试失败，优先判断是功能回归还是测试预期需要更新。
5. 如果是契约变化，必须同步更新设计文档、门禁配置和测试预期。
6. 修复后重新运行同一个门禁，直到通过。

不能用以下方式绕过门禁：

- 删除 `Trait` 让测试不被过滤命中。
- 把失败测试移出门禁但不补等价覆盖。
- 只跑单个测试方法后宣称门禁通过。
- 将 P0 失败延后到 P2 批量回归再处理。

## 8. CI 接入与定时检测

当前脚本已经可直接用于 CI，且会统一输出到 [`artifacts/test-gates`](../artifacts/test-gates) 下的门禁运行目录。每次运行都会生成：

- 门禁输出目录。
- 每个 `dotnet test` 的日志文件，或 Unity batchmode 的原始 `.log` 文件。
- 每个测试步的 TRX 结果文件，或 Unity step 的 `.xml` / `.command.txt` 结果文件。
- `gate-summary.json` 汇总文件。

推荐阶段如下：

| CI 阶段 | 命令 | 目的 |
| --- | --- | --- |
| Pull Request quick check | `powershell -ExecutionPolicy Bypass -File tools\run_test_gate.ps1 -Gate precheck -CI -ResultsDirectory artifacts\test-gates\precheck` | 快速阻断明显回归 |
| MOBA compile-time contract check | `powershell -ExecutionPolicy Bypass -File tools\run_test_gate.ps1 -Gate moba-codegen -CI -ResultsDirectory artifacts\test-gates\moba-codegen` | 校验 Roslyn 构建、生成/分析契约和 package 所有权 |
| Core stability check | `powershell -ExecutionPolicy Bypass -File tools\run_test_gate.ps1 -Gate core-stability -CI -ResultsDirectory artifacts\test-gates\core-stability` | 校验八个核心包测试、Attributes 零分配契约和 UPM 直接依赖 |
| Runtime contract check | `powershell -ExecutionPolicy Bypass -File tools\run_test_gate.ps1 -Gate runtime-contracts -CI -ResultsDirectory artifacts\test-gates\runtime-contracts` | 校验网络、DI 和表现运行时契约 |
| Nightly regression | `powershell -ExecutionPolicy Bypass -File tools\run_test_gate.ps1 -Gate regression -CI -ResultsDirectory artifacts\test-gates\regression` | 批量回归，包含嵌套 `core-stability` |

对应的 GitHub Actions 工作流位于 [`/.github/workflows/abilitykit-test-gates.yml`](../.github/workflows/abilitykit-test-gates.yml)。

### 8.1 定时策略

当前定时策略以 `workflow schedule` 为准：

- `0 18 * * *` UTC，每天触发一次。
- 对应北京时间约为每天 02:00。
- pull request 和 push 独立执行 `core-stability`，便于快速定位核心包失败。
- 定时和手工任务由 `regression` 嵌套执行 `core-stability`，避免同一工作流重复运行核心测试。
- 定时任务默认执行 `regression` 门禁，适合做夜间批量回归和失效检测。

### 8.2 CI 产物约定

每次 CI 运行建议上传整个门禁目录作为 artifact，便于排查：

- `gate-summary.json`：门禁状态、耗时、失败步骤。
- `*.log`：每个 step 的原始命令输出。
- `*.trx`：Test Explorer / CI 可直接读取的测试结果。

如果后续要引入覆盖率或更多 Unity batchmode 门禁，只需要继续沿用同一目录结构即可。

## 9. 维护规则

1. 新增门禁必须同时更新 [`tools/test-gates.json`](../tools/test-gates.json) 和本文档。
2. 删除门禁必须说明替代门禁或删除原因。
3. 修改 P0 门禁前必须确认不会降低继续开发前置保障。
4. `regression` 可以变大，但 P0 应保持快速。
5. 文档中的命令必须与脚本实际参数保持一致。

## 10. 当前 MOBA console smoke 覆盖范围

`moba-console-smoke` 当前覆盖：

1. 完整战斗 smoke 场景。
2. Console 表现层时间线对齐决策。
3. 真实 tick 链路中的表现层时间线对齐。
4. 技能释放、runtime 输入端口、技能效果 trace、导出 artifact 主链路。

该门禁是后续 MOBA 表现层可测试性优化的默认前置条件。

## 11. Shooter / MOBA 示例测试资产地图

Shooter 和 MOBA 示例不是普通展示 demo，而是 AbilityKit 框架能力的验收样板。测试资产应按“纯 C# 快速验证优先，Unity batchmode 权威验证补充”的方式分层管理。

| 示例 | 测试资产 | 当前入口 | 已覆盖能力 | 当前缺口 |
| --- | --- | --- | --- | --- |
| MOBA Console | xUnit smoke | `src/AbilityKit.Demo.Moba.Tests/Smoke/ConsoleMobaSmokeFlowTests.cs` | 完整战斗入口、表现时间线对齐、技能 cast trace、artifact 导出 | 覆盖面集中在 `Gate=MobaConsoleSmoke`，其他 smoke/contract 用例尚未进入独立门禁 |
| MOBA DSL / Script | xUnit 单元与 runner 测试 | `src/AbilityKit.Demo.Moba.Tests/Smoke/BattleTestScenarioLibraryTests.cs`、`src/AbilityKit.Demo.Moba.Tests/Smoke/BattleTestScriptRunnerTests.cs` | 平台无关脚本模型、随机种子、stress/full battle 场景、runner 失败回传 | 需要提升为独立 DSL/环境构建门禁，验证脚本资产能驱动 console 和 view runtime |
| MOBA View Runtime | xUnit runtime 测试 | `src/AbilityKit.Demo.Moba.View.Runtime.Tests/AbilityKit.Demo.Moba.View.Runtime.Tests.csproj` | client sync strategy、DemoHarness carrier、remote interpolation playback | 已进入 `regression`，但尚未形成更小的 P1/P0 view runtime 门禁 |
| MOBA Unity EditMode | Unity batchmode EditMode 测试 | `moba-content-contracts`、`moba-xiaoqiao-unity` | 通用内容契约、包资源所有权、聚合漂移、资源加载、有效时序，以及小乔权威技能结果和落盘产物 | 通用内容门禁仍为手工 P1；应在发布流水线稳定后纳入配置/资源变更的 CI 路径 |
| Shooter Runtime | xUnit runtime 测试 | `src/AbilityKit.Demo.Shooter.Runtime.Tests/AbilityKit.Demo.Shooter.Runtime.Tests.csproj` | sync mode、snapshot、protocol、gateway、rollback、determinism、presentation contracts | 测试资产丰富，但缺少统一 `Gate`/`Category` 标签，不能按 P0/P1 精确过滤 |
| Shooter DSL / Scenario | xUnit JSON scenario 与 benchmark 测试 | `src/AbilityKit.Demo.Shooter.Runtime.Tests/Gameplay/ShooterSveltoGameplayScenarioRunnerTests.cs` | JSON 场景、loadout、battle flow、确定性结果、benchmark profile | 需要明确哪些场景属于 smoke，哪些属于 nightly regression |
| Shooter Sync Matrix | xUnit 同步模式 smoke | `src/AbilityKit.Demo.Shooter.Runtime.Tests/Client/ShooterSyncModeSmokeTests.cs` | PredictRollback、AuthoritativeInterpolation、BatchStateSync、MassBattleLodSync、HybridHeroPrediction | 适合拆出 `shooter-runtime-smoke` 和 `shooter-sync-contracts` 门禁 |

相关设计依据：

1. Shooter 纯 C# 验收外壳与 Unity 薄层绑定见 [`Docs/Shooter验收外壳绑定说明.md`](Shooter验收外壳绑定说明.md)。
2. Shooter view/runtime 分层和测试先行原则见 [`Docs/Shooter示例View与Runtime整体设计.md`](Shooter示例View与Runtime整体设计.md)。
3. Shooter 同步能力矩阵见 [`Docs/Shooter同步能力演示流程与设计.md`](Shooter同步能力演示流程与设计.md) 和 [`Docs/网络同步能力档案与DemoHarness矩阵设计.md`](网络同步能力档案与DemoHarness矩阵设计.md)。
4. MOBA runtime 启动链、技能主链路和上线化缺口见 [`Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Docs/StartupChainGuide.md`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Docs/StartupChainGuide.md)、[`Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Docs/GoldenSkillFlowGuide.md`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Docs/GoldenSkillFlowGuide.md)、[`Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Docs/MobaRuntimeProductionReadinessReview.md`](../Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Docs/MobaRuntimeProductionReadinessReview.md)。

## 12. 推荐门禁演进矩阵

当前门禁已经能跑通 CI 和批量回归，但 Shooter/MOBA 示例还需要从“测试资产存在”推进到“测试资产可被稳定门禁选择”。建议按以下顺序演进：

| 推荐门禁 | 层级 | 建议来源 | 覆盖目标 | 纳入时机 |
| --- | --- | --- | --- | --- |
| `moba-runtime-smoke` | P0 | `src/AbilityKit.Demo.Moba.Tests` | runtime first frame、startup validation、skill pipeline prewarm、snapshot buffer 消费 | 当 MOBA runtime 启动链继续扩展时 |
| `moba-dsl-env` | P1 | MOBA script library 与 runner tests | full/random/stress script、seed 稳定性、console/view runtime 共用脚本模型 | 当 DSL 成为测试场景构建入口时 |
| `moba-view-runtime-contracts` | P1 | `src/AbilityKit.Demo.Moba.View.Runtime.Tests` | client sync strategy、carrier、interpolation playback | 当表现层同步策略或 view runtime contract 变化时 |
| `shooter-runtime-smoke` | P0 | Shooter sync mode smoke、scenario runner smoke | 关键同步模式能启动、基础场景能稳定完成、最终 snapshot 健康 | 当 Shooter runtime/view runtime 继续扩展示例能力时 |
| `shooter-sync-contracts` | P1 | Shooter protocol/snapshot/gateway/rollback tests | 协议、快照、网关、回滚、重连、同步策略契约 | 当网络同步抽象或 DemoHarness carrier 改动时 |
| `unity-editmode-authoritative` | P1/P2 | Unity EditMode tests | Unity 侧权威用例、编辑器集成、落盘验收产物 | 夜间或合并前专项运行，不建议默认阻断所有小 PR |

落地顺序建议：

1. 先给 Shooter 核心 smoke 和 contract 用例补 `Trait`，不要急于扩大 CI 默认耗时。
2. 把 MOBA 已有 DSL/runner 用例归入 `moba-dsl-env`，确认脚本资产是 console 和 view runtime 的共同输入。
3. 将 P0 保持在几分钟内，P1 面向跨模块 contract，P2/nightly 承担完整矩阵。
4. 每新增一个门禁，都先在本地跑通，再加入 [`tools/test-gates.json`](../tools/test-gates.json) 和 CI workflow。

## 13. DSL / 环境构建测试规范

DSL 或脚本场景测试的目标不是复刻所有人工操作，而是把“能稳定构建战斗环境并走完主链路”变成可重复资产。新增 DSL/环境构建测试时遵守以下规则：

1. 场景输入必须平台无关，优先放在纯 C# 层，避免依赖 Unity 场景对象才能解释测试意图。
2. 每个脚本场景必须有稳定 ID、随机种子、固定 tick 数或明确结束条件。
3. 测试断言优先检查关键状态、trace、snapshot、错误报告和 artifact，而不是只检查没有抛异常。
4. full battle、random、stress 三类脚本应分层进入不同门禁：full battle 可进 P0/P1，random/stress 默认进 P2/nightly。
5. 同一个 DSL 场景如果需要覆盖 console 和 view runtime，应共享脚本模型，只替换 driver/harness。
6. 任何 flaky 场景必须先退出 P0，定位为 deterministic replay 问题、时间依赖问题、资源加载问题或环境问题后再恢复。

推荐将 DSL 测试拆为三类：

| 类型 | 作用 | 适合门禁 |
| --- | --- | --- |
| Script model tests | 验证脚本结构、默认值、seed、序列化或 catalog 稳定 | P0/P1 |
| Runner contract tests | 验证 step 调度、duration、失败回传、driver 协议 | P1 |
| Scenario execution tests | 真实驱动 console/view/runtime，验证状态、trace、snapshot | P1/P2 |

## 14. 适用边界与成本控制

这套流程适合 AbilityKit 的原因是：AbilityKit 是框架型能力库，同时承担 MOBA/Shooter 示例、网络同步、运行时契约和 Unity 包交付。它的风险不是单个 demo 能不能跑，而是后续重构、多人协作、跨包发布和 CI 演进时，基础链路是否还能被快速证明。

适合使用完整流程的项目类型：

1. 长线运营、多人在线、竞技、同步或强数值规则项目。
2. 有公共 runtime/framework/package，需要被多个示例或游戏复用的项目。
3. 已进入多人协作或频繁重构阶段，人工验收成本持续上升的项目。
4. 需要 CI/nightly regression 作为合并或发布依据的项目。

不需要一开始完整使用的项目类型：

1. 一次性原型、短周期玩法验证或单人离线小项目。
2. 规则仍在剧烈探索、接口还没有稳定下来的早期 demo。
3. 没有 CI、没有多人协作、没有长期维护目标的展示项目。

成本控制原则：

1. P0 只保护最短主链路，不能把所有测试都塞进 P0。
2. 单元测试覆盖规则和边界，smoke 测试覆盖主链路，DSL 测试覆盖组合场景，Unity batchmode 覆盖引擎集成结论。
3. 每个线上 bug 或关键回归都应沉淀为一个最小测试，但不要求每个临时实现细节都有测试。
4. 测试用例数量增长后，必须通过标签、门禁和 nightly 分层管理，否则 CI 时间和维护成本会反噬开发效率。
5. 文档、门禁配置和测试标签必须同步演进，避免测试存在但无人知道何时运行。
