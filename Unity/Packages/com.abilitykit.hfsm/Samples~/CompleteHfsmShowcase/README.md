# HFSM 可编辑运行展示

本 Sample 同时覆盖巡逻抢占、分层战斗、显式复活触发、逻辑帧推进、快照恢复、编辑器导出和运行时观察。Unity 2022.3+ 项目在 Package Manager 的 AbilityKit HFSM 包中导入 **Complete HFSM Showcase**。

1. 执行 `AbilityKit/HFSM/Samples/Create Complete Showcase Scene`，打开生成的 `Assets/HfsmShowcase/HfsmShowcase.unity`。首次执行生成 `Graphs/PatrolAndBlock.asset`、`Graphs/HierarchicalCombat.asset`、`Actions/ShowcaseActions.asset` 和相应 `Runtime/*.json`；再次执行只重导出已有编辑资产，不重建场景或覆盖图和动作资产。
2. 选中场景代理，在 Inspector 点 `Open Graph Editor` 编辑状态、层级、优先级和绑定键；点 `Select Composite Actions` 在 Inspector 展开修改 Sequence、Parallel、Wait、Log、Play 树。修改后分别点 `Export Current Graph` 和 `Export Composite Actions`。非法图、缺少的状态绑定或无效动作树会阻止导出，不覆盖对应运行时 JSON。
3. 进入 Play Mode，修改 Moving、Blocked、Health、Has Target、Target Distance；三个代理的标签和颜色显示活动状态。Inspector 中的 `Current Action`、`Action Local Frame`、`Last Action Log`、`Log Frame` 显示复合行为进度（最近日志仅显示本次状态驻留期间的日志）。`Step One Tick` 可单步；`Save Checkpoint` / `Restore Checkpoint` 恢复动作进度而不重放已执行 Log；死亡后点 `Respawn` 验证触发转移。
4. 从对应代理点 `Open Runtime Monitor`，观察 HFSM 活动层级、转移记录与持续时间；再次从另一个代理打开会选中该代理。Monitor 查看状态，Inspector 查看状态内行为进度，Console 查看全部 Log。无 Animator 时 Play 仍输出意图和局部帧，绑定含对应状态的 Animator 后按帧 seek；场景不提供动画资源。

| 图资产 | 测试场景 | 关键状态 |
| --- | --- | --- |
| `PatrolAndBlock` | Moving 和 Blocked 组合切换 | Idle / Walking / Blocked；阻挡优先 |
| `HierarchicalCombat` | 距离、目标、生死和复活 | Life(Combat[Idle/Chase/Strike], Dead)；Strike 最短驻留 0.3 秒，死亡由 Life 抢占，复活记忆子机状态 |

## 复合行为需求场景

| 需求 | 操作与复合行为 | 运行时检查 |
| --- | --- | --- |
| 巡逻进入后持续播放，延迟一次性反馈 | PATROL 勾选 Moving：Walking 的 Sequence 执行进入 Log、循环 Walk Play、等待 0.2 秒、稳定 Log | Monitor 为 Walking；Inspector 局部帧逐帧增长，Log 仅输出一次 |
| 阻挡应立即抢占巡逻且有延迟提醒 | PATROL 同时勾选 Moving 和 Blocked：Blocked 的 Sequence 执行阻挡 Log、Block Play、等待 0.5 秒、提醒 Log | Monitor 从 Walking 转到 Blocked；新状态的局部帧从零开始 |
| 追击时动画与反馈互不阻塞 | CHASE 保持目标距离大于 2：Chase 的 Parallel 同时执行循环 Run Play 和 Wait(0.2 秒) + Log | Monitor 为 Life / Combat / Chase；Run 连续推进，追击日志在第 2 个局部帧出现 |
| 攻击阶段需要与权威伤害解耦 | STRIKE 保持目标距离不超过 2：Strike 的 Sequence 执行蓄力 Log、Attack Play、Wait(0.3 秒)、命中 Log、Wait(0.2 秒)、后摇 Log | Monitor 为 Life / Combat / Strike；命中和后摇分别在局部帧 3 和 5 出现；日志不造成伤害 |
| 中断和重连不应重复副作用 | 在 Strike 保存快照，单步到命中，随后恢复；或将 Health 改为 0 | 恢复后 Play seek 到保存的帧，不重放旧 Log；死亡从父层 Life 抢占 Strike |

状态内复合行为来源于 `Actions/ShowcaseActions.asset`，TickRate 默认 10 帧/秒，Wait 时长向上取整为逻辑帧。Play 产生持续的播放意图，不自动阻塞 Sequence；需用 Wait 明确阶段时间。动作快照校验配置与 TickRate，恢复后只 seek Play，不重放历史 Log；Inspector 的最近日志在恢复时清空，可从 Console 查看原记录。并行分支独立推进，多个 Play 意图冲突时当前示例保留最后一个播放意图；复杂多层动画应使用明确的 layer/mixer 实现。命中日志只用于观察执行顺序，不施加伤害；业务伤害仍应由权威技能系统决定。

随包 `Resources/hfsm_showcase_patrol.json` 和 `hfsm_showcase_combat.json` 是可直接加载的 formatVersion 2 参考定义，`Resources/hfsm_showcase_actions.json` 是复合行为参考配置。场景运行的是实际导出的 Definition 与 action JSON，编辑图的节点 ID 由资产持久化；不要用参考文件覆盖场景的导出文件。若项目配置了全局自定义 BindingCatalog，图编辑器的 `Export Next Runtime Definition` 需要将 `showcase.*` 键加入该目录；Sample Inspector 的图导出使用示例自身的目录，不修改项目全局设置。

测试：`src/AbilityKit.HFSM.Core.Tests/CompositeStateActionTests.cs` 与 `HfsmShowcaseConfigurationTests.cs` 验证无头复合执行及参考配置；Sample 的 `Tests/HfsmShowcaseExportTests.cs` 在 Unity Editor Test Runner 验证图/动作资产导出、编辑变更和运行时恢复。所有生成文件只位于 `Assets/HfsmShowcase`；删除该目录可移除生成的场景和资产。
