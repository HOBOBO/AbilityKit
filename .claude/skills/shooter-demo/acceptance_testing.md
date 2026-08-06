# 验收与基准测试

## ShooterAcceptanceSpecs（纯 C# 验收基线）

`view.runtime/Runtime/Client/Synchronization/ShooterAcceptanceSpecs.cs`：

不依赖 Unity，AI/CI/Unity 共用同一份规格。最核心的 `BasicCombat`：

```
玩家数：2
帧数：6
随机种子：1401
预期：玩家能正常移动 + 开火 + 命中
```

## ShooterAcceptanceSpecRunner

`view.runtime/Runtime/Client/Synchronization/ShooterAcceptanceSpecRunner.cs`：

- 加载 spec
- 创建 world（不进 Unity PlayMode）
- 跑指定帧数
- 对比结果（snapshot hash / 关键字段）
- 输出 pass/fail 报告

用途：CI 回归、ML-Agents 训练前 sanity check、跨平台确定性验证。

## ShooterDeterminismSpecRunner

`runtime/Runtime/Application/Synchronization/ShooterDeterminismSpecRunner.cs`：

- 跨进程确定性验证（同输入下不同机器/平台必须产出相同 hash）
- 检测浮点不稳定性、迭代顺序问题、状态泄漏

## ShooterSveltoGameplayBenchmark

`runtime/Runtime/Domain/Gameplay/Scenario/ShooterSveltoGameplayBenchmark.cs`：

- Svelto ECS 性能跑分
- 测量大数量实体下的 tick 性能

配套：
- `ShooterSveltoGameplayScenarioRunner`
- `ShooterSveltoGameplayScenarioConfig`（默认 `WaveSurvival`）
- `ShooterSveltoGameplayScenarioResult`
- `ShooterSveltoGameplayScenarioJsonSource`（可选 JSON 配置）
- `ShooterSveltoGameplayScenarioInitializer`（Composition Root）
- 子系统：
  - `ShooterSveltoGameplayScenarioWaveSpawnSystem`
  - `ShooterSveltoGameplayScenarioProjectileSystem`
  - `ShooterSveltoGameplayScenarioShooterDecisionSystem`
  - `ShooterSveltoGameplayScenarioEnemyDecisionSystem`
  - `ShooterSveltoGameplayScenarioResultCollector`

## src/ 下测试

`src/AbilityKit.Demo.Shooter.Runtime.Tests/`：

- `Client/ShooterRemoteCoordinatorInputContractTests.cs` — **断言 shooter 远程输入路径不经 coordinator**（断言 view.runtime asmdef 不引用 `AbilityKit.Coordinator`，且 `Hosting/ShooterCoordinator*` 文件不存在）

## 推荐工作流

```
1. 改代码后先跑 ShooterAcceptanceSpecRunner 验证 BasicCombat（< 1 秒）
2. 跑 ShooterDeterminismSpecRunner 验证确定性（跨进程）
3. 进 Unity PlayMode 用 RemoteStateSync 连本地服务器做联机测试
4. 跑 ShooterSveltoGameplayBenchmark 看性能回归
5. AI 训练前用 ML-Agents 接 ShooterAiTrainingEnvironment
```

## AI 训练

`com.abilitykit.demo.shooter.ai/Editor/ShooterAiTrainingEnvironment.cs`：

- `IAiEnvironment` 实现（基于 `com.abilitykit.ai.abstractions`）
- `ShooterAiObservationBuilder`：构造观察
- `ShooterAiActionMapper`：动作映射
- `ShooterAiRewardEvaluator`：奖励评估
- `AiObservationBuffer`：观察缓冲

训练环境用 `ShooterAcceptanceSpecs` 同款纯 C# 模拟，保证 Unity 内外一致。
