# AbilityKit AI Abstractions

模型中立的强化学习/训练环境契约：观察、动作、策略与环境接口。
不依赖任何其他 AbilityKit 包，是 AI 相关包（如 ML-Agents 桥接）的最底层抽象。

## 为什么需要它

把「游戏逻辑暴露给训练/推理」这件事和「具体 ML 框架」解耦：玩法侧只面向
`IAiEnvironment` / `IAiPolicy` 编程，换 ML-Agents、自研推理或纯 C# 脚本策略都不用改逻辑。

## 契约一览

```csharp
public interface IAiEnvironment
{
    AiObservationSpec ObservationSpec { get; }
    AiActionSpec ActionSpec { get; }
    AiStepResult Reset(in AiEpisodeOptions options);   // 开局，返回初始观察
    AiStepResult Step(in AiActionBuffer action);        // 推进一帧，返回观察/奖励/结束
}

public interface IAiPolicy
{
    AiActionSpec ActionSpec { get; }
    void Decide(in AiObservationBuffer observation, AiActionBuffer action);
}
```

- `AiObservationSpec` / `AiObservationBuffer`：观察空间描述与缓冲（float/int/bool）。
- `AiActionSpec` / `AiActionBuffer`：动作空间描述与缓冲（连续 + 离散）。
- `AiEpisodeOptions`：种子、最大步数、固定步长（默认 1/30s，与战斗帧率对齐）。
- `AiStepResult`：观察、奖励、done/truncated、步号、状态哈希（可校验确定性）。

## 典型用法

```csharp
var env = new MyBattleEnvironment();          // 玩法侧实现 IAiEnvironment
var policy = new MyScriptedPolicy();          // 策略侧实现 IAiPolicy（脚本或模型）

var step = env.Reset(new AiEpisodeOptions(seed: 42, maxSteps: 1800));
while (!step.Done && !step.Truncated)
{
    var action = new AiActionBuffer(env.ActionSpec);
    policy.Decide(step.Observation, action);
    step = env.Step(action);
}
```

配套桥接包：`com.abilitykit.ai.mlagents.bridge`（把本契约适配到 Unity ML-Agents）。

## 依赖

无 AbilityKit 内部依赖。Unity 2022.3+。
