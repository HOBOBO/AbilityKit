---
name: moba-hero-batch-authoring
description: 按正式配置源批量实现 AbilityKit MOBA 英雄技能，并同步完成 Flow、Trigger、表现、验收和发布检查；遇到权威来源、机制落点或扩展边界不确定时暂停确认。
metadata:
  short-description: MOBA 英雄技能批量实现工作流
---

# MOBA 英雄批量实现

本 Skill 用于新增一个英雄、批量实现一个英雄的主动技/被动/技能效果，或把一个英雄机制整理成可复用的正式配置切片。它引用并服从 [`moba-skill-authoring`](../moba-skill-authoring/SKILL.md) 的落点判定、标签、控制效果、扩展门禁和反模式；引用 [`moba-demo`](../moba-demo/SKILL.md) 的包结构、加载链路和测试入口。

调用本 Skill 后，可以直接编辑正式配置源并执行导出、发布和检查。只有遇到本文件“暂停条件”中的未决策项，才暂停并向用户提问。未得到确认前，不修改生产配置，不新增运行时代码，不用猜测替代确认。

## 权威来源

| 内容 | 编辑入口 | 产物（只检查，不直接编辑） |
|---|---|---|
| 英雄、技能、Buff、Area、Projectile、Query、模型、VFX、表现模板及数值 | `LubanConfig/Moba/Production/Datas/*.xlsx` | Luban JSON、bytes、生成代码、`Resources/luban` 和 Console 副本 |
| Skill Flow 的阶段、等待、提交点 | `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/Moba/SkillFlowCO.asset` | `Resources/moba/skill_flows.json` 和 Console Flow 副本 |
| 时间轴逻辑与表现轨道 | `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/action_timeline/skill_*.json`，用 ActionEditor 编辑 | 同目录 `*.moba.logic.json`、`*.moba.presentation.json` |
| 技能、被动、Buff、玩法事件的条件和效果动作 | `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaTriggerAuthoring/Packages/*.Module.asset`，通过 Trigger Authoring Workspace 编辑，并同步 `Sources/*.trigger.json` | Unity/Console `ability_trigger_plans.json` |
| 角色或投射物状态机 | `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaStateMachineAuthoring/` 的状态机编辑器 | `moba/character_hfsm.json`、`moba/actor_state_machines.json` |

不要编辑发布后的 JSON、bytes、生成代码、Console 副本、历史 `Production/Archive` 或迁移用 `Resources/ability/triggers/`。日常开发不要运行会覆盖生产模块的 Trigger `GenerateBatch`；使用 `PublishRuntimeBatch` 和 `CheckRuntimeBatch`。

详细命令和归属以 [`Docs/MobaSkillPipeline.md`](../../../Docs/MobaSkillPipeline.md)、[`Docs/MobaTriggerAuthoring.md`](../../../Docs/MobaTriggerAuthoring.md) 和 [`LubanConfig/Moba/README.md`](../../../LubanConfig/Moba/README.md) 为准。

## 工作流

### 1. 收集设计输入

先建立一张英雄实现表，至少包含：英雄 ID、技能槽位、技能/被动 ID、Flow ID、Effect/Trigger ID、Buff/Area/Projectile/Launcher/Query/VFX/Model/PresentationTemplate ID、目标规则、数值参数、持续时间、控制效果、表现资源和验收场景。

为每个机制标注能力维度：普通数值、资源消耗、Buff、位移、投射物、区域、多段阶段、召唤物、标签、表现 Cue、回滚/重连。引用已有机制时先找到真实生产源和消费者，不从生成物反推设计。

### 2. 判定落点

按 [`references/decision-matrix.md`](references/decision-matrix.md) 从内容参数向运行机制判断。优先复用现有的 Skill Flow、Trigger 动词、Continuous Runtime、Targeting 注册点和表现 Cue。一个技能不能因为“看起来特殊”就新增组件、属性、服务或英雄专属运行时分支。

特别遵守：施法阶段、蓄力、引导、提交点、资源结算和阶段等待属于 Skill Flow；时间轴动作属于 ActionEditor；事件发生后的条件与效果属于 Trigger Authoring；纯客户端可见内容属于表现层。不要用 TriggerPlan 节点替代技能内部的阶段编排。

### 3. 先做一个垂直切片

从该英雄最能代表机制的技能开始，完成一条可验收链路：正式表格行 → Skill Flow → Trigger Authoring → 必要的 ActionEditor 逻辑/表现 → 运行时加载 → 真实行为验收。复杂技能优先选择能暴露生命周期、目标选择、位移、区域或投射物问题的代表性场景。

