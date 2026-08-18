# Moba Buff 正式化推进计划

## 1. 当前定位

当前 Buff 模块已经从临时实现恢复为可运行的完整链路，具备正式化基线条件：

- 入口层由 `MobaBuffService` 统一收敛 apply/remove 请求。
- 生命周期编排由 `BuffLifecycleExecutor` 集中处理。
- 运行时状态由 `BuffRuntime`、`BuffRuntimeView`、`BuffRuntimeKey` 承载。
- 持续行为通过 `BuffContinuousBindingService`、`BuffContinuousRuntime` 接入通用 continuous 系统。
- 阶段效果、事件、表现 cue、标签门禁已经拆成独立服务。

当前阶段更适合定义为“结构健康、可扩展、需要继续控膨胀”的正式化过渡态，而不是最终收敛态。

## 2. 正式化目标

Buff 模块正式化的目标不是继续堆功能，而是让 Buff 成为可维护、可验证、可扩展、可排错的战斗运行时子系统。

核心目标：

1. 稳定生命周期顺序，避免重复添加、重复释放、先清理后上报等隐性问题。
2. 降低 `BuffLifecycleExecutor` 的编排压力，避免其继续膨胀为不可维护的总控类。
3. 明确 Buff 与 continuous、triggering、presentation、tagging 的边界。
4. 补齐驱散、免疫、替换、标签变更等高风险场景的验证能力。
5. 建立配置验收规则，减少由表驱动配置错误导致的运行时问题。

## 3. 阶段路线

### 阶段一：生命周期稳定化

目标：先保证现有链路可靠，不做大规模结构拆分。

工作项：

- 梳理 apply、refresh、replace、remove、expire、tag-end 的完整执行顺序。
- 固化 `EndRuntime` 的清理顺序：停止 continuous、清 owner 绑定、发布 remove 事件、上报表现、释放技能运行时、移出列表、回收对象池。
- 对 `BuffRepository` 的添加、移除、回收语义补充测试或验证脚本。
- 明确 `TraceLifecycleReason.None` 的归一化规则，避免表现和日志层拿到模糊原因。
- 检查 `runtime.SourceContextId` 在 remove 阶段被清理前是否已被完整快照。

验收标准：

- apply 新实例后只出现一个 runtime。
- replace 不产生旧 continuous 残留。
- remove 不出现 runtime 二次释放。
- expired/completed/interrupted/replaced 的 reason 能正确传递到事件和表现层。
- core 项目构建通过，Buff 相关新增验证通过。

### 阶段二：生命周期编排拆分

目标：降低 `BuffLifecycleExecutor` 复杂度，让扩展点更清晰。

建议拆分方向：

- `BuffApplyFlow`：负责申请、刷新、替换、新建实例。
- `BuffEndFlow`：负责结束、清理、释放、结束原因归一化。
- `BuffLifecycleNotifier`：负责事件、表现、stage effect 的统一派发顺序。
- `BuffRuntimeBindingCoordinator`：负责 skill runtime、continuous、trace/source context 之间的绑定协调。

约束：

- 不改变外部 API。
- 不改变 `MobaBuffService` 的入口语义。
- 拆分时先移动代码，不顺手改业务规则。
- 每次拆分后必须保持构建通过。

验收标准：

- `BuffLifecycleExecutor` 只保留主流程协调职责。
- apply/remove/end 的关键顺序和拆分前一致。
- 拆分后核心构建通过。
- 关键行为可通过测试或最小运行场景验证。

### 阶段三：策略扩展正式化

目标：让叠层、刷新、替换、驱散、免疫等规则具备可扩展策略入口。

工作项：

- 将 `BuffStackingPolicyApplier` 从 switch 型规则逐步演进为策略表或策略接口。
- 新增 `BuffDispelPolicy`，处理可驱散、不可驱散、按标签驱散、按来源驱散。
- 新增 `BuffImmunityPolicy`，处理目标免疫标签、来源免疫、配置免疫组。
- 明确 replace 与 dispel、interrupt、override 的优先级。
- 补充策略执行结果对象，例如 applied、ignored、blocked、replaced、failed。

验收标准：

- 新增策略不需要修改 apply/remove 主流程的大段逻辑。
- 策略拒绝能返回稳定 reject code。
- 驱散和免疫能影响事件、表现、trace reason。
- 表配置错误能被诊断日志或校验流程发现。

### 阶段四：测试与验证体系

目标：从“能编译”推进到“关键行为可回归”。

优先测试场景：

1. 同一 buff 重复 apply：ignore、refresh、add stack、replace。
2. 同一 buff 不同 source apply：按配置确认是否共用实例或强制新实例。
3. instance remove：按 `SourceContextId` 只移除指定实例。
4. remove all：同 buff 多实例全部移除。
5. continuous 到期：runtime 自动结束，并正确上报 expired/completed。
6. tag blocked：标签不满足时 apply 被拒绝。
7. tag should remove：标签变化后 reconcile 能结束 runtime。
8. replace：旧 runtime 的 continuous、trigger owner、skill retain 均释放。
9. interval tick：事件、表现、stage effect 都按 interval 阶段触发。
10. presentation optional：无表现模板时逻辑不失败。

验收标准：

- 每个高风险生命周期路径至少有一个自动化验证或稳定 smoke 场景。
- 构建命令通过。
- 测试失败能定位到具体阶段和 reason。
- 关键 reject code 稳定，不依赖日志文本判断。

### 阶段五：配置验收与工具化

目标：减少 Buff 表配置错误导致的运行时异常。

