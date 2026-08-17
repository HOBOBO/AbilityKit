# 旧版/兼容性清单

本文记录 `com.abilitykit.demo.moba.runtime` 当前仍存在的 Legacy、Stub、TODO、兼容适配和临时占位路径。

这份清单的目标不是一次性清掉所有历史代码，而是给后续优化提供明确边界：

- 新功能不得依赖本清单中标记为 Legacy、Stub、Fallback 的路径。
- 兼容路径必须有推荐替代路径。
- TODO 占位必须被视为功能缺口，而不是默认实现。
- 命名中的 Obsolete/Legacy/Compat/Stub 需要区分真实风险和普通运行时语义。

## 分级规则

| 等级 | 含义 | 新代码是否可依赖 | 处理策略 |
| --- | --- | --- | --- |
| Legacy | 旧流程或旧数据结构遗留 | 否 | 保留短期兼容，迁移到推荐路径后删除 |
| Compatibility | 为了兼容旧调用或旧配置保留 | 谨慎，默认否 | 保持边界清晰，新增用例优先走新路径 |
| Stub | 为了让流程跑通的占位实现 | 否 | 替换成正式 PlanAction、Service 或配置驱动实现 |
| TODO Placeholder | 已知未完成能力 | 否 | 后续按功能优先级补齐 |
| Fallback | 默认兜底或降级路径 | 否，除非明确是产品行为 | 只作为过渡保护，不作为扩展入口 |
| False Positive | 命名命中但语义正常 | 可以 | 不需要治理，只在清单中说明 |

## 总览

| 位置 | 类型 | 当前用途 | 推荐方向 |
| --- | --- | --- | --- |
| `Runtime/Common/Enum/CDLEnum.cs` | Legacy | `ProjectileSpawnMode.LegacyAimPos` 旧投射物出生点模式 | 新配置使用明确的出生点/发射器模式 |
| `Runtime/Domain/Triggering/ShootProjectileAction.cs` | Legacy | 仍处理 `LegacyAimPos` | 迁移到 `shoot_projectile` PlanAction 与发射器配置 |
| `Runtime/Infrastructure/Config/Core/MobaConfigGroups.cs` | Legacy | `LegacyJson` 配置组 | 新配置进入明确的 config group |
| `Runtime/Infrastructure/Config/Core/ConfigGroupNames.cs` | Legacy | `LegacyJson` 配置组名 | 逐步停止新增旧 JSON 聚合入口 |
| `Runtime/Infrastructure/Config/Core/LegacyJsonConfigGroupDeserializer.cs` | Legacy | 旧 JSON 反序列化兼容 | 仅保留加载旧数据，新增 DTO 使用专属反序列化 |
| `Runtime/Application/Services/Skill/Effects/MobaEffectExecutionService.cs` | Compatibility | 已移除执行期修复缺失 action；仅在初始化阶段注册 PlanActionModule | 继续让 `TriggerPlansStage` 与 `PlanActionModuleRegistry` 成为唯一注册入口 |
| `Runtime/Application/Services/Skill/Pipeline/DefaultMobaSkillPipelineLibrary.cs` | Fallback | 默认技能管线兜底 | 新技能使用 `TableDrivenMobaSkillPipelineLibrary` |
| `Runtime/Application/Services/Skill/Handlers/BuiltInSkillHandlers.cs` | Legacy / TODO | 旧 Handler 流程痕迹与若干 TODO | 新效果走 SkillFlow + PlanAction |
| `Runtime/Application/Services/Skill/DTO/SkillHandlerDTO.cs` | Legacy | 旧技能 Handler DTO | 新配置走 SkillFlow、TimelineEvent、PlanAction 参数 |
| `Runtime/Domain/Predicates/CombatPredicates.cs` | TODO Placeholder | 目标、Buff、HP 等谓词仍有占位逻辑 | 接入正式查询服务或 ECS 组件读模型 |
| `Runtime/Application/Systems/Bootstrap/PlanActions/ConsumeResourcePlanActionModule.cs` | TODO Placeholder | 资源扣除尚未接入正式资源系统 | 接入资源/能量服务并补失败语义 |
| `Runtime/Application/Services/Skill/Conditions/BuiltInSkillConditions.cs` | TODO Placeholder | 施法状态、沉默/禁用检查未完整实现 | 与 Buff/Tag/State 查询能力合流 |
| `Runtime/Application/Services/Skill/Phases/SkillFlowChecksPhase.cs` | TODO Placeholder | required/blocking tag 检查未实现 | 与 TagQuery 或 Buff/State 查询合流 |
| `Runtime/Application/Services/Skill/Pipeline/SkillPipelineContext.cs` | Compatibility | 填充兼容 shared data key | 保留读旧 key，新增代码优先使用强类型上下文 |
| `Runtime/Application/Services/Effect/EffectPipelineContext.cs` | Compatibility | `FillSkillCompatibleKeys` | 与 `SkillPipelineContext` 统一兼容策略 |
| `Runtime/Application/Systems/Skill/PassiveSkillTriggerListenerManager.cs` | False Positive | `RemoveObsoleteListeners` 表示移除失效监听 | 不按 Legacy 处理，可保留当前命名 |

