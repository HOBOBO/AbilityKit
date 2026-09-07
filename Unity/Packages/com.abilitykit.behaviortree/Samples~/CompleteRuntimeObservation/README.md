# Complete Runtime Observation

该示例同时演示 BehaviorTree 的 authoring JSON、ScriptableObject 编辑资产、确定性运行时驱动和 Runtime Observation。

## 导入与创建编辑资产

1. 在 Package Manager 中选择 **AbilityKit BehaviorTree**，导入 **Complete Runtime Observation**。
2. 执行菜单 **AbilityKit > Behavior Tree > Samples > Complete Runtime Observation > Create Or Refresh Authoring Asset**。
3. 工具读取 `Authoring/complete_runtime_observation.authoring.json`，校验并反写到 `Assets/BehaviorTreeSamples/CompleteRuntimeObservation.asset`。
4. Graph Editor 会自动打开，可查看 26 个节点、三组优先级分支、节点元数据、布局与注释。
5. JSON 是示例权威源；再次执行菜单会用 JSON 刷新已有 ScriptableObject。

## Play Mode 运行与观察

1. 在场景中新建 GameObject。
2. 添加 **AbilityKit/Behavior Tree/Runtime Observation Sample**。
3. 将 `Authoring/complete_runtime_observation.authoring.json` 拖到 **Authoring Json** 字段。
4. 进入 Play Mode。
5. 点击 Sample Inspector 中的 **Open Runtime Observation**，选择 **Complete Runtime Observation**。
6. 修改以下输入观察运行路径抢占和黑板变化：
   - `Health <= 30`：进入紧急撤退。
   - `Has Target = true`、`Can Act = true`、`Target Distance <= 6`：进入战斗。
   - 其他情况：进入默认巡逻。
7. 可使用 **Step One Deterministic Tick**、**Restart Tree**、**Stop Runtime**，也可在 Observation Window 冻结、回看或打开图形观察。

## 示例覆盖能力

- Composite：Selector、Sequence、RandomSelector。
- Decorator：Timeout、Cooldown。
- Condition：Bool、Int64、Fixed64 黑板比较。
- Action：SetBlackboard、Wait、Log。
- Typed blackboard：Bool、Int64、Fixed64、String。
- 确定性帧与时间、Seed、响应式 Restart。
- `DebugName` 自动注册、节点状态、活动路径和黑板实时观察。

## 示例代码职责

- `RuntimeObservationSample`：只适配 Unity 生命周期并协调各对象。
- `ObservationRuntimeFactory`：从 authoring JSON 创建已注册调试观察的运行实例。
- `ObservationRuntimeSettings`：确定性 tick、Seed 和重启策略。
- `AgentDecisionInputs` / `AgentDecisionOutputs`：领域输入输出与黑板映射。
- `ObservationBlackboardKeys`：JSON 与 C# 共享的黑板 key 契约。
- `RuntimeObservationSampleInstaller`：Editor 中的 JSON → ScriptableObject 安装流程。

> Sample 使用未缩放 Unity 时间仅作为 tick 调度边界；传入行为树的是离散帧号和按 tick 累加的 `Fixed64` 时间。
