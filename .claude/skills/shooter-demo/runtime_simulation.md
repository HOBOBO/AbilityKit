# ShooterBattleRuntimePort + Simulation

## ShooterBattleRuntimePort（核心 facade）

位置：`runtime/Runtime/Application/Runtime/ShooterBattleRuntimePort.cs`

实现 9 个端口接口：

```csharp
public sealed class ShooterBattleRuntimePort :
    IShooterBattleRuntimePort,
    IShooterGameStartPort,
    IShooterInputPort,
    IShooterSimulationClock,
    IShooterSnapshotReadPort,
    IShooterStateHashProvider,
    IShooterPackedSnapshotPort,
    IShooterPureStateSnapshotPort
{
    public MobaGameStartResult StartGame(...);
    public void SubmitInput(...);
    public void Tick(float deltaTime);
    public void GetSnapshot(out ShooterStateSnapshotPayload payload);
    public void ExportPackedSnapshot(...);
    public void ExportPureStateSnapshot(...);
    public WorldStateHash ComputeStateHash();
    // Bot AI mount
}
```

## ShooterBattleSimulation（一帧双 module 管线）

位置：`runtime/Runtime/Domain/Battle/ShooterBattleSimulation.cs`

```csharp
public sealed class ShooterBattleSimulation {
    public void Tick(float deltaTime) {
        ShooterPlayerCommandBattleModule.Tick(deltaTime);    // 玩家移动 + 瞄准 + 开火
        ShooterProjectileCombatBattleModule.Tick(deltaTime); // 投射物位移 + 命中 + 爆炸 + 穿透
    }
}
```

每个 module 都有 `BeginStructuralChanges / EndStructuralChanges`（Svelto 实体创建/删除集中处理）。

## ShooterPlayerCommandBattleModule（玩家命令）

- 读 `ShooterBattleState.InputBuffer` 最新命令
- 归一化移动 + 按 `PlayerSpeed * dt` 位移
- 圆形竞技场裁剪（`ShooterArenaGameplayOptions.CreateCircular`）
- 更新 Aim
- 若 Fire 按 `AttackSlot` 生成子弹：
  - `Primary`（slot 0）：1 发普通弹
  - `Spread`（slot 1）：3 发扇形 + 爆炸
  - `Twin`（slot 2）：2 发穿透

## ShooterProjectileCombatBattleModule（投射物命中）

- 子弹位移 + 减寿 + 出界回收
- `ShooterSpatialPlayerHitIndex` / `ShooterSpatialHitIndex` 做 O(1) 邻格命中查询
- 玩家命中：扣 HP + 加分
- 敌人命中：扣 HP，击杀入 `PendingDefeatedEnemyRemovals` + `DefeatedEnemies++` + 加分
- 爆炸物范围伤害
- 穿透弹 `PenetrationRemaining--` + 沿方向再推一步避免重复命中

## ShooterBattleState（帧/事件/匹配状态）

```csharp
public sealed class ShooterBattleState {
    public int Frame;
    public ShooterInputFrameBuffer InputBuffer;
    public List<ShooterEventSnapshot> Events;   // Hit/Fire/MatchVictory/MatchDefeat/MatchEnded
    public ShooterBattleMatchState MatchState;
    public int VictoryTargetDefeats = 72;       // 击败 72 敌人胜利
    public int TimeLimitFrames;
    public int AllocateBulletId();
}
```

## ShooterBattleRules / ShooterRuleSet

`ShooterRuleSet`：`PlayerSpeed / BulletSpeed / BulletLifeFrames / HitRadius / HitDamage` 等硬编码数值。

## 敌人波次系统

`Domain/Battle/Systems/`：

- `ShooterEnemyWaveBattleSystem` — 总调度
- `ShooterEnemyWaveSpawnDirector` — 波次生成
- `ShooterEnemyWaveMovementBattleSystem` — 敌人移动
- `ShooterEnemyWaveCombatModule` — 敌人战斗
- `ShooterEnemyWaveOptions` / `ShooterEnemyWaveProgress` / `ShooterEnemyIdAllocator`

## 空间网格命中

`Domain/Battle/Systems/`：

- `ShooterSpatialHashGrid` — 网格
- `ShooterSpatialHitIndex` / `ShooterSpatialPlayerHitIndex` / `ShooterSpatialTargetIndex`
- `ShooterSveltoPlayerTargetSelector` — 玩家目标选择

## Bot AI

`Domain/Battle/AI/`：

- `ShooterBotAiRuntime` — 规则 Bot（不依赖 ML-Agents）
- `ShooterBotAiService` — 服务封装
- `ShooterPureSveltoBotAiBattleSystem` — 把 Bot 接入战斗模拟

## ShooterBattlePipelineFactory（Svelto 集成）

`Domain/Battle/Factories/ShooterBattlePipelineFactory.cs`：构建 `ShooterBattleSveltoStepEngine`，注册到 Svelto `EnginesRoot`。
