# MOBA 技能能力缺口矩阵

> 文档类型：MOBA 项目能力评估
> 事实基线：2026-09-14
> 方法：以"机制轴 × 技能形态"为行，对每一格判定"用当前配置 + 现有动词/阶段能不能表达"，区分**能力缺失**与**能力闲置**。
>
> 本文回答一个问题：**这套系统离承载真实商业 MOBA 的技能需求还差几个设计？**
> 配套规范见 skill `moba-skill-authoring`；设计理由见同目录 `13`/`14`/`18`。

## 1. 判定口径

| 判定 | 含义 | 后续动作 |
|---|---|---|
| **✓ 能表达** | 用现有配置 + 现有动词/阶段即可，零代码 | 直接做 |
| **A 差动词** | 缺一个**可复用的 PlanAction**（或现有动词缺参数） | 加动词（6 处同步点） |
| **B 缺能力/未接线** | 阶段或组合能力**已实现但零内容**，或实现与语义不符 | **先用内容验证**，再决定 |
| **C 缺机制** | 需要**新状态维度 / 新领域运行时 / 新同步契约** | 按一等机制设计 |
| **D 范围外** | 需要新玩法域（视野/地形/经济…），不是技能系统的问题 | 识别并排除 |
| **E 硬凑** | 能拼出来但落点错误，会污染现有抽象 | 否决，改走 A/C |

**关键区分**：A/B 是"系统健全，差一点包装或验证"；C 才是真正的设计缺口。**只看通过率没有信息量，必须看 C 的分布。**

## 2. 矩阵

### 轴 1：时序

| 技能形态 | 判定 | 依据 |
|---|---|---|
| 瞬发单体/范围伤害 | ✓ | `give_damage`（damage_value / source_attack_ratio / attribute_source / target_mode） |
| 延迟结算（落地后 N ms） | ✓ | `aoes.DelayMs`、Timeline `AtMs`、`SkillDelayPhase` |
| 多段/周期（定时反复） | ✓ | `ongoing_effects.PeriodMs`、`launchers.DurationMs+IntervalMs`、buff `IntervalMs` |
| 可中断（受控/死亡打断） | ✓ | `cancel_skill` + `SkillCastStage.Cancelled` + `AwaitEvent` |
| 阶段间插队并恢复 | ✓ | 已有验收 `Skill10010301_CanInsertSkill1BetweenStagesAndResumeUltimate` |
| 等待条件 / 等待事件 | ✓ | `SkillWaitUntilPhase`（Condition/Timeout/CompleteOnTimeout/ObservedSlots）、`SkillAwaitEventPhase`（EventId/Filters） |
| **蓄力释放（按档位取不同效果）** | **B（有单测）** | `SkillWindowPhase` + `WindowKind.Charge` + `ChargeTierThresholdMs` 已实现；单测 `ChargeRelease_PersistsResolvedTierAndReleaseReason` + `FromDtoAndToDto_RoundTripsAdvancedWindowStateMachinePhases`；**26 条 flow 零使用** |
| **引导（持续施法 + 周期 tick + 可打断）** | **B（有单测）** | `WindowKind.Channel` + `ChannelIntervalMs`；单测 `ChannelInterrupt_StopsFutureTicks`、`ConsumeOperation_AbortsWhenChannelTickCannotPay`；零内容 |
| **重复 N 次（固定间隔）** | **B（有单测）** | `SkillRepeatPhase`（RepeatCount/IntervalMs）；单测 `ToDto_MapsRecursiveSequenceParallelRepeatAndControlPhases`、`InspectorSelection_ResolvesNestedRepeatPhaseAndStablePropertyPath`；零内容 |
| **并行两段** | **B（有单测）+ 命名陷阱** | flow 级 `AbilityParallelPhase` **是真并行**（子阶段同帧启动、未完成者逐帧推进）；但触发器级 `ParallelTriggerPlanExecutable` 是**顺序遍历 + fail-fast**，同名不同义。零内容 |
| **竞速（先到者胜）** | **B（有单测）** | `SkillRacePhase`；单测 `AbilityRacePhaseTests`、`Race_TimeoutWindowWinsAndUnsubscribesAwaitEvent`、`RaceLoserWindow_DoesNotExecuteBusinessCloseTriggers`；零内容 |
| **充能次数（分层回复）** | **B（有单测）** | `SkillEconomyPhase`（MaxCharges/ChargeCost/ChargeRecoveryMs）+ `ActiveSkillRuntime.CurrentCharges`；单测 `Charges_RecoverLazilyAtDeterministicBoundaries`、`SkillCooldownRollback_RestoresChargeConfigurationAndProgress`；flow 零使用，内容走 `SkillMO.CooldownMs` |
| **共享冷却 / 全局冷却** | **B（有单测）** | `Economy.CooldownGroup/SharedCooldownMs/GlobalCooldownMs/IgnoreGlobalCooldown`；单测 `SharedCooldownAndGlobalCooldown_BlockRelatedCastsOnly`；零内容 |
| **资源提交点 + 失败退款** | **B（有单测）** | `SkillCommitPointPhase` + `Economy.RefundBeforeCommit`；单测 `CancelBeforeCommit_RefundsResourceAndChargeExactlyOnce`、`CommitThenCancel_KeepsCostAndStartsAllCooldowns`、`PipelineCompletion_ImplicitlyCommitsUncommittedReservation`；26 条 flow 用 `RulePlan(发动)+RulePlan(结算)` 近似 |
| **释放另一个技能（派生技能 / 继承瞄准）** | **B（有单测）** | `SkillPhaseType.DerivedSkill`(=20) + `SkillDerivedSkillPhaseDTO`（SkillId/InheritAim/InheritTarget/WaitForCompletion/MaxDepth）；单测 `FromDtoAndToDto_RoundTripsP2DerivedSkillAndEventCaptures`；零内容 |
| **事件载荷捕获到黑板** | **B（有单测）** | `SkillAwaitEventPhaseDTO.Captures`（FieldId/Key/Scope/ValueType/Required）；单测 `AwaitEvent_CapturesRegisteredPayloadFieldsIntoScopedRuntimeBlackboard`；零内容 |
| **施法前后摇（PreCast）** | **B（无验证）** | `SkillDTO.PreCastFlowId` 字段在，构建分支在；**无单测、无配置样例**，文档自述"不能只根据字段和源码分支认定可用" |

