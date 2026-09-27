# MOBA 技能流程可视化分析平台设计草案

## 1. 目标

本设计面向 `AbilityKit.Demo.Moba` 运行时的技能、效果、触发、Buff、投射物、伤害与表现事件的统一分析展示。目标不是做单一的 trace viewer，而是做一套能同时回答以下问题的平台：

- 这次技能为什么成功或失败？
- 这条效果链路从哪里来，挂在哪个上下文上？
- 哪个触发条件未满足，哪个 budget 被挡住了？
- effect 的 action 是从哪个 plan / config 生成的？
- Buff、Projectile、Damage、Presentation 如何与技能主链路关联？

## 2. 运行时事实基础

当前 runtime 代码已经把核心语义拆成了几层：

- `SkillPipelineRunner`：负责 `PreCast` / `Cast` 生命周期、`StartReject`、`PipelineFailure`、完成/取消/失败收口。
- `MobaCombatExecutionContext`：统一承载 payload、lineage、origin、execution snapshot、skill runtime handle、frame。
- `MobaGameplayOrigin` / `MobaContextSourceView`：描述来源事件、父节点、根节点、所有权节点和诊断边界。
- `MobaEffectExecutionService`：负责 effect scope、trigger 条件、budget 限流、action child nodes。
- `MobaExecutionContextRegistry` / `MobaExecutionKind`：负责 root/child/action 的正式身份与生命周期；`MobaTraceRegistry` 仅作可选投影。
- `Buff` / `Projectile` / `Damage` / `Presentation` payload：都已具备统一的上下文入口。

## 3. 可视化字段分组

### 3.1 生命周期字段

- `stage`
- `state`
- `elapsedMs`
- `startFrame`
- `endReason`
- `failReason`
- `startReject.code`
- `startReject.message`
- `pipelineFailure.stage`
- `pipelineFailure.code`
- `pipelineFailure.message`

### 3.2 来源归因字段

- `sourceActorId`
- `targetActorId`
- `sourceContextId`
- `parentContextId`
- `rootContextId`
- `ownerContextId`
- `originKind`
- `contextKind`
- `executionKind`
- `runtimeHandle`
- `runtimeKind`
- `runtimeConfigId`

### 3.3 触发链路字段

- `triggerId`
- `configId`
- `actionContextIds`
- `nodeId`
- `rootId`
- `parentId`
- `childCount`
- `isRoot`
- `isEnded`
- `endedFrame`

### 3.4 约束与失败字段

- trigger condition 是否通过
- failure key / reason
- execution budget block reason
- frame / depth / rootCount / sameTriggerCount
- effect scope 是否缺失
- combat execution context 是否缺失

### 3.5 结果输出字段

- effect action
- buff apply / tick / remove
- projectile launch / hit
- damage attack / calc / apply
- presentation play / stop

## 4. 组件选型

建议采用“三层联动”方案：

### 4.1 左侧：Trace Tree / DAG

用于看节点拓扑关系。

适合展示：

- 技能根节点
- effect root
- action child
- buff / projectile / damage / presentation 子节点
- 失败截断节点

### 4.2 中间：时间轴泳道

用于看时序与跨系统并行。

建议泳道：

- Skill Pipeline
- Effect Execution
- Trigger Plan Actions
- Buff Lifecycle
- Projectile Lifecycle
- Damage Pipeline
- Presentation Events

### 4.3 右侧：Inspector

用于查看单节点细节。

建议内容：

- 来源上下文
- lineage / origin / trace snapshot
- 失败原因与约束结果
- config / trigger / plan 信息
- 与父子节点的关系

## 5. 后端投影建议

当前 `AdminConsole` 的 artifact viewer 主要适合 Scenario 验收结果，后续应增加统一的技能分析 projection：

- `SkillAnalysisNodeDto`
- `SkillAnalysisEdgeDto`
- `SkillAnalysisFailureDto`
- `SkillAnalysisSourceDto`
- `SkillAnalysisTimelineEventDto`
- `SkillAnalysisSummaryDto`

最少要包含：

- `nodeId` / `rootId` / `parentId`
- `kind` / `stage` / `configId` / `triggerId`
- `sourceActorId` / `targetActorId`
- `sourceContextId` / `rootContextId` / `ownerContextId`
- `reasonCode` / `reasonMessage`
- `frame` / `timeMs`
- `children`

## 6. AdminConsole 改造方向

建议将现有页面拆成两个模式：

- `Acceptance Replay`：面向 Scenario artifact 回放
- `Runtime Live View`：面向实时战斗诊断

对应前端改造点：

- `SkillAcceptancePanel.vue` 继续负责验收报告，但数据模型升级为统一 node projection。
- `SkillDiagnosticsPanel.vue` 增加 skill trace / effect trace / failure inspector。
- 新增 `SkillTraceGraph.vue` 或同类图形层，承载树图与泳道联动。
- `skillAcceptanceAnalysis.ts` 从“解析 JSON”升级为“构建节点关系图”的 domain service。

## 7. 分阶段落地

### Phase 1

- 统一 acceptance artifact 的节点投影
- 做树图 + inspector
- 展示失败原因、来源上下文、trace lineage

### Phase 2

- 接入 effect action 和 buff / projectile / damage 事件
- 加入时间轴泳道
- 支持按 `skillId` / `actorId` / `executionKind` 过滤

### Phase 3

- 接入 live runtime trace
- 与 acceptance replay 共用同一套 schema
- 增加失败热点与调用链聚合

## 8. 验证策略

- 用现有 acceptance artifacts 验证树图结构与失败归因。
- 用 runtime smoke / test harness 验证 live projection 字段完整性。
- 先保证“看见链路”，再保证“看懂原因”，最后再做“看得舒服”。
