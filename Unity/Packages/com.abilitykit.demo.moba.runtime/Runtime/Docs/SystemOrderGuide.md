# MOBA 系统装配与帧结算顺序

本页定义正式 MOBA 装配入口的执行合同。2026-11-17 用户确认的范围为
`plans/MobaAreaFrameBoundaryDecision.2026-11-17.md` D1–D3 及
`plans/MobaDerivedCombatSettlementDecision.2026-11-17.md` 第 9 节 L1–L4；机制准入以
`Docs/MobaMechanismGovernance.md` 为准。

## 装配规则

`MobaWorldBootstrapModule` 将生成清单和显式外部 `ProjectileTickSystem` 描述符合并，
按 `(WorldSystemPhase, Order, TypeName)` 排序后一次建立阶段树。
外部 Tick 的 Phase/Order 读取其自身 `WorldSystemAttribute`，不在业务侧复制偏移值。
同一类型重复、相同 Phase/Order 冲突或非法 Phase 在实例化系统前拒绝。

Entitas 按 Add 顺序执行；多次安装阶段树不会对已有树重新排序。
不能在装配尾部补第二份 ProjectileSync、AreaSync、ActorCleanup 或物理 Tick 来弥补顺序。
此规则只调整 MOBA 入口，不修改通用 AutoSystemInstaller 和 Shooter 的独立装配合同。

## 正式执行边界

Phase 优先于整数 Order；不能跨 Phase 只比较常量。下表是关键执行项，
完整清单以运行世界的执行树和各系统声明为准。

| Phase | 执行项 | 当前 Order / 职责 |
|---|---|---|
| PreExecute | CollisionWorldSync | 1100；同步本帧移动前的碰撞体采样 |
| PreExecute | EntityManagerSync、MotionInit | 按各自声明装配与初始化 |
| Execute | ResourceRegeneration | Normal + 6；固定量非生命资源回复，早于 ActorStateMachine/Brain/技能费用检查 |
| Execute | Motion、SkillPipeline、Trigger、HFSM、Buff、Continuous、Gameplay | 按生成清单顺序执行 |
| Execute | ProjectileTick | 2500；只推进一次 schedules → AreaWorld → ProjectileWorld |
| PostExecute | SkillCastDestroyCleanup | 1602；保留现有保留期及确认门限 |
| PostExecute | EntityManagerCleanup | 1905 |
| PostExecute | ProjectileSync 共同结算入口 | 1910；整轮冻结 Projectile／Area 通知后有界结算；Area 消费者不再独立安装 |
| PostExecute | ProjectileLauncherCleanup | 1912 |
| PostExecute | ShieldLifecycle、SummonLifecycle | 1913、1914 |
| PostExecute | ActorDespawnCleanup | 1918；消费同帧临时实体移除请求 |
| PostExecute | DiagnosticStateSample | 1930；采样已完成上述处理的状态 |

Buff 命令及生命周期以当前 `MobaSystemOrder` 的 BuffCommandsDrain、ContinuousTick、
BuffLifecycleReconcile、OngoingTriggerPlansReconcile 为准；旧 BuffsApply/BuffsTick 示例已移除。
阶段标签不代替真实装配验收，必须递归展开正式世界的执行树检查唯一性与顺序。

## Actor 清理的每轮顺序

2026-11-18 用户确认 `plans/MobaActorCleanupOrderDecision.2026-11-18.md` D8。
ActorDespawnCleanup 每轮收集已达到确认门限的请求候选，按稳定 ActorId 升序清理；
普通运行、关闭回滚和恢复后重演使用同一规则。候选使用可复用列表，不原地排序 Entitas 缓存数组。
无有效 ActorId 的异常候选排在有效身份之后，彼此保持原枚举顺序，仍沿既有异常清理路径处理。

消费前重新检查实体启用状态、创建序号、ActorId、请求存在性和确认门限；
创建序号只检测池对象复用，不参与业务排序或恢复数据。本轮回调新增请求、
或本轮开始尚未达到确认门限的请求，在后续轮重新收集。
同一 Execute 最多 8 轮，剩余请求保留给下一次正式调度；不改变确认条件、清理帧或系统 Phase/Order。
该规则只确定 Actor 清理候选顺序，不重排 Projectile/Area 通知、Continuous、物理或 Command 协议。

## 区域检测与恢复

- 物理前存在的区域参与本帧检测；物理产生的 Enter/Cross/Stay/Exit/Expire 在后续 AreaSync 消费。
- ProjectileSync 的到达处理或 AreaSync 的回调新建区域，首次空间检测在下一次正常物理 Tick。
  区域寿命、Stay 游标及物理帧号只能推进一次，不能补 Tick 以提前检测。
