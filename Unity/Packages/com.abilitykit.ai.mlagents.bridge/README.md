# AbilityKit AI ML-Agents 桥接包

此包有意设计为可选包。AbilityKit AI 运行时契约位于 `com.abilitykit.ai.abstractions`，不依赖 Unity 或 ML-Agents。只有 Unity 训练工作流需要时，桥接包才负责把 `IAiEnvironment` 适配到 Unity ML-Agents。

## 定位

稳定契约如下：

- 服务端/无界面训练和运行时推理使用 `IAiEnvironment`、`IAiPolicy`、`AiObservationBuffer` 和 `AiActionBuffer`。
- Unity ML-Agents 是训练前端，而不是核心 AI 运行时 API。
- 学习得到的运行时模型后续应重新接入 `IAiPolicy`，使 Shooter 和 Moba 可以共享同一条服务端推理路径。

这样可以避免玩法模拟与 ML-Agents 耦合，并使服务端 C# 运行时独立于 Unity 包。

## 启用 ML-Agents

1. 在 Unity 项目中安装 Unity ML-Agents。
2. 添加脚本定义符号 `ABILITYKIT_ML_AGENTS`。
3. 从本包导入 `Shooter ML-Agents Agent Skeleton` 示例。
4. 把示例智能体组件添加到训练场景。
5. 配置 ML-Agents 的 `Behavior Parameters` 组件，使其与环境规格一致：
   - 观察向量长度：`ShooterAiTrainingEnvironment.ObservationSpec.Length`。
   - 连续动作数：`ShooterAiTrainingEnvironment.ActionSpec.ContinuousLength`。
   - 离散动作数：`ShooterAiTrainingEnvironment.ActionSpec.DiscreteLength`。

## Shooter 示例

Shooter 示例使用 ML-Agents 的 `Agent` 包装 `ShooterAiTrainingEnvironment`，不会替代现有无界面运行器。需要可视化调试或 ML-Agents 训练器时，可用它在 Unity 中训练策略。

自动化服务端训练继续使用 `AbilityKit.AI.Training.Runner` 及其摘要/回合 JSONL 输出。