## Legacy 路径

### 旧投射物出生点模式

涉及文件：

- `Runtime/Common/Enum/CDLEnum.cs`
- `Runtime/Domain/Triggering/ShootProjectileAction.cs`

现状：

`ProjectileSpawnMode.LegacyAimPos` 仍被旧触发逻辑识别。这类模式通常来自早期“先跑起来”的配置表达，含义不如发射器、挂点、目标点等正式模型清晰。

治理规则：

- 新投射物配置不得新增 `LegacyAimPos`。
- 新技能优先通过 SkillFlow timeline event 触发 `shoot_projectile`。
- 投射物出生点应由 launcher/projectile 配置表达，而不是由旧 action 解释。

建议后续动作：

1. 找出仍使用 `LegacyAimPos` 的配置。
2. 为这些配置补齐 launcher spawn mode。
3. 在配置迁移完成后删除 legacy 分支。

### 旧 JSON 配置组

涉及文件：

- `Runtime/Infrastructure/Config/Core/MobaConfigGroups.cs`
- `Runtime/Infrastructure/Config/Core/ConfigGroupNames.cs`
- `Runtime/Infrastructure/Config/Core/LegacyJsonConfigGroupDeserializer.cs`

现状：

`LegacyJson` 仍作为配置组存在，用于承接旧 JSON 聚合数据。这对兼容历史数据有价值，但不适合作为新增配置入口。

治理规则：

- 新表、新 DTO、新配置域不得挂到 `LegacyJson`。
- 新配置必须进入明确的配置组，并有独立反序列化路径。
- `LegacyJsonConfigGroupDeserializer` 只负责旧数据兼容，不承载新格式演进。

建议后续动作：

1. 盘点当前 `LegacyJson` 下仍加载的 DTO。
2. 按领域拆入 battle、skill、effect、projectile、presentation 等明确组。
3. 保留一段兼容窗口后删除旧组。

## Stub / 兼容 Action 路径

### PlanAction stub 自动补注册

涉及文件：

- `Runtime/Application/Services/Skill/Effects/MobaEffectExecutionService.cs`

现状：

旧的 `MobaWorldBootstrapModule.PlanActions.StubActions.cs` 已删除，运行时不再保留空壳 stub 注册入口。`MobaTriggerPlanExecutor` 已移除执行期捕获 `InvalidOperationException` 后修复并重试的路径。PlanAction 的正式边界是初始化阶段发现强类型模块并注册到 `ActionRegistry`。

治理规则：

- 新 PlanAction 必须通过 `IPlanActionModule` + `[PlanActionModule]` 注册。
- `PlanActionModuleRegistry` 应是模块发现入口。
- `TriggerPlansStage` 应负责把配置里的 plan/action 注册到 `ActionRegistry`。
- `MobaEffectExecutionService` 不应重新引入“修复注册表”的职责。

推荐路径：

```text
TriggerPlan config
    -> TriggerPlansStage
    -> PlanActionModuleRegistry
    -> IPlanActionModule.Register(ActionRegistry)
    -> MobaEffectExecutionService.Execute(effectId, context)
```

建议后续动作：

1. 继续统计哪些 action id 缺少正式 `PlanActionModule`。
2. 为缺失 action 补正式强类型模块和 schema。
3. 把 PlanAction 注册时机进一步前移到 bootstrap stage 的明确安装步骤，减少服务初始化里的隐式注册。

## Fallback 技能管线路径

涉及文件：

- `Runtime/Application/Services/Skill/Pipeline/DefaultMobaSkillPipelineLibrary.cs`
- `Runtime/Application/Services/Skill/DTO/SkillHandlerDTO.cs`
- `Runtime/Application/Services/Skill/Handlers/BuiltInSkillHandlers.cs`

现状：

当前最佳实践已经转向表驱动 `SkillFlow`：

```text
SkillDTO
    -> SkillFlowDTO
    -> SkillTimelineEventDTO
    -> effectId
    -> TriggerPlan
    -> PlanActionModule
```

旧的 Handler DTO 和内置 handler 更像早期技能执行模型遗留，适合迁移期保留，但不应作为新技能扩展方式。

治理规则：

- 新主动技能默认使用 `TableDrivenMobaSkillPipelineLibrary`。
- 新效果默认使用 timeline event + trigger plan + plan action。
- 旧 handler 只保留兼容与对照价值。
- 如果某个 handler 能力仍有价值，应拆成正式 `PlanActionModule` 或 service。

建议后续动作：

1. 在 `DefaultMobaSkillPipelineLibrary` 注释中明确 fallback 定位。
2. 将仍有价值的 `BuiltInSkillHandlers` 能力迁移到 PlanAction。
3. 删除无配置引用的旧 handler DTO。

## TODO 占位功能

### 战斗谓词