### 轴 2：空间

| 技能形态 | 判定 | 依据 |
|---|---|---|
| 直线弹道 | ✓ | `projectiles`（Speed/Lifetime/MaxDistance/Collision 盒）+ launcher |
| 扇形 / 散射 / 爆发多发 | ✓ | `launchers.CountPerShot + FanAngleDeg`；`Fan/Scatter/Burst` pattern |
| 追踪弹 | ✓ | projectile 追踪 + `track_target` |
| 回旋弹（去而复返） | ✓ | `ReturnAfterMs/ReturnSpeed/ReturnStopDistance` + `ProjectileExitReason.ReturnArrived` |
| 穿透（命中 N 个继续） | ✓ | `HitPolicyKind` + `HitsRemaining` + `HitCooldownMs` |
| **弹射 / 连锁（命中后跳到下一个目标）** | **A / C** | 无 bounce：`HitPolicyKind` 只有 ExitOnHit/Pierce。用"命中触发 → 再发射追踪弹"可**近似**，但那是 E（落点错），要通用化需在投射物域加 hit policy |
| **墙壁反射** | **C** | `ProjectileWorld` 无 reflect 分支，需要碰撞法线反射 |
| **环绕自身（轨道运动）** | **A（有近似）** | 无轨道运动源；嬴政飞剑用 `Projectile.StateMachineProfileId` + `projectile.moveRelative` 硬凑 → 属 E 近似，通用化需 motion 侧的轨道源 |
| 即时形状判定（锥/矩形/圆/胶囊，无弹道） | ✓ | targeting `SectorShape / RectangleShape / CircleShape / CapsuleShape` |
| 地面区域（进入/离开/驻留） | ✓（配置面有缺口） | `spawn_area` + `AreaWorld` + `area.*` 事件；**但 `aoes.json` 只暴露 `OnDelayTriggerIds`**，Enter/Exit/Interval 触发 id 在 DTO 里有、内容表没有 |
| **跟随施法者的区域 / 光环** | **C** | `aoes.AttachMode` **运行时未消费**（只在 `MobaBattleConfigReferenceValidator` 里查 `<0`）；`AreaSpawnParams` 只有生成时固定的 `Center` |
| 可被墙/单位阻挡 | ✓ | 碰撞世界 + `pass_through_walls` |