配置校验项：

- `BuffId` 唯一且大于零。
- `DurationMs`、`IntervalMs` 合法。
- `MaxStacks` 与 stacking policy 组合合法。
- `OnAddEffects`、`OnRemoveEffects`、`OnIntervalEffects` 引用的 trigger/effect 存在。
- `PresentationTemplateId` 可选，但非零时必须存在。
- `ContinuousTagTemplateId` 可选，但非零时必须存在。
- remove/replace/interval 阶段需要的上下文来源完整。

验收标准：

- 配置导出或启动时能报告 Buff 配置错误。
- 错误信息包含 buffId、字段名、引用 id。
- 非致命表现配置缺失不会阻塞逻辑运行，但能给出诊断。

### 阶段六：表现与网络同步正式化

目标：让 Buff 表现和同步具备稳定消费契约。

工作项：

- 固化 `MobaPresentationCueSnapshotEntry` 中 Buff 相关字段语义。
- 明确 `InstanceKey` 与 `SourceContextId` 的生命周期关系。
- 确认 refresh、stack changed、expired、removed 在表现层的差异化处理。
- 如果网络同步需要 Buff 状态，定义最小同步字段：buffId、sourceActorId、sourceContextId、stack、remaining、reason。
- 避免表现层直接依赖 live `BuffRuntime`。

验收标准：

- 同一 Buff 实例的 start/refresh/end 能命中同一个表现对象。
- 客户端断线重建或状态重放时可以恢复 Buff 表现必要信息。
- 表现缺失不影响逻辑链路。

## 4. 推荐优先级

近期优先级：

1. 生命周期稳定化。
2. 高风险场景测试。
3. `BuffLifecycleExecutor` 拆分。
4. 驱散/免疫策略入口。
5. 配置验收工具化。
6. 表现与同步契约整理。

建议先不要直接扩展大量新 Buff 类型。当前最值得做的是把现有链路变成可回归、可拆分、可诊断的正式基线。

## 5. 风险清单

### 高风险

- `BuffLifecycleExecutor` 继续膨胀，导致新增规则时改动主流程。
- remove/replace 清理顺序被破坏，导致 continuous、trace、trigger owner、skill retain 残留。
- `SourceContextId` 在事件或表现上报前被清理，导致溯源丢失。

### 中风险

- `BuffRepository` 索引能力不足，未来 buff 数量增加后查找成本升高。
- 事件命名和 stage 命名增长后缺少统一枚举或校验。
- 表现 cue key 长期缓存缺少清理策略。

### 低风险

- 中文注释与实现不同步。
- 部分 using 冗余。
- 现有 build warning 较多，容易掩盖真正新增警告。

## 6. 建议实施顺序

第一轮实施建议：

1. 补生命周期最小测试或 smoke 验证。
2. 修正 `BuffRepository` 索引脏标记语义，让 `MarkDirty` 名实一致。
3. 抽出 `BuffEndFlow`，优先降低结束清理复杂度。
4. 抽出 `BuffLifecycleNotifier`，统一事件、表现、stage effect 顺序。
5. 为驱散和免疫预留策略接口，但暂不实现复杂业务。

第二轮实施建议：

1. 扩展策略化 stacking/refresh/replace。
2. 补配置校验工具。
3. 整理表现与网络同步契约。
4. 建立 Buff 模块正式化验收清单。

## 7. 本轮 P0-P3 实施状态

截至 2026-08-17，推荐顺序中的正式化基线已完成：

| 阶段 | 已落地内容 | 验收证据 |
|------|------------|----------|
| P0 | 全量恢复 payload v2 版本/身份/依赖预校验；导入失败恢复旧快照；重获 skill retain，重建 Continuous、Tag requirement 和 context，依赖缺失时 fail-fast | 恢复失败不破坏现态、无资源泄漏及 ownership 专项通过 |
| P1 | `DrainPending` 固化预算、尾部保留、重入和异常隔离；单项 Immediate 返回执行结果；EndFlow 在清理异常时完成剩余补偿；timer rollback v3 使用完整实例身份并保证未命中零写入 | Unity 生命周期及 timer rollback 专项通过 |
| P2 | `BuffDispelPolicy`、可选驱散类别、不可驱散和目标有效 Tag 阻断；Tag 查询缺失 fail-closed；运行时与配置验收使用稳定诊断 code | 生命周期 fixture 18/18、配置验收 fixture 5/5 |
| P3 | `BuffRepository` 改为按列表身份隔离的四类首项哈希索引；`MarkDirty(list)` 只失效指定列表；列表回池前清除索引；同步深潜文档 | Repository 首项语义和列表级失效专项通过 |

构建证据：Runtime、Diagnostics Core Tests、Game UnitTests 均为 0 error；warning 数量分别为 111、148、162，与本轮既有基线一致。详细生命周期边界见 [`07-BuffLifecycleDeepDive.md`](design/09-ImplementationExamples/MOBA/07-BuffLifecycleDeepDive.md)。

## 8. 完成定义

Buff 模块达到正式化完成态时，应满足：

- 外部调用只依赖稳定入口服务。
- 生命周期核心路径有自动化回归覆盖。
- 主要策略通过策略类或策略接口扩展，而不是继续堆主流程分支。
- 配置错误能被提前发现。
- 表现、事件、同步都消费快照或稳定上下文，不依赖即将被释放的 live runtime。
- 新增一种 Buff 行为时，改动集中在策略、配置和测试，不需要大范围修改生命周期编排器。
