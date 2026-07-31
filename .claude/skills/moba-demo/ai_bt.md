# AI 行为树修复与配置

基于源码核校（2026-07-29）。覆盖 BT 召唤守卫移动修复、寻路接入。

## 召唤守卫 BT 移动修复

### 背景
`summon_warden_bt`（BrainId=3）的守卫在施法者位置生成后，若不移动——生成距离在 BT 的 `DefaultApproachRange=1.5f` 内时（如生成点距敌人 <1.5m），`MobaShouldApproachEnemyCondition` 返回 false → 不移动。

### 修复（2026-07-29）
- `DefaultApproachRange` 1.5→**0.5f**（`MobaSelectReadySkillAction.cs`）
  - 0.5f = 碰撞半径，合理的近战逼近范围——守卫会更积极地追击
  - 影响：无显式 `MobaSelectReadySkillAction` 节点的 BT（召唤物/小兵）更积极逼近
- `CreateSummon()` + `CreateMinion()` 加 `.WithMoveInput()`（`MobaActorArchetypeAssembler.cs`）
  - 与 `CreateHero()` 一致，MoveInput 组件创建即有，消除首次 BrainOutputApply 才 Add 的时序脆弱性

### 验证
召唤烟雾测试 `MobaSummonBTreeSkillSmokeTests` 由 fail→pass（179/179）

## BT 寻路接入（PathFollowing）

BT 节点本身不需要显式障碍感知——寻路在 motion 层以下完成：
1. BT (`MobaMoveToEnemyAction`) 设置 `Output.Movement.TargetPosition` = 敌人位置
2. `MobaPathFollowingSystem` 读脑输出 → `FindPath` → `PathFollowerMotionSource`
3. Policy `Path→[Locomotion]` 抑制直线 Locomotion → Path 源接管移动
4. 寻路失败时 BrainOutputApply 的直线 MoveInput 兜底

详见 [path_following.md](path_following.md)。

## BT 配置

### 脑配置
`Configs/moba/brains.json`：
- BrainId=1 和 BrainId=3 都映射 `summon_warden_bt`
- DriverKind: `behaviorTree` → `MobaBTreeBrainDecisionDriver`

### BT JSON
`Configs/moba/bt/summon_warden_bt.json`：
```
Sequence "RefreshPlanArbitrate"
  [1] MobaSelectNearestEnemyAction "QueryNearestEnemy" (searchRadius=1000)
  [2] Selector "BuildMoveOrHold"
    [2a] Sequence "BuildMoveIntent"
      MobaHasEnemyCondition, MobaCanMoveCondition,
      MobaShouldApproachEnemyCondition, MobaMoveToEnemyAction
    [2b] MobaHoldPositionAction
  [3] MobaArbitrateCombatIntentAction "ArbitrateIntent"
```
缺 `MobaSelectReadySkillAction` 节点 → `SkillApproachRange=0` → 回退 `DefaultApproachRange`（现 0.5f）

## 相关
- 寻路跟随 → [path_following.md](path_following.md)
- 服务 → 见 ability-kit skill 的 [skill_buff/README.md](../ability-kit/skill_buff/README.md)