### 轴 3：状态

| 技能形态 | 判定 | 依据 |
|---|---|---|
| 单体控制（眩晕/沉默/减速） | ✓ | Buff + `ContinuousTagTemplate.ActivationBlockedTags` |
| 叠层（最多 N 层 + 刷新策略） | ✓ | `StackingPolicy / MaxStacks / RefreshPolicy` |
| **多来源独立标记（两个英雄各自叠、各自计时）** | **C** | `BuffStackingPolicy` 只有 IgnoreIfExists/Replace/AddStack/RefreshDuration；applier 收 `sourceActorId` 但只**覆盖** `existing.SourceId` → 一个 buff id 在目标上只有一份 runtime，做不到"按来源分实例" |
| 可/不可驱散 | ✓ | `DispelPolicy / DispelCategory / DispelBlockedByTagNames` |
| 免疫（免控/免伤） | ✓ | tag + 9 个 `damage.*` 阶段事件可拦截/修改 |
| 层数触发（满 3 层引爆） | ✓ | `advance_gameplay_counter`（threshold → trigger_id）或 `buff.interval/stack_changed` |
| 状态互斥 | ✓ | `ContinuousTagTemplate` 的 ActivationRequired/BlockedTags |
| 霸体 / 免疫位移 | ✓ | tag + motion group `SuppressedGroupIds`（5 个运动组带抑制关系） |
| 状态到期触发效果 | ✓ | buff `OnRemoveEffects` / `TriggerIds` |
| **变身 / 形态切换（换技能组）** | **A** | 基建已有 `MobaHeroReplacementTransactionService`（换实体 + `MobaPlayerLoadout` + 玩家绑定原子切换 + precommit 快照 + 补偿），缺包装成动词 |

### 轴 4：关系

| 技能形态 | 判定 | 依据 |
|---|---|---|
| 召唤物（单只） | **A** | 机制在（`spawn_summon` + `SummonMO`），但 **6 英雄内容 0 使用**；`interval_ms/duration_ms/total_count/query_template_id` **解析后丢弃** |
| 召唤物分波 / 一次多只 | **A** | 同上，参数已解析未实现（旧强类型引擎有 Line/Ring/Arc/Grid，新 Plan 路径未承接） |
| **召唤物参与回滚 / 重连** | **C** | 无 rollback provider；`MobaLogicWorldStateImporter` 对召唤物"按 Unit 处理（P1 简化）"且**不写 `SummonMeta`** → 重进后超时/主死清理失效 |
| **载具 / 骑乘** | **C** | 全仓无 mount/vehicle 概念 |
| **傀儡 / 复制对方技能组** | **A** | 复用变身那套 replacement 基建 + `SkillLoadoutComponent`（`ActiveSkillRuntime[]`）；缺"从目标读取 loadout"的动作 |
| **单位绑定（共享伤害 / 链接）** | **C** | per-pair 关系，无机制 |
| 传送 / 位移到友方 | ✓ | `blink` + targeting `ContextTarget` provider |
| **隐身 / 视野检测** | **C** | 可见性**零代码**；本质是 per-observer 关系 |
| **仇恨 / 嘲讽** | **C** | 有 brain + blackboard，无仇恨模型 |

### 轴 5：资源

