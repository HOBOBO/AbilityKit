# Moba 中大型项目工程化治理补强方案

> 目标：在现有效果触发、上下文、溯源、技能运行时正式化主干之上，补齐中大型项目所需的工程化治理层，避免主链路继续依赖隐式约定、人工约束和事后排查。

## 1. 结论先行

当前 Runtime 已经具备以下基础：

- 统一触发入口与计划执行入口。
- 强类型 payload 和统一执行上下文。
- Trace / Origin / Lineage 主链路。
- Runtime Validation 框架。
- Diagnostics / Warning / Gauge / Duration 能力。
- Skill runtime 生命周期与残留扫描能力。
- Trigger plan、action schema、config reference 校验能力。

因此，现阶段的重点不再是“再造一套框架”，而是补齐四类治理能力：

1. **规范文档**：明确接入边界、数据契约、生命周期、命名与禁用项。
2. **配置校验**：把关键约束前移到启动期 / 编辑期 / 构建期。
3. **诊断工具**：把 trace、runtime、trigger、skill、plan action 的健康度统一暴露。
4. **性能压测**：建立可复用的 benchmark / stress matrix 与验收阈值。

---

## 2. 规范文档补强范围

### 2.1 必备文档

建议补一份治理总文档，作为所有后续接入规范的入口：

- `Docs/Moba中大型项目工程化治理补强方案.md`
- 配套引用到现有阶段性设计文档：
  - [`Docs/Moba效果触发上下文溯源与技能释放运行时阶段性设计.md`](Moba效果触发上下文溯源与技能释放运行时阶段性设计.md)
  - [`Docs/MobaBattleFormalIntegrationGuide.md`](MobaBattleFormalIntegrationGuide.md)
  - [`Docs/Moba正式化工程下一阶段优化清单.md`](Moba正式化工程下一阶段优化清单.md)

### 2.2 文档应明确的准入规范

#### 2.2.1 触发与 payload

- 业务触发必须使用强类型 payload。
- 禁止新增“仅靠 object / dictionary 传参”的主路径。
- 新触发类型必须明确：
  - 触发来源。
  - 目标对象。
  - payload 类型。
  - trace kind。
  - owner-bound 还是 direct trigger。

#### 2.2.2 Context

- 运行时执行上下文必须以统一主模型为准。
- 诊断视图对象不能反向侵入执行主链路。
- 新业务逻辑不得自己重新拼装一套临时上下文结构。
- 所有上下文扩展字段必须说明归属：
  - 运行时执行字段。
  - 诊断查询字段。
  - 溯源字段。

#### 2.2.3 Trace / Origin / Lineage

- 任何能跨步骤追踪的业务动作都应有 execution context root。
- 安装 Trace Adapter 后，对应 Trace root 的生命周期必须可释放、可保留、可扫描，但不能成为业务执行前提。
- lineage/origin 只负责描述来源和链路，不承担执行逻辑。

#### 2.2.4 技能运行时

- 技能释放流程必须经由正式 runtime service / pipeline。
- runtime child 必须有 retain / release / finalize 语义。
- 不允许新增绕过 runtime 生命周期的子对象创建方式。

#### 2.2.5 Plan Action

- 新增 action 必须实现 schema 校验。
- 新增 action 不得跳过 `TryValidateArgs`。
- action 的参数语义必须可被配置校验工具识别。

---

## 3. 配置校验补强方案

当前已有 [`MobaRuntimeValidationService`](Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Validation/MobaRuntimeValidation.cs:479) 和 [`MobaBattleConfigReferenceValidator`](Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Validation/MobaBattleConfigReferenceValidator.cs:11)，因此建议以“扩展现有 validator”为主，而不是另起炉灶。

### 3.1 现有校验能力可直接复用的部分

- `MobaBattleConfigReferenceValidator`
  - 配置引用完整性。
  - trigger plan 存在性。
  - skill/buff/projectile/area/gameplay 引用检查。
- `MobaRuntimeValidatorContract`
  - 统一 required validator 约束。
- `MobaRuntimeValidationMode`
  - 启动严格 / 编辑全量 / 运行采样 / 手动模式。

### 3.2 建议新增或强化的规则

#### 3.2.1 TriggerPlan 规则

需要补充以下检查：

