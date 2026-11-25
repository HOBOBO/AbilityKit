# AbilityKit AI Abstractions

为玩法推理或训练提供模型无关的观测、动作、策略和环境契约。
`AbilityKit.AI.Abstractions` 是纯 C# Runtime 程序集，不依赖 UnityEngine、其他 AbilityKit 包或具体模型后端。模型执行、训练调度、特征定义、奖励计算和玩法动作映射由消费者提供。

## 为什么需要它

把「游戏逻辑暴露给训练/推理」这件事和「具体 ML 框架」解耦：玩法侧只面向
`IAiEnvironment` / `IAiPolicy` 编程，换 ML-Agents、自研推理或纯 C# 脚本策略都不用改逻辑。

## 契约一览

```csharp
public interface IAiEnvironment
{
    AiObservationSpec ObservationSpec { get; }
    AiActionSpec ActionSpec { get; }
    AiStepResult Reset(in AiEpisodeOptions options);
    AiStepResult Step(in AiActionBuffer action);
}

public interface IAiPolicy
{
    AiActionSpec ActionSpec { get; }
    void Decide(in AiObservationBuffer observation, AiActionBuffer action);
}
```

- `AiObservationSpec` / `AiObservationBuffer`：观测空间及固定长度 `float[]` 缓冲；`Float32`、`Int32`、`Boolean` 是逻辑类型，不更换数组类型或自动转换元素。
- `AiActionSpec` / `AiActionBuffer`：动作空间描述与缓冲（连续 + 离散）。
- `AiEpisodeOptions`：种子、最大步数、固定步长（构造时默认 1/30 秒）；环境负责使用这些参数，调用方应显式构造，不传 `default` 代替。
- `AiStepResult`：借用的观测、奖励、终止 `Done`、截断 `Truncated`、步号与可选状态哈希。步进粒度、奖励和哈希格式由环境定义，状态哈希不是通用快照或跨实现确定性保证。

## 生命周期与缓冲区

- 先 `Reset()` 取初始观测，再依次调用 `Decide()` 和 `Step()`；遇到 `Done` 或 `Truncated` 停止推进，开新一局时再次 `Reset()`。
- `AiStepResult.Observation` 借自环境，后续 `Step()` 或 `Reset()` 可以覆盖同一缓冲。只读消费；需要保留历史观测时，在下一次环境调用前复制数据。复制结果结构体不会复制底层数据。
- 动作缓冲由调用方分配和复用；策略在调用期间读取观测并写入动作，环境在 `Step()` 中读取动作。`in` 不会把引用类型变成只读对象。两个缓冲的数组属性均返回可写底层数组，不复制、不自动清零，接口也不提供线程同步。
- 调用方应匹配环境的观测和动作规格。复用动作前若策略只写部分槽位，应先调用 `Clear()`，以免上一帧残留值进入本次动作。

## 典型用法

```csharp
IAiEnvironment env = new MyBattleEnvironment();
IAiPolicy policy = new MyScriptedPolicy();

var action = new AiActionBuffer(env.ActionSpec);
var step = env.Reset(new AiEpisodeOptions(seed: 42, maxSteps: 1800));
while (!step.Done && !step.Truncated)
{
    action.Clear();
    policy.Decide(step.Observation, action);
    step = env.Step(action);
}
```

配套桥接包：`com.abilitykit.ai.mlagents.bridge`（把本契约适配到 Unity ML-Agents）。

## 可选的策略快照

普通推理只需 `IAiPolicy`。有状态策略可通过 `IAiPolicyRuntimeSnapshot` 捕获和恢复自身状态；需要在恢复前校验的宿主可要求 `IAiPolicySnapshotValidator`。`IAiStatelessPolicy` 是无隐藏跨步状态的显式承诺。以上接口不保存环境状态，也不实现训练检查点或回滚调度。

## 依赖

无 AbilityKit 内部依赖。Unity 2022.3+。