| 技能形态 | 判定 | 依据 |
|---|---|---|
| 消耗法力 / 能量 | ✓ | `consume_resource` / `modify_resource` + Economy |
| 消耗生命施法 | ✓ | `modify_resource`（resource_type=Hp） |
| 资源转治疗 / 护盾 | ✓ | `convert_resource_to_heal`；护盾走 `add_shield` |
| 血量分档效果 | ✓ | `health_percent` predicate + Buff（赵云已实现） |
| 计数达成强化（第 N 次普攻） | ✓ | `advance_gameplay_counter`（墨子/嬴政已实现） |
| 溢出转化（过量治疗→护盾） | ✓ | heal 阶段事件 + 触发器组合（fixture `99190004` 已证明） |
| 上限动态变化 | ✓ | Modifier 改 `MAX_HP / MAX_MANA` |

### 轴 6：世界

| 技能形态 | 判定 | 依据 |
|---|---|---|
| 建筑 / 塔（可标记、可摧毁） | ✓（受轴 3"多来源标记"限制） | 单位 + `UnitSubType` + 血量；标记 = Buff |
| 地形 / 穿墙 | ✓ | `pass_through_walls` + 碰撞世界 + 导航 |
| 持续区域影响敌我 | ✓ | area + `CollisionLayerMask` + targeting |
| 对局级事件（结束 / 结算） | ✓ | `end_game`（reason_id / win_team_id）+ gameplay vars |
| 兵线 / 刷新节奏 | **待核** | 有 `gameplay.tick/started` 事件 + `gameplays.json`，大概率可组合 |
| **草丛 / 迷雾** | **D** | 新玩法域 |

## 3. 汇总

共 61 条形态（脚本按 §2 表格逐行统计，可复现）：

| 判定 | 条目数 | 占比 | 性质 |
|---|---|---|---|
| ✓ 能表达 | 34 | 56% | — |
| **A 差动词** | 6 | 10% | 便宜，加就是 |
| **B 已实现 + 有单测 + 零内容** | 10 | 16% | **不是能力问题，是"没人用"** |
| **B（无验证）** | 1 | 2% | PreCast |
| **C 缺机制** | 8 | 13% | **真正的设计缺口** |
| D 范围外 | 1 | 2% | 排除 |
| 待核 | 1 | 2% | 见 §4 |

> 计数口径：「弹射/连锁」判定为 **A/C 两可**，此处计入 A（若能接受"命中触发再发射"的近似则属 E，需明确否决）。

### 3.1 B 类（11 条）：**不是能力问题，是"全链路建好了没人用"**

初版判为"已实现未验证"，逐条核对单测后**修正**：11 条里 **10 条有实质单测覆盖**，链路是完整的：

```
能力实现 ✓ → 配置构建 ✓（TableDrivenMobaSkillPipelineLibrary 的 switch 覆盖全部 15 种 phase）
          → 编辑器作者化 ✓（SkillFlowSO + SkillFlowDef 全类型 Def + Add Skill Phase 菜单）
          → 往返 ✓（FromDtoAndToDto round-trip 单测）
          → Unity EditMode 单测 ✓（Window 8 例 / Economy 9 例 / Race 3 例 / 编辑器 10 例）
          → 生产内容 ✗（26 条 flow 一条都没用）
```

真正没有验证的只有 **PreCast** 一条（无单测、无样例，文档自述不可据字段认定可用）。

**这把问题从"能力问题"改判为"流程问题"**：既然能力、构建、编辑器、往返、单测都在，为什么 26 条 flow 全落成同一个三段式？值得追问的方向：

- 内容作者没有入口感知（编辑器有菜单，但没有人示范过一条蓄力/引导/充能技能）
- 迁移脚本只生成了历史形态，后续照抄
- 缺少"这条能力怎么配"的示例内容（P1 Economy / P2 Skill Programming 有编辑器 showcase 资产，但**没有落到生产内容**）

**唯一确认的缺陷是命名陷阱**：flow 级 `AbilityParallelPhase`（真并行）与触发器级 `ParallelTriggerPlanExecutable`（顺序遍历）同名不同义，按名字选用会出错。

