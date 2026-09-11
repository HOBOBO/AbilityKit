# Combat Sample Coverage Plan

> 目标：把 AbilityKit 已有的战斗相关能力拆成循序渐进的 samples，让新人可以从“一个实体、一次命中”逐步理解到“技能、投射物、区域、World、帧同步、回放”的完整链路。

## 当前覆盖结论

现有 samples 已经覆盖了一部分基础概念，第一轮 combat samples 已补齐实体、技能、目标、伤害、碰撞、区域和投射物命中的最小链路；后续仍需要继续补技能组合、运动、World、同步和回放：

- 已有 `TargetingBasics` 仍是旧的单点演示，新的主要教学入口已迁到 `combat/targeting-index-provider`，并接入实体阵营索引作为候选来源。
- `EntityManager`、`SkillLibrary`、`Targeting`、`Damage`、`Collision`、`Projectile` 已进入 sample 项目引用；`Motion` 和更高层 World/同步链路还待后续补齐。
- `TowerDefense`、`TimedTowerDefense`、`RPGBattle` 更像早期占位示例，很多输出文本已经不完整，也没有真正展示框架包能力。
- `ProgressiveSkill` 展示了 Pipeline / Triggering / Continuous / HFSM 的演进思路，但没有把战斗包能力串进“选目标 -> 命中 -> 结算伤害 -> 事件/表现”的完整闭环。
- Demo Moba 已经有比较完整的配置、输入、World、同步、表现、回放链路，但对新人来说过大，应该被拆成多个小 sample，而不是直接拿完整工程作为第一入口。

## 战斗能力拆分地图

### 1. 战斗实体与索引

涉及包：

- `AbilityKit.Combat.EntityManager`
- `AbilityKit.Combat.SkillLibrary`

应该拆的示例：

| 建议 id | 目标 | 展示点 | Web |
| --- | --- | --- | --- |
| `combat/entity-registry` | 建立战斗实体目录 | `BattleEntityManager<TId>`、Add/Remove、Registry.Count | 是 |
| `combat/entity-keyed-index` | 按阵营/类型查实体 | `KeyedEntityIndex<TKey,TId>`、阵营索引、类型索引 | 是 |
| `combat/entity-multikey-index` | 一个实体挂多个标签 | `MultiKeyEntityIndex<TKey,TId>`、Buff/状态/标签多索引 | 是 |
| `combat/skill-library-index` | 技能配置目录 | `SkillLibrary<TKey,TData>`、按流派/标签派生索引 | 是 |
| `combat/skill-library-update` | 技能热更新/配置变更 | `Update` 后索引自动调整 | 是 |

价值：新人先理解“战斗对象和技能数据怎么被管理”，后续 Targeting 不再用手写数组当候选来源。

### 2. 目标查找 Targeting

涉及包：

- `AbilityKit.Combat.Targeting`
- 可结合 `AbilityKit.Combat.EntityManager`

应该拆的示例：

| 建议 id | 目标 | 展示点 | Web |
| --- | --- | --- | --- |
| `combat/targeting-pipeline` | 最小搜索链路 | Provider -> Rule -> Scorer -> Selector | 是 |
| `combat/targeting-shapes` | 圆形/扇形范围选敌 | `CircleShapeRule`、`SectorShapeRule`、`IPositionProvider` | 是 |
| `combat/targeting-nearest-topk` | 最近 N 个目标 | `DistanceToEntityScorer`、`TopKByScoreSelector` | 是 |
| `combat/targeting-streaming-topk` | 高频搜索优化 | `StreamingTopKByScoreSelector`、候选统计 | 是 |
| `combat/targeting-index-provider` | 从实体索引提供候选 | EntityManager 阵营索引 -> CandidateProvider | 是 |
| `combat/targeting-deterministic-random` | 确定性随机选择 | `SeededHashRandomScorer`、seed 数据、稳定 key | 是 |

价值：把“查谁”拆细，避免直接进入技能逻辑时 Targeting 成为黑盒。

### 3. 伤害计算 Damage

涉及包：

