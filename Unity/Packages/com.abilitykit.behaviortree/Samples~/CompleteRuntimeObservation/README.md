# 行为树可运行示例

本 Sample 包含三棵可编辑行为树、一键生成的基础 Unity 场景，以及可在 Play Mode 中操作的运行宿主。项目需先通过 Package Manager 导入 **Complete Runtime Observation** Sample。

## 打开场景与编辑

1. 执行菜单 `AbilityKit/行为树/示例/创建可运行展示场景`。菜单首次运行会在 `Assets/BehaviorTreeShowcase/` 创建场景、三份编辑资产和相应运行时 JSON；再次执行只重新导出已有资产，不覆盖树的编辑内容，也不重建已有场景。
2. 打开 `BehaviorTreeShowcase.unity`。场景中三个代理分别使用巡逻、目标追踪和优先级决策树；点击代理，在 Inspector 中点「打开行为树编辑器」。
3. 编辑并保存树后，回到该代理 Inspector，点「导出当前行为树供场景运行」。导出结果保存在 `Assets/BehaviorTreeShowcase/Runtime/`，组件运行时直接加载此 JSON；不导出则 Play Mode 仍使用上一次的产物。
4. 进入 Play Mode，展开「感知输入」修改生命值、目标和距离。胶囊颜色及场景标签反馈 `out.mode`，Inspector 中能看到帧、树状态与输出。
5. 点「打开运行时观察」选择对应实例，可查看活动节点、抢占路径、黑板初始/实时值与时间线；Inspector 的「推进一个逻辑帧」「重新开始行为树」可用于观察长耗时分支。

| 示例 | 树资产 | 可观察的行为 |
| --- | --- | --- |
| 巡逻循环 | `Trees/PatrolLoop.asset` | Sequence + RandomSelector + Wait；完成后重新开始，输出 `Patrol`。 |
| 目标追踪 | `Trees/TargetChase.asset` | Selector 优先选择近距离攻击，发现远处目标时追踪，无目标时等待；输出 `Attack` / `Chase` / `Idle`。 |
| 优先级决策 | `Trees/PriorityDecision.asset` | 26 节点的撤退 > 战斗 > 巡逻策略，含条件中断、Timeout、Cooldown、随机巡逻与 Bool/Int64/Fixed64/String 黑板。生命值 <= 30 时输出 `Retreat`。 |

三棵树同时使用 `self.*` 感知键与 `out.mode` / `out.busy` 输出键，便于在观察窗中对照同一组输入的不同决策。场景只使用 Unity 内置几何体，不依赖第三方美术资源；节点运行、时钟与随机流由 BehaviorTree 运行时负责。

旧版手动演示仍可用：执行 `AbilityKit/Behavior Tree/Samples/Complete Runtime Observation/Create Or Refresh Authoring Asset` 安装单份完整树，并将 `Resources/complete_runtime_observation.json` 赋给 `RuntimeObservationSample` 的「编辑源 JSON」。新展示场景默认使用导出的运行时 JSON，不依赖这份兼容入口。