**行动建议**：把 10 条各补一条**生产内容 + 验收**（不是补能力）。成本只是内容与 fixture，收获是把"编辑器里能编辑"升级为"策划照着能用"。

### 3.2 C 类（初扫 8 条）按维度归类

> ⚠ 本节是**初扫**的归类。逐条回源码重验后已修正——见 **§4**：2 条判错、2 条降级，真正保留 4 条。

| 维度 | 条目 | 共性 |
|---|---|---|
| **关系型状态**（(A,B) 维度） | 单位绑定、仇恨、隐身/视野 | 不是"某个实体的属性"，单实体组件在结构上表达不了 |
| **新领域运行时** | 跟随施法者的区域/光环、载具/骑乘 | 需要挂进 `IContinuous` 的领域 runtime |
| **投射物域扩展** | 墙壁反射、弹射/连锁 | 在 `ProjectileWorld` 内加反射分支 / 命中策略 |
| **实体生命周期与同步契约** | 召唤物回滚/重连 | 回滚需要增删实体，是生命周期问题不是字段问题 |

**初扫结论**（重验后部分修正）：主体是"关系型状态"和"领域运行时"，不是"再加一个组件"。**§4 的重验进一步表明：这些项多数落在"差一个开放扩展点 + 关系型存储 + 契约"上，而非"缺机制"。**

## 4. C 类逐条重验与设计要点（2026-09-15）

初版 8 条 C 逐条回源码重验后，**2 条判错、2 条降级**。**真正需要新机制的只剩 4 条。**

### 4.1 重验修正

| 原判 | 重验 | 依据 |
|---|---|---|
| 多来源独立标记 | **✗ 判错 → ✓ 能表达** | `BuffRuntimeKey.MatchApplyRequest` 用 `MatchBuffAndSource(buffId, **sourceActorId**)`，且支持 `MatchInstance(buffId, sourceActorId, sourceContextId)`；`MobaBuffService` 已在 apply 时填这两个字段。初版只读了叠层 applier 的 `SourceId` 赋值就下了结论——那是在**已按来源匹配到同一 runtime** 时的覆盖，属正确行为 |
| 墙壁反射 | **✗ 判错 → A** | `ProjectileHitEvent` **已携带 `Normal`**（`ProjectileEvents.cs:61`），碰撞层 `RaycastHit` 也返回法线。`ProjectileWorld` 只是没用它 |
| 隐身（排除） | **C → A** | 加一个 `[MobaTargetFilter]` 按 tag 排除的规则即可；targeting 是**特性注册 + codegen 清单**的开放扩展点 |
| 仇恨 | **C → A + 存储** | 打分器走 `[MobaTargetOrder]` 开放注册；缺的只是威胁表（per-pair 存储）与其回滚 |

### 4.2 真正保留的 C（4 条）

