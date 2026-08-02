# 06 — Beta (0.1.0) 稳定化与发版清单

> 本文件固化"核心包从 0.0.1 推进到 0.1.0 (Beta)"的可复制流程，作为
> [`04-CompanyAdoptionAndModuleGovernance.md`](./04-CompanyAdoptionAndModuleGovernance.md)
> 的轻量前置档：用"务实 Beta"先把多包稳定下来，后续再逐包冲完整 Supported。
> 配套构建基建见根目录 `global.json`、`Directory.Build.props`。

## 1. 背景

当前 61 个核心包几乎全部停在 `0.0.1`，版本号不反映成熟度。本计划按"务实 Beta"标准
（测试 + 清债 + 版本号 + CHANGELOG）分批把**基础层**（core / world.di / network.runtime）
与**同步/宿主层**（world.framesync / snapshot / record / host）尽快推到 0.1.0。

## 2. 0.1.0 (Beta) 验收定义（每个包通用 recipe）

一个包达到 0.1.0 当且仅当：

1. **有脱离 demo 的直接契约测试**：覆盖核心 API 的成功 + 至少一个失败/边界用例；
   新建 `src/<Pkg>.Tests` 时沿用镜像编译模式（`<Compile Include="../../Unity/Packages/<pkg>/Runtime/**/*.cs">`，参考 `src/AbilityKit.Core/AbilityKit.Core.csproj`）。
2. **无 Critical/High 债**：无桩适配器、生成的代码无静默 TODO、正确性路径无吞异常、无进程级可变静态状态。
3. **构建干净**：通过对应 gate；声明 `<AbilityKitStable>true</AbilityKitStable>` 后**零警告**
   （`Directory.Build.props` 已把 CS1591 文档警告排除在门槛外）。
   ⚠ **可空性目前为咨询级**：`Directory.Build.props` 的 `WarningsNotAsErrors` 把 CS8xxx（可空标注）
   列为"警告不转错误"（CS8602 可能 null 解引用仍是硬错误）。这意味着包**无需先清完所有可空标注**
   即可开 `AbilityKitStable`；剩余可空标注作为独立的"可空启用"专项跟踪（core 现存 ~280 条）。
   `AbilityKitStable=true` 会传递到 ProjectReference 依赖，因此依赖链上每个包都需已满足本门槛。
4. `package.json` 版本 = `0.1.0`，并新增 `CHANGELOG.md`（含 API 边界 + 已知限制一段话）。

## 3. 发版动作清单（每个 0.1.0 包）

- [ ] 该包 `package.json` `version` → `0.1.0`（可批量用 `tools/update_abilitykit_package_versions.ps1`）。
- [ ] 该包新增 `CHANGELOG.md`，首条 `[0.1.0] — <date> — Beta`：API 边界 / 已知限制 / 变更。
- [ ] 该包 `src/<Pkg>` 的 `.csproj` 加 `<AbilityKitStable>true</AbilityKitStable>`（仅当依赖链已零警告）。
- [ ] `dotnet test src/<Pkg>.Tests` 全绿；`tools/run_test_gate.ps1 -Gate <对应gate>` 全绿。
- [ ] `tools/validate_abilitykit_package_json.ps1` 与 `tools/audit_unity_package_dependencies.ps1` 通过。
- [ ] （回归）`ShooterDeterministicReplayTests` + `MobaDeterministicCheckpointTests` hash 不变。

## 4. 推进路线（依赖序）

| 批次 | 内容 | 状态 |
|------|------|------|
| Batch 0 | 清场：删 TEMP 诊断；`.gitignore` 收口测试 XML/Logs；标注未提交 WIP（multiplayer-starter、静态 launch-context、新裸 catch）给 owner | 进行中 |
| Batch 1 | 构建/版本/治理基建：`global.json`、根 `Directory.Build.props`（opt-in `AbilityKitStable`）、0.1.0 发版清单、CODEOWNERS、`runtime-contracts` 门禁提 PR | 进行中 |
| Batch 2 | 基础层 → 0.1.0：`network.runtime`（模板）、`world.di`、`core` | ✅ 全部完成：`core`(11 测试)/`network.runtime`(161 测试)/`world.di`(31 测试) 均 0.1.0 + 本地开 `AbilityKitStable`（非传递，依赖链无需先零警告）。文档族(CS157x/1591)与可空(CS8xxx)降为咨询级，故各包无需逐条清理文档/可空 |
| Batch 3 | 同步/宿主层 → 0.1.0：`record`、`world.snapshot`（新建 src 测试）、`host`、`world.framesync`（先决策 D1 规范预测栈） | ✅ 全部完成：`record`(10)/`world.snapshot`(新建 5)/`host`(新建 4)/`world.framesync`(新建 4) 均 0.1.0 + 开 `AbilityKitStable`。D1 已定：`world.framesync` 的 `ClientPredictionRunner/Reconciler` 标 `[Obsolete]`，规范预测栈归 `host.extension` |
| 跨包硬化 | `protocol.editor` 生成器 `// TODO` → 抛异常（保护线协议）；`coordinator.Hybrid` 范围收口 | ✅ `protocol.editor` 6 处 TODO 桩改为 `throw NotSupported`（下次代码生成生效）；`coordinator.Hybrid` 待决策 D2 |

## 5. 待决策（不替团队拍板）

- **D1 客户端预测规范栈**：推荐 `host.extension/ClientPredictionDriverModule` 胜出，`world.framesync` 预测 runner 标废弃（不删）。
- **D2 `coordinator.HybridSyncAdapter` 0.1.0 范围**：推荐砍掉，只承诺 Local/Remote。
- **D3 `world.statesync` 空壳**（demo 依赖但 0 个 .cs）：填实现 or 移除依赖。

## 6. 本轮范围外（已识别，后续）

`host.extension` 抽 Moba 子树、`triggering` legacy/formal 收尾、`combat.damage` 从 demo 上浮、
全局静态状态收口、release/发布工作流与覆盖率、`global.json` SDK 钉版对 CI 的回归确认。