- CollisionWorldSync 仍在 MotionTick 前。移动后的空间结果使用既有采样节奏，本批未改变该合同。
- 检查点保存第 N 帧结算数据，重演从 N+1 开始。已结算效果进入 HP、Buff、认领和上下文数据；
  未到期 delay 的时间与已触发标志是未来任务状态，继续由区域数据保存。
- 恢复不得补发 Enter、重跑正常 Spawn/Added 或保存历史回调；结构回退沿既有类型化 Command。
  新旧构建的检查点不混用，本批未新增快照字段或队列恢复协议。

## 派生链的共同结算

每轮先 Drain 全部 Projectile／Area 通知并冻结 delay 候选，之后才执行回调。
顺序固定为 Projectile Spawn → Arrival → Tick → Hit → Exit → Area Spawn → due delay →
Enter/Cross → Exit → Stay → Expire。类别内保持入队顺序，Enter 保持 AreaId／ColliderId 排序。
Projectile Actor transform 只在首轮 Tick 后、Hit 前同步一次。

回调产生的任意类别通知都进入下一轮，创建后立即删除仍先 Spawn 后终结。
due delay 在 Area Spawn 后采集并标记，只处理本轮开始已存在的候选；
本轮新建对象下一轮完成 Spawn 后才具备 delay 资格，Spawn 回调不会提前观察到已触发标记。
有通知或本帧到期 delay 才继续下一轮；不推进物理、寿命、空间采样或未来任务。

合法存活 Area 在成功帧末已完成 Spawn；已终结 Area 在最后一次合法 Exit／Stay／Expire 后，
由原领域 Unregister 清理来源、索引和 retain。最后 retain 不得早于回调释放。
临时批次和候选不入快照，N 帧恢复不执行旧 Spawn／Enter／Expire。

每帧预算为 16384 个通知／due delay 项。超限、重入、必要关系缺失或后段 cleanup 新增通知，
由世界 `IWorldFrameGuard` 永久标记会话失败，阻断该帧提交、后续推进、Capture 和正式复制／权威输出。
门禁在世界原 Cleanup 后复核，不能只依赖异常传播，因为 WorldManager／HostRuntime 会隔离异常。
两帧间新增通知也阻断 Capture／输出，下一正常帧可结算；未来 schedules 不属于通知残留。

本批不循环清理树，不支持任意尾部派生；失败后重建会话，不保证撤销已发出的本地表现／诊断。
活动 schedule 恢复、已销毁 Continuous／Cast 重建及完整外部输出事务仍需独立合同。
验收见 `local/docs/Moba派生战斗帧内结算验收.2026-11-17.md`；旧缺口复现报告保留历史含义。

## 普通移动来源的驱动与恢复

`MobaMotionLocomotionInputSystem` 继续在 `MotionLocomotionInput` 的 Execute 阶段更新普通移动，
由 `MobaLocomotionRuntimeService` 持有来源和实体版本/Pipeline 关联；服务不另行调度。
正常帧取样属性与 CanMove，空组及旧关联清扫仅撤销服务自己持有的来源。
联合生命周期 v7 只恢复已批准的同实例混合来源值和次序，静默重绑不执行输入 System，
下一帧仍按原顺序推进。删除重建、Path/CollisionDash 及其它未知来源不在该范围内。

## 固定量资源自动回复

2026-11-24 用户确认 `plans/MobaSevenHeroNumericBaseline.2026-11-24.md` R1–R3。
`MobaResourceRegenerationSystem` 在 Execute 的 ResourceRegeneration 位置按世界步长调用领域服务，
按 ActorId 和规则 ID 排序，先回复再进行本帧决策与技能费用检查。仅 InGame、活跃存活且已有资源的
Actor 参与；死亡与暂停不补时，后续阶段出生/复活从下一次正常调度开始。
速率属性与实时上限每帧采样，Current 仍由 ResourceContainer 独占，定点增量封顶但不修正已超上限值。
恢复仅还原资源/属性，恢复过程不调用回复 System；从 N+1 正常重演，无新增计时或回复 Provider。

## 添加或修改系统

MOBA 业务系统在 `MobaSystemOrder` 定义职责明确的常量，用 `WorldSystemAttribute` 声明 Phase/Order，
由生成器发现；禁止编辑生成清单。外部系统在 MOBA 合并入口显式登记，
不能另加一棵独立阶段树。新的时序或所有权语义仍须按机制准入规则确认。

验收至少包括真实装配唯一性和依赖顺序、受影响技能真实结果、帧末数据恢复、
正常清理与来源存续，以及派生消息残留检查。常量大小比较或单一英雄冒烟不能代替这些验收。