- `AbilityKit.Combat.Damage`
- `AbilityKit.Dataflow`

应该拆的示例：

| 建议 id | 目标 | 展示点 | Web |
| --- | --- | --- | --- |
| `combat/damage-basic` | 一次物理/魔法伤害 | `DamageRequest`、`DamageCalculationContext`、`DamageResult` | 是 |
| `combat/damage-defense` | 护甲/魔抗减免 | `TargetArmor`、`TargetMagicResist`、减免过程日志 | 是 |
| `combat/damage-slots` | 强类型数据槽 | `DamageSlots.DamageBonusFlat`、穿透、护盾 | 是 |
| `combat/damage-pipeline` | 完整处理器链 | `DamageCalculationPipeline.CreateDefault()`、每阶段结果 | 是 |
| `combat/damage-apply-to-entity` | 由上层落地扣血 | Damage 只算结果，上层实体状态负责应用 | 是 |

注意：`DamageProcessors.cs` 已移除对 `UnityEngine.Random` 的直接依赖，暴击判定随机值通过 `DamageSlots.CritRoll` 由上层注入，方便纯 .NET sample、回放和确定性测试。

### 4. 碰撞与范围 Collision / Area

涉及包：

- `AbilityKit.Combat.Collision.Abstractions`
- `AbilityKit.Combat.Projectile`

应该拆的示例：

| 建议 id | 目标 | 展示点 | Web |
| --- | --- | --- | --- |
| `combat/collision-world` | 建立纯逻辑碰撞世界 | `NaiveCollisionWorld`、Sphere/Aabb/Capsule | 是 |
| `combat/collision-raycast` | 子弹/投射物射线命中 | `Ray3`、`RaycastHit`、layerMask | 是 |
| `combat/collision-overlap` | AOE 查询 | `OverlapSphere`、结果排序 | 是 |
| `combat/area-enter-stay-exit` | 区域持续检测 | `AreaWorld`、Enter/Stay/Exit/Expire 事件 | 是 |
| `combat/area-dot` | 区域持续伤害 | AreaStay -> DamagePipeline -> 扣血 | 是 |

价值：让新人理解投射物和 AOE 不是 Unity Collider 的直接包装，而是可替换的逻辑碰撞抽象。

### 5. 运动 Motion

涉及包：

- `AbilityKit.Combat.Motion`
- `AbilityKit.Combat.Collision.Abstractions`

应该拆的示例：

| 建议 id | 目标 | 展示点 | Web |
| --- | --- | --- | --- |
| `combat/motion-trajectory` | 轨迹运动 | `LinearTrajectory3D`、`TrajectoryMotionSource`、`MotionState` | 是 |
| `combat/motion-stack-policy` | 多运动源叠加/覆盖 | `MotionStacking`、priority、group | 是 |
| `combat/motion-leash` | 位移边界限制 | `ConfigurableMotionSolver`、Leash constraints | 是 |
| `combat/motion-collision` | 位移碰撞裁剪 | MotionSolver + CollisionWorld sweep | 是 |

价值：把冲刺、击退、牵引、路径移动这类能力从技能流程里拆出来。

### 6. 投射物 Projectile

涉及包：

- `AbilityKit.Combat.Projectile`
- `AbilityKit.Combat.Collision.Abstractions`
- 可结合 `AbilityKit.Combat.Damage`

应该拆的示例：

| 建议 id | 目标 | 展示点 | Web |
| --- | --- | --- | --- |
| `combat/projectile-basic-hit` | 单发投射物命中即消失 | `ProjectileWorld.Spawn`、`ExitOnHitPolicy`、Hit/Exit 事件 | 是 |
| `combat/projectile-pierce` | 穿透多个目标 | `PierceHitPolicy`、`HitsRemaining` | 是 |
| `combat/projectile-cooldown-filter` | 同一目标命中冷却 | `IProjectileHitFilter`、`HitCooldownFrames` | 是 |
| `combat/projectile-patterns` | 单发/扇形/散射/连发 | `SingleShotPattern`、`FanPattern`、`ScatterPattern`、`BurstPattern` | 是 |
| `combat/projectile-return` | 回旋/返回投射物 | `ReturnAfterFrames`、`IProjectileReturnTargetProvider` | 是 |
| `combat/projectile-rollback` | 投射物状态快照 | `ExportRollback`、`ImportRollback`、确定性恢复 | 是 |
| `combat/projectile-hit-damage` | 命中后结算伤害 | ProjectileHitEvent -> DamagePipeline -> entity hp | 是 |