- `TriggerPlanJsonDatabase` 中 triggerId 唯一性。
- 计划 scope 与使用场景一致性。
- owner-bound 计划必须在事件注册表中找到 args 类型。
- direct trigger 计划不得依赖 owner-bound 专属参数假设。
- action schema 参数校验必须通过 `TryValidateArgs`。
- action 的参数名别名必须稳定且一致。

#### 3.2.2 Action schema 规则

借助 [`MobaPlanActionSchemaBase`](Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Triggering/PlanActions/Core/MobaPlanActionSchemaBase.cs:15) 的 `TryValidateArgs`，对每个 plan action 做：

- 必填字段检查。
- 数值范围检查。
- 枚举范围检查。
- 互斥参数检查。
- 过时字段提示。

#### 3.2.3 Runtime 依赖规则

围绕 [`MobaSkillCastRuntimeService`](Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Runtime/MobaSkillCastRuntimeService.cs:1)、[`MobaExecutionContextRegistry`](Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Context/Execution/MobaExecutionContextRegistry.cs:1) 和可选 [`MobaTraceRegistry`](Unity/Packages/com.abilitykit.demo.moba.trace.adapter/Runtime/MobaTraceRegistry.cs:1) 补充：

- trace registry 缺失时禁止正式技能运行。
- runtime service 缺失时禁止正式技能运行。
- 运行时 child 未清理必须在校验中暴露。
- trace chain 断链必须可被检测。

#### 3.2.4 数据一致性规则

- 配置 id 必须正数。
- 事件、技能、buff、projectile、area 的跨表引用必须完整。
- 同一业务概念的命名必须统一，不允许多套别名并存。

### 3.3 校验分层建议

建议把校验分成三层：

1. **静态配置校验**：编辑期 / 构建期执行。
2. **启动前校验**：BootstrapStrict 模式执行。
3. **运行中采样校验**：低频采样执行，主要看泄漏、断链和运行状态。

### 3.4 输出格式建议

校验结果建议保留以下字段：

- severity。
- category。
- code。
- source。
- path。
- businessId。
- blocksStartup。
- message。

这与 [`MobaRuntimeValidationReport`](Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Validation/MobaRuntimeValidation.cs:193) 的结构保持一致，便于输出到控制台、构建日志和自动化测试报告。

---

## 4. 诊断工具补强方案

当前已有 [`MobaBattleDiagnosticsService`](Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Diagnostics/MobaBattleDiagnosticsService.cs:142)、[`MobaPlanActionDiagnostics`](Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Triggering/PlanActions/Core/MobaPlanActionDiagnostics.cs:9)、可选 [`MobaTraceRetention.ScanRetention()`](Unity/Packages/com.abilitykit.demo.moba.trace.adapter/Runtime/MobaTraceRetention.cs:1) 和 [`MobaSkillCastRuntimeService.ScanDiagnostics()`](Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Skill/Runtime/MobaSkillCastRuntimeService.cs:1)，可以组合为正式诊断入口；业务健康状态以 Execution Context 为准，Trace retention 仅作为已安装增强项。

### 4.1 诊断入口建议

建议提供一个统一的诊断汇总服务，职责包括：

- 触发计划健康度。
- skill runtime 泄漏 / waiting children。
- trace retention / stale root。
- runtime validation 最近一次结果摘要。
- plan action rejection / applied 计数。
- 核心依赖缺失检查。

### 4.2 必备诊断维度

#### 4.2.1 Trigger / Plan 维度

- trigger plan 缺失计数。
- owner-bound 事件未注册计数。
- action schema reject 计数。
- direct trigger vs owner-bound trigger 使用比例。

#### 4.2.2 Skill 维度

- active runtime 数。
- waiting children 数。
- pending children 数。
- pipeline finished but runtime not ended 数。

#### 4.2.3 Trace 维度

- total roots。
- active roots。
- retained roots。
- retained ended roots。
- stale retained roots。
- chain validation failure 数。

#### 4.2.4 Config 维度

- config reference 错误数。
- trigger scope mismatch 数。
- 过时字段使用次数。

### 4.3 建议诊断 API 形式

建议提供以下形式的统一入口：

- `ScanHealth()`：汇总健康检查。
- `ScanRetention()`：trace retain 扫描。
- `ScanRuntime()`：skill runtime 扫描。
- `ValidateBootstrap()`：启动严格校验。
- `ValidateConfigReferences()`：配置引用校验。

