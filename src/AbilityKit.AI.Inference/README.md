# AbilityKit AI 推理

此包定义 AbilityKit AI 在服务器端的学习策略边界。

稳定的运行时契约仍位于 `AbilityKit.AI.Abstractions`：

- `IAiEnvironment` 负责重置和步进模拟。
- `IAiPolicy` 负责动作选择。
- `AiObservationBuffer` 和 `AiActionBuffer` 是数据交换格式。

`AbilityKit.AI.Inference` 将学习模型执行器适配为 `IAiPolicy`，不依赖具体模型运行时包。后续 ONNX Runtime 应作为独立执行器实现接入，不应成为 AI 抽象或训练环境包的依赖。

## 当前边界

`AiModelPolicy` 是服务器及无界面运行器使用的策略适配器。

`IAiModelExecutor` 是模型运行时接入接口。ONNX 执行器、原生推理执行器或确定性测试执行器都可实现此接口，无需修改 Shooter 或 Moba 环境。

`AiModelPolicySpec` 记录观测长度、动作长度和张量名，在驱动玩法前集中验证训练模型是否匹配目标环境。

## 后续方向

预期的生产路径：

1. 使用 Unity ML-Agents 或其他训练器训练。
2. 导出学习模型。
3. 通过 `IAiModelExecutor` 实现加载模型。
4. 在纯 C# 服务器及运行时路径中作为 `IAiPolicy` 执行。

Unity 训练工具因此保持可选，同时 Shooter、Moba 和后续示例共享同一运行时 AI 契约。