| 项 | 落点 | 最小实现面 | 契约影响 | 成本 |
|---|---|---|---|---|
| **单位绑定**（共享伤害/链接） | 世界级**关系服务** | 双向索引关系表 + `damage.apply.before` 阶段的重定向/分摊 handler + 解绑时机（despawn / buff 结束）。关系映射有先例：`MobaSummonService._summonsByRootOwner` | 回滚 + 重连 + 定点分摊 + 稳定遍历序 | 中（参照 `MobaShieldRollbackProvider` 419 行量级） |
| **跟随施法者的区域/光环** | 领域 runtime（挂 `IContinuous`）+ 消费既有 `AttachMode` | `AttachMode` 在生成路径被消费；center 按 owner 每帧更新（或注入位置 provider）；owner 死亡/失效即结束 | **会暴露既有缺口**：区域状态当前**无快照**，跟随让位置成为随时间变的权威状态，必须补 area snapshot + 回滚 | 中低 |
| **载具/骑乘** | 领域 runtime（**组装已有原语**） | `MountService` 上下车事务 + 乘客表（per-entity 容器）+ 输入所有权切换（`PlayerActorMap` 已有接缝）+ 载具运动源（复用 `MobaMotionContinuousRuntime`）+ 销毁补偿（照 `MobaHeroReplacementTransactionService`） | 乘客关系进快照/回滚；输入所有权切换要与回滚协同（切回去） | 高（本表最大），但**不是新范式** |
| **召唤物回滚/重连** | **已裁定：销毁预测 + 重执行创建**（见 §4.6） | ① **根因不是 importer 漏写**（2026-09-15 追查修正）：`MobaActorSpawnSnapshotService.Enqueue` 的调用方只有**英雄生成 / 英雄替换 / 投射物**三处，**战斗中召唤物从不进 spawn 快照**；且载荷 `MobaActorSpawnSnapshotEntry` 只有 `NetId/Kind/Code/OwnerNetId/X/Y/Z`、`SpawnEntityKind` 只有 `Character/Projectile` —— **没有承载"这是召唤物"的位置**。importer 的"小兵/召唤物按 Unit 处理（P1 简化）"实际只覆盖**开局 roster**（经 `PublishSpawnPayload` 下发）。② **裁定走"销毁预测 + 重执行"**：权威侧只需一份存活实体 id 集合；provider 只负责幸存实体状态。**前提：`ActorIdAllocator` 必须进回滚**（当前不参与，见 §4.6） | 回滚（id 集合 + 分配器）；跨会话恢复后置 | **低**（前提修复约三十行），跨会话部分**暂不承诺** |

### 4.3 降级项：都是"差一个可复用扩展点"

| 项 | 要加什么 | 规约 |
|---|---|---|
| 墙壁反射 | 反射分支/策略（用已有的 `Normal`） | 见下方 ⚠ |
| 弹射/连锁 | 命中策略（需要"命中后解析下一个目标"） | 见下方 ⚠ |
| 隐身（排除） | `[MobaTargetFilter]` 按 tag 排除规则（可能另需按 tag 取候选的 provider） | 同 PlanAction 的清单规约 |
| 仇恨 | `[MobaTargetOrder]` 威胁打分器 + 威胁表存储 | 同上 |

> **⚠ 新发现的架构问题：扩展点开放性不一致。**
> targeting（`[MobaTargetFilter]` / `[MobaTargetOrder]`）与 PlanAction（`[PlanActionModule]`）都走**特性 + codegen 清单**的开放扩展点；而投射物的 `ProjectileHitPolicyFactory` 是 `internal` 的**硬编码 switch**，`IProjectileHitPolicy` 没有注册 API。
> 后果：加命中策略必须改**框架包**，而不能像前两者一样在项目侧扩展。
> 建议：把投射物命中策略也改成注册表 + 特性（与既有两处一致）。**注意**：若 bounce 需要在 projectile 域内解析"下一个目标"，不要把 targeting 包反向拉进 projectile 包——应通过注入的 provider 接口（`ProjectileWorld` 已有 `IProjectileTrackingTargetProvider` 这类先例）。

### 4.4 C 类整体结论（修正）

```
原判："8 条缺机制，主体是关系型状态与领域运行时"
修正："真正需新机制 4 条；另 3 条只是差一个可复用扩展点 + 1 条判错"

需补的是两类东西：
  (a) 若干开放扩展点（目标规则 / 打分器 / 命中策略）——规约已存在，照抄即可
  (b) 几个关系型存储 + 它们的回滚 / 重连契约——这才是真成本
```

**这套系统的"机制完备度"比初版矩阵呈现的高；短板在契约层（回滚与重连），不在机制层。**

### 4.5 关于"召唤物不支持同步"的范围界定

MOBA 默认走 **Lockstep**（帧同步），所有端跑同一套定点模拟——**正常对局中召唤物在各端自然重建，不走快照**。上面那条缺口真正影响的是 **中途加入 / 断线重连的恢复路径**，而不是常规对战。

所以它是一个**范围决策**，不是 bug：