### 4.4 诊断指标建议

建议延续现有 `MobaBattleDiagnosticMetric` 组织方式，再补充：

- `moba.trigger.plan.missing`
- `moba.trigger.plan.schema.reject`
- `moba.runtime.validation.run`
- `moba.runtime.validation.blocked`
- `moba.trace.chain.invalid`
- `moba.skill.runtime.ended.waiting`

### 4.5 告警原则

- 配置缺失、链路断裂、生命周期泄漏：默认按 error / warning 输出。
- 单次诊断中的重复问题必须限频。
- 诊断工具必须支持“手动运行”和“启动自动运行”两种模式。

---

## 5. 性能压测补强方案

工业化项目不能只看功能正确性，还要给出性能预算。

### 5.1 关注对象

重点压测以下热点：

- 触发计划执行路径。
- action schema 解析与校验。
- payload resolver。
- trace node 创建与结束。
- execution context 入栈 / 出栈。
- skill runtime 创建、child retain/release。
- 诊断扫描。

### 5.2 压测矩阵建议

#### 5.2.1 单点基准

| 场景 | 目标 |
|---|---|
| 单次 direct trigger | 测试最短执行路径开销 |
| 单次 owner-bound trigger | 测试事件派发与订阅成本 |
| 单次 skill cast | 分别测试 runtime + execution context root 的必选成本，以及 Trace Adapter 投影的增量成本 |
| 单次 buff tick | 测试持续行为与定时触发开销 |
| 单次 damage pipeline | 测试伤害计算和 trace 成本 |

#### 5.2.2 批量基准

| 场景 | 规模建议 |
|---|---|
| 同帧 trigger 执行 | 100 / 500 / 1000 |
| 同帧 skill cast | 50 / 200 / 500 |
| 同帧 trace node 创建 | 500 / 2000 / 5000 |
| runtime child retain/release | 100 / 500 / 1000 |
| diagnostics scan | 1k / 10k / 50k 对象规模 |

#### 5.2.3 压力基准

| 场景 | 目标 |
|---|---|
| 长时间战斗 | 检查 trace retention 与 runtime 泄漏 |
| 高频 buff tick | 检查持续行为调度成本 |
| 高频 projectile hit | 检查触发计划和 payload 解析成本 |
| 高频配置校验 | 检查启动阶段校验耗时 |

### 5.3 验收指标建议

建议按项目阶段设定阈值，先给初版目标：

- 单次核心触发不应产生明显 GC 峰值。
- 高频路径不得出现未受控分配。
- trace root 不应无限增长。
- runtime ended 后 children 必须可在限定时间内释放。
- 诊断扫描应为低频后台路径，不应明显影响主战斗帧。

### 5.4 压测输出

每次压测应输出：

- 平均耗时。
- P95 / P99。
- 分配次数与分配字节。
- GC 触发次数。
- 失败次数。
- trace / runtime 泄漏数。

### 5.5 工程化建议

压测最好分两类：

1. **开发期 smoke benchmark**：小规模、快速、用于回归。
2. **正式压力 benchmark**：中大规模、用于版本准入。

---

## 6. 推荐落地顺序

### 第 1 步：补规范文档

输出一份接入与治理规范，统一所有团队对 runtime、trigger、context、trace、skill 的边界认知。

### 第 2 步：扩展配置校验

优先接入：

- trigger plan schema 校验。
- action arg schema 校验。
- owner-bound event args 注册检查。
- runtime dependency 检查。

### 第 3 步：统一诊断汇总入口

把 validation、diagnostics、trace retention、skill runtime scan 串成一个健康检查入口。

### 第 4 步：建立压测矩阵

先做 smoke benchmark，再补长稳压力测试。

### 第 5 步：制定准入门槛

明确哪些错误阻断启动，哪些 warning 仅记录，哪些属于性能红线。

---

## 7. 最终判断

当前架构已经具备中大型项目的主干能力，但离“可长期维护的工业化落地”还差治理层闭环。

如果按本方案补齐：

- **规范文档** 解决“怎么接入”。
- **配置校验** 解决“怎么提前发现错误”。
- **诊断工具** 解决“怎么快速定位问题”。
- **性能压测** 解决“怎么证明能扛住规模”。

这样就可以把现有正式化主干推进到更接近中大型项目的交付标准。