涉及文件：

- `Runtime/Domain/Predicates/CombatPredicates.cs`

现状：

目标 actor、Buff、HP 等谓词存在 TODO 或占位逻辑。这会影响触发器、技能条件、AI 条件的可信度。

推荐方向：

- 目标相关谓词接入 actor/entity 查询服务。
- Buff 相关谓词接入 BuffService 或 ECS buff 组件。
- HP/状态相关谓词接入 Attribute/State 查询服务。

优先级：高。谓词是触发器和条件系统的基础能力。

### 资源消耗

涉及文件：

- `Runtime/Application/Systems/Bootstrap/PlanActions/ConsumeResourcePlanActionModule.cs`

现状：

资源扣除仍是 TODO，占位实现容易让技能看起来成功但没有实际资源成本。

推荐方向：

- 接入资源/能量服务。
- 明确资源不足时的失败语义。
- 与 `SkillFlowChecksPhase` 的前置检查保持一致。

优先级：高。资源消耗是技能闭环的一部分。

### 技能条件

涉及文件：

- `Runtime/Application/Services/Skill/Conditions/BuiltInSkillConditions.cs`
- `Runtime/Application/Services/Skill/Phases/SkillFlowChecksPhase.cs`

现状：

施法状态、沉默、禁用、required tag、blocking tag 等仍未完整实现。

推荐方向：

- 将状态查询统一到 Actor State、Buff、TagQuery 或 Attribute 服务。
- `BuiltInSkillConditions` 负责可复用条件。
- `SkillFlowChecksPhase` 负责编排 flow 级检查。

优先级：高。它决定技能是否能被正确阻止。

## 兼容适配层

### Host 输入入口收敛

涉及文件：

- `Runtime/Application/Services/Input/MobaInputCoordinator.cs`

现状：

旧的 `MobaLobbyInputSink` 适配器已删除。正式输入协调器 `MobaInputCoordinator` 直接注册 `IWorldInputSink`、`IMobaInputCoordinator` 和具体类型，Host 帧同步输入与玩法输入协调不再分成两条实现路径。

治理规则：

- Host 输入入口统一落到 `MobaInputCoordinator`。
- 业务系统应依赖稳定后的命令或事件模型。
- 不再新增独立的 `IWorldInputSink` 兼容适配器。

### Skill/Effect 兼容键

涉及文件：

- `Runtime/Application/Services/Skill/Pipeline/SkillPipelineContext.cs`
- `Runtime/Application/Services/Effect/EffectPipelineContext.cs`

现状：

上下文仍填充兼容 key，说明旧 action、旧表达式或旧 plan 仍读取字符串型 shared data。

治理规则：

- 新代码优先使用强类型上下文访问器。
- 兼容 key 只用于读取旧 plan/旧表达式。
- 新增 key 必须进入集中定义，避免散落字符串。

建议后续动作：

1. 列出所有兼容 key。
2. 给每个 key 标注使用方。
3. 对仍需要的 key 建立正式常量或访问器。
4. 删除无人读取的兼容 key。

## 误报

### RemoveObsoleteListeners

涉及文件：

- `Runtime/Application/Systems/Skill/PassiveSkillTriggerListenerManager.cs`

说明：

`RemoveObsoleteListeners` 表示移除运行时已经失效的监听器，属于正常生命周期清理语义，不是旧系统兼容或废弃 API。

处理：

- 不需要治理。
- 不需要改名，除非团队希望统一避免 `Obsolete` 关键词误触扫描。

## 注释与文档编码问题

扫描过程中发现若干旧文件注释存在乱码或不可读文本，主要集中在旧 handler、PlanAction、配置迁移相关位置。

治理规则：

- 修复注释时只改注释，不混入行为修改。
- 优先修复仍处在推荐路径上的文件。
- Legacy 文件可以只保留简短迁移说明，避免维护大段过时注释。

## 后续整理建议

建议按风险从小到大推进：

1. 在 Legacy/Stub/Fallback 文件顶部补简短定位注释。
2. 为新文档补 Unity `.meta`，保持包资产一致性。
3. 更新 `ServiceRegistrationGuide.md` 中可能过时的扫描范围说明。
4. 给 `DefaultMobaSkillPipelineLibrary`、旧 handler、stub action 注册处补推荐替代路径。
5. 补齐资源消耗、技能条件、战斗谓词等 TODO 占位功能。
6. 删除无引用的旧 DTO、旧 handler 和 legacy config group。

## 新功能准入检查

新增技能、效果或触发器时，应检查：

- 是否走 `TableDrivenMobaSkillPipelineLibrary`。
- 是否通过 SkillFlow timeline event 触发 effect。
- 是否通过 TriggerPlan + PlanAction 表达行为。
- 是否通过 `PlanActionModuleRegistry` 注册 action。
- 是否避免依赖 `LegacyJson`。
- 是否避免依赖 stub action 自动补注册。
- 是否避免新增兼容 shared data key。
- 是否补齐资源、条件、状态检查。