| 选项 | 内容 | 代价 |
|---|---|---|
| A. 复制召唤物 | 把 `spawn_summon` 纳入 spawn/despawn 快照；需给 `MobaActorSpawnSnapshotEntry` 加 `UnitSubType`/`SummonId`（**协议变更**，按 `protocol-wire` 走 Wire Schema v2 + 重新生成 + 兼容 revision） | 中-高 |
| B. 恢复时重演 | 重连时从权威快照恢复到某个帧，再由确定性模拟重建召唤物 | 低（若链路已支持"从帧恢复 + 重演"），但要验证触发/预算在重演下幂等 |
| C. 显式不承诺 | 重连不恢复召唤物（当前事实行为），在文档与协议契约里写明 | 零，但要在验收里挡住"以为恢复了" |

**未做决策前不应改动 importer 或 payload**——改 importer 是无效的（它根本收不到召唤物）。

### 4.6 裁定：走"销毁预测 + 重执行创建"，状态回滚后置（2026-09-15）

**决定**：这类由模拟创建/销毁的实体（召唤物、投射物）不走状态快照回滚。改为——权威侧维护一份**当前存活的实体 ID 集合**，客户端回滚时把本地有而权威集合没有的**预测实体删除**，然后**重新执行命令帧**把它们创建出来。逐实体的组件数据回滚**后置处理**。

**为什么成立**：这把"增删实体"从 rollback provider 的职责里摘了出去——provider 只管**幸存实体的状态**，绕开了 `MobaShieldRollbackProvider` 头注释记录的硬约束（*cannot safely create or destroy simulation entities*）。权威数据从"逐实体状态"降为"一份 id 集合"。

**必须先补的前提：id 复现。** 重执行会调用 `ActorIdAllocator.Next()`，而该分配器当前**不参与回滚**：

| 事实 | 证据 |
|---|---|
| 纯计数器、单 int | `MobaActorSpawnService.cs:119` 注入 `ActorIdAllocator`；`ActorIdAllocator.Next()` 单调递增 |
| **无 rollback provider 覆盖** | `MobaRollbackRegistryBuilder` 的 15 个 provider 中不含 actor id |
| **`Reset()` 无调用点** | 运行时与测试全仓 grep 无命中 |
| importer 是**绕开**而非同步 | `MobaLogicWorldStateImporter.cs:261` `AllocateActorIdIfMissing = false`，注释"必须使用快照中的 actorId 与服务端对齐" |

后果：计数器不回退 → 重建得到**更高的新 id** → 与 owner link / 命令 target / trace / 快照 netId 全部对不上。
**修复成本低**：给 `ActorIdAllocator` 加一个 rollback provider（单 int，形状照抄 `MobaOwnerBlackboardRollbackProvider`），比协议方案便宜一个数量级。

**两点待确认**（未经验证，勿当结论）：

1. 删除判据需按**已确认帧**划边界，否则会误删"本地领先于权威、尚未被确认"的新实体。
2. 重执行需要**从创建帧起**的输入/事件可回放。同会话短距回滚满足；跨会话（重连 / 中途加入）不满足——与"后置处理"的范围一致。

## 5. 待核清单

- 兵线 / 刷新节奏能否用 `gameplay.tick` + 触发器 + 计时组合表达
- 召唤物是否真的完全不支持跟随（`MobaSummonService` 注释为"只在生成时定位"）
- 按来源分实例的标记是否可通过"每个来源一个 buff id"近似（属 E，需明确否决或接受）
- `aoes` 的 Enter/Exit/Interval 触发 id 是配置面遗漏还是刻意留给 `area.*` 全局事件

## 6. 复用说明

本矩阵用**现有能力清单**扫描得出，未新写任何配置。若要继续深化：

1. 对 C 类逐条做**设计草案**（新服务/新维度/新契约），评估成本与爆炸半径
2. 对 B 类逐条**补一条生产内容 + 验收**，把"编辑器里能编辑"变"策划照着能用"
3. 把判定规则（§1 口径）接进 `moba-content-contracts` gate，让新增技能自动落格

---

*生成方式：按六条机制轴扫描 36 个 PlanAction 动词参数面、14 种 skill_flow 阶段、14 种触发器执行节点与全部 MOBA 配置表字段；判定规则见 skill `moba-skill-authoring` §1。*