价值：投射物是战斗包能力最能体现“跨帧 + 命中 + 状态恢复”的模块，应作为 combat samples 的主线之一。

### 7. 技能执行链路

涉及包：

- `AbilityKit.Pipeline`
- `AbilityKit.Flow`
- `AbilityKit.Triggering`
- `AbilityKit.Modifiers`
- `AbilityKit.GameplayTags`
- 战斗包：Targeting / Damage / Projectile / Area / Motion

应该拆的示例：

| 建议 id | 目标 | 展示点 | Web |
| --- | --- | --- | --- |
| `combat/skill-cast-check` | 技能释放前检查 | 标签、法力、冷却、目标合法性 | 是 |
| `combat/skill-target-damage` | 指向技能闭环 | Targeting -> Damage -> ApplyHp -> Event | 是 |
| `combat/skill-projectile` | 投射物技能 | Cast -> SpawnProjectile -> Hit -> Damage | 是 |
| `combat/skill-area-dot` | 地面 AOE/DOT | SpawnArea -> Stay -> DamageOverTime | 是 |
| `combat/skill-buff-modifier` | Buff 修改属性 | Triggering 事件 -> Modifier 叠加/衰减 | 是 |
| `combat/skill-flow-channel` | 引导/蓄力技能 | Flow Wait/Cancel -> Cast Result | 是 |
| `combat/skill-state-gate` | 状态机限制技能 | HFSM 状态决定可释放动作 | 是 |

价值：这组示例应该替代现有偏占位的 `TowerDefense/RPGBattle`，成为“从零到一理解战斗框架”的核心路径。

### 8. World / Host / FrameSync / StateSync / Replay

涉及包：

- `AbilityKit.Host`
- `AbilityKit.World.DI`
- `AbilityKit.World.FrameSync`
- `AbilityKit.World.Snapshot`
- `AbilityKit.World.StateSync`
- `AbilityKit.Game.Battle.Runtime`
- `AbilityKit.Game.Battle.Transport.Runtime`
- `AbilityKit.Record`
- `AbilityKit.Record.MemoryPack`

应该拆的示例：

| 建议 id | 目标 | 展示点 | Web |
| --- | --- | --- | --- |
| `battle/world-composition` | 战斗 World 服务组合 | WorldContainerBuilder、服务注册、模块边界 | 是 |
| `battle/frame-input` | 一帧输入如何进入逻辑 | PlayerInputCommand、SubmitInputRequest、帧号 | 是 |
| `battle/frame-simulation` | 固定帧推进 | IWorldClock / IFrameTime / Execute phase | 是 |
| `battle/snapshot-dispatch` | 快照拆分与分发 | actor transform / damage / projectile / area event | 是 |
| `battle/state-hash` | 状态一致性检查 | StateHashData、确定性验证 | 是 |
| `battle/replay-record-playback` | 录制与回放 | Record writer/player、快照间隔 | 否，先控制台 |
| `battle/transport-contract` | 传输层抽象 | `IBattleLogicTransport`、Create/Join/Leave/Input | 是 |
| `battle/sync-adapter-fake` | 本地假传输适配器 | 不开网络，模拟 FramePushed | 是 |

注意：真实网络、ET/Orleans、Unity WebGL 等不应放进第一批 Web sample。第一批应使用 fake transport / in-memory snapshot，保证纯逻辑可运行。

## 推荐落地顺序

### Phase A：先补 sample 项目引用和测试夹具

目标：让 samples 能编译并引用战斗包。

建议新增引用：

