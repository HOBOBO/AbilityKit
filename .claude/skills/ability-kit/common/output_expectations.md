# Output expectations

本 skill 的输出应包含：

## 必备项

- **涉及的关键文件与入口点**：当前真实路径（项目相对路径），不要照搬旧 skill 路径
- **数据流/调用链说明**：
  - 技能：`SkillCastCoordinator.TryCastSkill → SkillPipelineRunner.Start → SkillRulePlanPhase → MobaTriggerPlanExecutor.ExecuteRulePlan → 各 PlanActionModule`
  - 触发器：`EventBus.Publish<TArgs>(EventKey<TArgs>, in TArgs) → TriggerRunner<TCtx>.Dispatcher.OnEvent → 按 phase/priority 排序派发 → ITrigger.Evaluate/Execute`
  - BUFF：`MobaBuffService.ApplyBuffImmediate → 入命令队列 → DrainPending → BuffLifecycleExecutor.Apply → BuffApplyFlow → BuffEventPublisher.PublishApplyOrRefresh("buff.apply")`
- **明确指出用的是哪套 EventBus**：`AbilityKit.Triggering.Eventing.IEventBus`（moba 生产）vs `AbilityKit.Ability.Triggering.IEventBus`（ability 包 Effect 层）
- **高频路径性能约束**：池化（`PooledTriggerArgs.Rent()/Dispose()`）、手写 `for`、`in` 传结构体、避免 LINQ/反射/`new List`（仅 Editor/序列化/构建期可用）
- **对工程既有约束的遵守**：`WorldSystemBase.OnTearDown()` 反注册、`[WorldService]` singleton 注册、asmdef references 显式声明

## 反模式（应明确指出避免）

- 跨 EventBus 订阅/发布（永远收不到事件）
- 在 `OnCleanup()` 反注册（应该用 `OnTearDown()`）
- 在 Runtime 热路径用 LINQ / 反射 / `new List<>`（仅限 Editor/构建期）
- 假设 asmdef 引用会传递（moba.runtime 显式列了 34 条 references）