只有垂直切片通过后，才批量铺同英雄剩余技能。每完成一类能力，立即更新实现表和验收覆盖，不把多个未验证技能堆到最后统一排查。

### 4. 按正式源发布

依赖顺序固定为：

1. 在 ActionEditor 保存并导出逻辑/表现 JSON；没有时间轴需求时明确记录“不使用 ActionEditor”。
2. 在 `SkillFlowCO.asset` 编辑并发布 Flow；新 Flow ID 必须先发布，Luban 才能校验技能表引用。
3. 在 Trigger Authoring Workspace 编辑模块，保存 Source，并发布聚合 TriggerPlan。
4. 在 Luban Excel 编辑角色、技能、效果、Buff、区域、投射物、目标查询、模型和表现资源等表，执行 `python LubanConfig/Moba/export_pipeline.py --apply`。
5. 若修改状态机，先由状态机编辑器保存并发布，再检查状态机产物。

每一步都检查生成物是否来自当前源，不能为了让检查变绿而手改生成文件。

### 5. 编写真实验收

在 `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/UnitTest/Acceptance/Heroes/<Hero>/<Hero>SkillAcceptanceTests.cs` 增加受影响技能的验收。复杂技能至少断言一项真实状态或运动结果，并保留关键 trace；不得只断言 Action trace。

按机制选择断言：位移验证位置、穿墙/落地和目标控制；Buff 验证授予、刷新、叠层、过期、移除和多来源；投射物验证生成、移动、命中/退出和 VFX 绑定；区域验证生成、进入/离开、间隔和结束；表现验证 Cue 的添加、暂停、恢复、结束和资源加载。

新增英雄还要同步维护 `tools/moba-hero-acceptance-coverage.json`、`tools/test-gates.json` 和 `tools/validate_moba_hero_acceptance_coverage.ps1` 要求的英雄登记点。不要通过降低覆盖声明来绕过验收。

### 6. 发布检查

按改动范围执行，至少包括：

```powershell
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate moba-content-contracts
python LubanConfig/Moba/export_pipeline.py --check-published
dotnet test src/AbilityKit.Demo.Moba.Tests/AbilityKit.Demo.Moba.Tests.csproj --filter "FullyQualifiedName~MobaLubanConfigPipelineTests|FullyQualifiedName~BattleFlowRealSkillTests"
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate moba-<hero>-unity
```

涉及 Trigger 时运行 `TriggerAuthoringMobaMigration.CheckRuntimeBatch`；涉及 Skill Flow 时运行 `MobaSkillPipelineExporter.Check`；涉及状态机时运行 `MobaStateMachinePublisher.Check`。运行受影响英雄验收后，再补六英雄 .NET 冒烟和相关内容门禁。失败时先定位当前工作树、源文件、发布物和测试报告，不沿用历史失败结论。

## 必须暂停确认的情况

遇到以下任一情况，停止依赖该决策的编辑，并向用户说明已确认事实、候选落点、影响范围和需要确认的问题：

- 不清楚哪个文件或字段是权威源，或多个源对同一事实有不同值。
- 现有 Flow、Trigger 动词、Targeting 注册点、Continuous Runtime 或表现 Cue 无法明确表达机制。
- 需要新增组件、属性、领域服务、PlanAction、Targeting 规则、投射物命中策略、协议字段或状态机类型。
- 机制创建/销毁实体，或涉及回滚、重连、快照、Actor ID、召唤物生命周期。
- 标签的授予源、消费规则、控制免疫、持续对象生命周期或多来源叠加不明确。
- 需求要求直接修改生成物、历史迁移物或把逻辑塞入英雄专属硬编码。
- 预期行为与已有英雄、设计文档、生产表或门禁结果冲突。
- 导出或检查失败，且无法证明失败只由当前改动引起。

提问应具体到一个决策，不以“是否继续”代替问题。例如：“该机制需要反射命中，但 `ProjectileHitPolicyFactory` 是框架侧硬编码；请选择复用现有命中策略、修改框架扩展点，或暂不支持反射。”

## 完成定义

只有以下条件全部满足，才报告英雄或技能完成：

- 实现表中的 ID、来源、消费者和依赖闭合。
- 正式配置源已保存，发布产物由导出命令生成且检查无漂移。
- 受影响技能的真实验收通过，覆盖对应的行为和表现维度。
- Trigger、Flow、状态机（如有）检查通过。
- 业务 ID、依赖图、资源归属、表现资源和有效时间门禁通过。
- 没有未确认的架构假设、未使用参数、绕过正式源的临时补丁或未说明的覆盖缺口。

实现完成后报告：修改的正式源、生成命令、检查结果、验收结果、未覆盖维度和仍需用户决策的事项。