- `AbilityKit.Combat.EntityManager`
- `AbilityKit.Combat.SkillLibrary`
- `AbilityKit.Combat.Damage`
- `AbilityKit.Combat.Collision.Abstractions`
- `AbilityKit.Combat.Motion`
- `AbilityKit.Combat.Projectile`
- `AbilityKit.Game.Battle.Runtime`
- 后续再看是否接 `World.FrameSync`、`World.Snapshot`、`Record`

同时新增 sample 内部测试夹具：

- `SampleBattleEntity`
- `SampleBattleWorld`
- `SamplePositionProvider`
- `SampleCandidateProvider`
- `SampleCollisionScenario`
- `SampleDamageApplier`

这些夹具只服务 sample，不进入框架包，避免污染正式 API。

### Phase B：纯战斗基础能力

优先补 web 可运行示例：

1. `combat/entity-keyed-index`
2. `combat/skill-library-index`
3. `combat/targeting-index-provider`
4. `combat/damage-pipeline`
5. `combat/collision-raycast`
6. `combat/area-enter-stay-exit`

目标：新人看完后知道实体、技能、目标、伤害、碰撞这些基础件各自负责什么。

### Phase C：跨帧命中链路

优先补：

1. `combat/projectile-basic-hit`
2. `combat/projectile-pierce`
3. `combat/projectile-hit-damage`
4. `combat/area-dot`
5. `combat/motion-trajectory`

目标：展示持续驱动和跨帧模拟，适合 Web 静态导出，因为导出时可以预跑固定帧并展示每帧日志。

### Phase D：技能组合与事件驱动

优先补：

1. `combat/skill-target-damage`
2. `combat/skill-projectile`
3. `combat/skill-area-dot`
4. `combat/skill-buff-modifier`
5. `combat/skill-flow-channel`

目标：把现有 `ProgressiveSkill` 升级成真正接入战斗包的渐进式技能示例。

### Phase E：战斗运行环境

优先补：

1. `battle/world-composition`
2. `battle/frame-input`
3. `battle/snapshot-dispatch`
4. `battle/transport-contract`
5. `battle/sync-adapter-fake`

目标：让新人理解真实项目不是直接调用 sample，而是由 Host/World/Transport 驱动纯逻辑。

## Web 模式筛选建议

适合默认进入 `web` 标签：

- 确定性、短流程、纯内存、无网络、无 UnityEngine、不会跑很久的示例。
- Targeting / Damage / Collision / Area / Projectile fixed frames / Skill chain。

不建议默认进入 `web` 标签：

- 真实网络连接。
- ET/Orleans/Unity 运行环境。
- 需要持续监听输入或长时间运行的回放。
- 依赖未隔离随机源的示例。

## 现有旧示例处理建议

- `TargetingBasics`：保留但重写为第一批 `combat/targeting-pipeline`，输出改为结构化日志，接入实体索引 Provider。
- `TowerDefense`、`TimedTowerDefense`、`RPGBattle`：不再作为主要教学入口，后续迁移为 `combat/skill-*` 或 `battle/demo-*` 系列。
- `ProgressiveSkill`：保留“循序渐进”的形式，但 Phase 需要补战斗包接入。建议从 `Phase2/Phase3` 开始接 `Damage` 和 `Targeting`，后续接 `Projectile`、`Area`、`Modifier`。
- `WorldServicesDeepDive`：当前会触发循环依赖栈溢出，修复前不要进入 web 导出。

## 最小优先清单

第一轮已完成这 8 个：

1. `combat/entity-keyed-index`
2. `combat/skill-library-index`
3. `combat/targeting-index-provider`
4. `combat/damage-pipeline`
5. `combat/collision-raycast`
6. `combat/area-enter-stay-exit`
7. `combat/projectile-basic-hit`
8. `combat/projectile-hit-damage`

这 8 个能形成第一条完整战斗学习链：

```text
Entity/Skill data
  -> Targeting
  -> Damage
  -> Collision
  -> Area/Projectile
  -> Hit event
  -> Apply result
```

完成后，再补技能组合、World、同步、回放，学习曲线会顺很多。
