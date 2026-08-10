# Ability-Kit Behavior 行为执行模块开发设计文档

## 一、定位与边界

`com.abilitykit.behavior` 是一个按 Tick 驱动的行为决策与执行运行时。它把一次行为拆成两个可替换阶段：`IBehaviorDecision` 根据上下文和世界查询产生 `DecisionResult`，`IBehaviorExecutor` 将结果解释为移动、事件、效果、完成或中断意图。

该模块不是完整行为树编辑器，也不是 Unity 协程、任务调度器或跨线程执行框架。BehaviorTree、BTCore 和 MOBA Brain 属于上层组合能力；Behavior 只提供可被这些系统复用的行为运行时契约。

当前能力结论为 E0/E1：包内源码和示例能证明实现存在，MOBA 与 BTCore 路径能证明被调用；不能据此宣称已有稳定生产级测试、性能预算、发布阻断或回滚门禁。

## 二、运行模型

### 2.1 每帧决策与执行

运行中的 `BehaviorRuntime.Tick(deltaTime, frame)` 执行以下顺序：

```text
Running Tick
  -> 累加 ElapsedSeconds
  -> 清空上一帧 BehaviorOutput
  -> Decision.Decide(Context, World)
  -> Executor.Execute(Result, Context, Output)
  -> 处理 Complete / Interrupt 请求
  -> 检查 DurationSeconds
```

`BehaviorOutput` 是帧级意图缓冲区。`Output.Clear()` 在每次 Running Tick 开始时调用，因此持续移动、事件或效果意图必须由 Decision/Executor 每帧重新发布，不能假设上一帧输出会保留。

`DecisionResult` 可以携带 `DecisionKind`、参数、移动目标、目标实体和速度。`DefaultExecutor` 主要处理 Continue、Complete 和 Interrupt；`ChangeState` 不会自动替业务系统产生状态副作用，必须由上层解释。

### 2.2 生命周期

```text
Created -> Running -> Completed
                  \-> Interrupted
Running <-> Paused
```

`Start()` 将行为置为 Running；`Pause()` 和 `Resume()` 只改变 BehaviorRuntime 自身阶段。暂停不会自动通知 BTCore 的 Running Node 停止，也不会统一冻结外部时钟、动画或移动系统。`ElapsedSeconds` 只在 Running Tick 中增长。

实现 `IContinuous` 时的映射为：

| BehaviorPhase | ContinuousState |
|---|---|
| Created | Inactive |
| Running | Active |
| Paused | Paused |
| Completed | Expired |
| Interrupted | Aborted |

完成和中断是终态。终态事件由 Runtime 通知 Manager，Manager 再执行索引移除与资源清理。

## 三、所有权与清理

`BehaviorManager` 以 instance id 保存 `BehaviorRuntime`，并提供按实体、行为类型和优先级查询，以及 Interrupt/Pause/Resume 操作。`BehaviorCreateConfig` 负责提供 owner、行为类型、Decision、Executor、配置和可选 Duration。

清理时会释放实现 `IDisposable` 的 Decision，并从 Manager 的字典移除运行时。当前 Manager 没有统一的 `IDisposable`、Shutdown 或存量 Runtime 批量清理契约，宿主必须自行保证 Manager 生命周期与 Runtime 生命周期一致。

Manager 的 `Tick()` 直接遍历 `_behaviors.Values`。如果 Runtime 在同一次遍历中完成或中断，回调会同步 Cleanup 并删除字典项，存在集合枚举失效风险。接入方应在生产采用前补充回归测试，或在 Manager 层采用快照遍历/延迟删除策略。

## 四、异常与失败边界

Decision 或 Executor 抛出的异常会被捕获并转换为 `Interrupt($"Exception: {ex.Message}")`。这保证行为不会继续运行，但当前中断原因主要保留异常消息，不包含异常类型、堆栈、行为实例和帧号等结构化诊断信息。需要可靠故障定位时，宿主应在 Decision/Executor 外层记录结构化日志，并避免把敏感信息直接放入用户可见原因。

其他必须显式处理的边界：

| 边界 | 当前语义 | 接入要求 |
|---|---|---|
| 空或无效 Decision | 依赖创建配置与调用方保证 | 创建前校验并拒绝 |
| Duration | 只在 Running Tick 中累计 | 暂停期间是否计时由宿主语义决定，不能默认推断 |
| 持续输出 | 每帧清空 | 每帧重发意图 |
| Pause | 只改变 Runtime 阶段 | 外部系统需自行停止/恢复 |
| Tick 中终态清理 | 可能同步删除 Manager 条目 | 先补并发/枚举回归测试 |
| Dispose | Decision 可释放，Manager 无统一 Shutdown | 宿主负责批量停止与资源回收 |

## 五、目录与扩展点

```text
Runtime/
├── Core/       BehaviorPhase、DecisionResult、BehaviorEntityId 等值类型
├── Interface/  IBehaviorContext、IBehaviorDecision、IBehaviorExecutor、Output 契约
├── Runtime/    BehaviorRuntime、BehaviorManager、DefaultWorldQuery
└── Tests/      包内测试或宿主侧验证入口（以实际工程配置为准）
```

扩展 Decision 时应保持纯决策边界：通过 `IBehaviorContext` 读取配置、状态和目标，通过 `IWorldQuery` 查询世界，不直接持有 Manager 或修改外部系统。扩展 Executor 时应明确每种 `DecisionKind` 的副作用归属；若需要 ChangeState、事件总线或效果系统，应由上层适配器消费输出，而不是把跨模块生命周期偷偷放入默认 Executor。

## 六、消费者与证据

- E0：`Runtime/Runtime/BehaviorRuntime.cs`、`BehaviorManager.cs`、`Interface/IBehaviorDecision.cs` 和 `IBehaviorExecutor.cs` 证明核心实现与生命周期。
- E1：Samples、BTCore 集成和 MOBA Brain 路径证明 Behavior 结果被上层调用。
- E2：当前仓库可见的是 MOBA 运行时消费者；尚不能把所有 Behavior API 认定为框架外生产主链。
- E3：已有部分生命周期、MOBA 和 BTCore 行为测试，但未覆盖 Manager Tick 同步删除、统一 Shutdown、异常结构化诊断等关键风险。
- E4/E5：当前未发现针对本包的独立 Smoke、性能预算、CI 阻断或发布回滚证据。

推荐源码阅读顺序：`BehaviorTypes.cs` -> `IBehaviorDecision.cs` -> `IBehaviorExecutor.cs` -> `BehaviorRuntime.cs` -> `BehaviorManager.cs` -> 上层 BehaviorTree/MOBA 消费者。

## 七、采用门槛

将 Behavior 用于新的长期运行主链前，至少应补齐：

1. Manager 在 Tick 中终态删除的回归测试；
2. Manager Shutdown/Dispose 的统一所有权契约；
3. Decision/Executor 异常的结构化诊断字段；
4. Pause、Duration 与外部时钟之间的明确协议；
5. 对持续输出重复发布、完成/中断幂等性和高频 Tick 的验收样例。

本设计文档是包内运行语义的 canonical 入口；更高层的行为树分层、BTCore 和 MOBA 集成见 `Docs/design/13-FrameworkCore/02-BehaviorTreeIntegrationDesign.md`。
