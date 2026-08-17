# AbilityKit AI 训练数据契约与 JSONL 校验设计

> 本文定义 AbilityKit AI 训练链路的 JSON Lines 数据契约：训练 runner 如何输出 run / episode / step 行，消费端如何按 schemaVersion 做兼容校验，以及 Shooter、Moba 与后续 Python 训练器、离线数据构建器、运行时推理验证之间如何保持稳定边界。目标不是把每个环境的观测语义都写进公共格式，而是先固定跨环境可共享、可校验、可演进的数据外壳。

> 文档类型：Canonical 数据契约设计
>
> 事实基线：2026-08-16
>
> 当前证据：JSONL、Python 训练工具和 C# 加载器具备局部 E3 测试；尚无 AI 专项 E5 CI gate

---

## 1. 能力定位

AI 训练数据契约承担的是跨进程、跨语言、跨示例的数据边界。C# 侧负责用确定性的 headless 训练环境产出 JSONL，Python 或其他训练端负责读取这些行并构建数据集、训练策略或回放分析。

| 能力 | 负责模块 | 说明 |
| --- | --- | --- |
| 契约版本 | `AiTrainingDataContract` | 定义当前支持的 `schemaVersion`，所有输出行必须携带该字段。 |
| 训练报告 | `AiTrainingReportWriter` | 输出一次训练 run 摘要和每个 episode 摘要。 |
| 轨迹数据 | `AiTrainingRolloutJsonLinesWriter` | 输出逐 step 的 observation、action、reward 和 stateHash。 |
| 消费端读取 | `AiTrainingJsonLinesReader` | 读取 JSONL 并校验公共行外壳与字段类型。 |
| 文件级校验 | `AiTrainingJsonLinesValidator` | 统计 run / episode / step 行数，给 CLI 和 CI 使用。 |
| 命令行入口 | `AbilityKit.AI.Training.Runner --validate` | 在训练器或 CI 消费前拒绝不兼容数据。 |

公共契约只覆盖所有环境都必须一致的字段形状。观测向量长度、动作维度、奖励含义、实体编码顺序等环境语义仍由各环境的 `AiObservationSpec`、`AiActionSpec` 和 adapter 文档约束。

---

## 2. 兼容性规则

| 规则 | 要求 | 原因 |
| --- | --- | --- |
| 每行必须携带版本 | Writer 必须在每一行写入 `schemaVersion`。 | JSONL 可以被截取、合并或单行处理，不能依赖文件头。 |
| 版本先于载荷校验 | Reader 在消费字段前必须拒绝不支持的 `schemaVersion`。 | 避免旧消费端误读新版字段语义。 |
| 行类型必须封闭 | `type` 只能是 `run`、`episode`、`step`。 | 消费端可以明确选择处理范围，不会默默跳过未知语义。 |
| 同版本只允许兼容扩展 | Schema 1 可以增加旧 Reader 可忽略的可选字段。 | 支持渐进补充诊断字段，不破坏已有训练流水线。 |
| 必填字段语义不可漂移 | 同一 `schemaVersion` 内不得改变既有必填字段含义。 | 保证离线数据、回归数据和训练脚本可重复解释。 |
| 环境私有数据保持隔离 | 环境特有字段应作为可选字段或嵌套对象增加。 | 防止 Shooter 或 Moba 的实现细节污染公共行结构。 |

---

## 3. Report JSONL

Report JSONL 由 `AiTrainingReportWriter` 写入训练 runner 标准输出，用于汇总一次 run 以及每个 episode 的结果。它适合 CLI 观察、CI 摘要和训练前快速审计。

### 3.1 Run 行

`run` 行描述一次训练命令的整体结果。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `schemaVersion` | number | 当前行使用的数据契约版本，当前值为 `1`。 |
| `type` | string | 固定为 `run`。 |
| `episodes` | number | 本次请求执行的 episode 数。 |
| `totalSteps` | number | 所有 episode 实际执行的环境 step 总数。 |
| `totalReward` | number | 所有 episode reward 之和。 |
| `averageReward` | number | 单个 episode 的平均 reward。 |
| `averageSteps` | number | 单个 episode 的平均 step 数。 |
| `completedEpisodes` | number | 到达环境终止状态的 episode 数。 |
| `truncatedEpisodes` | number | 因达到最大 step 上限而截断的 episode 数。 |
| `seed` | number | 本次 run 的基础随机种子。 |
| `maxSteps` | number | 每个 episode 配置的最大 step 数。 |
| `fixedDeltaSeconds` | number | 每个逻辑 step 使用的固定模拟时间。 |

### 3.2 Episode 行

`episode` 行描述一次 episode 的最终结果。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `schemaVersion` | number | 当前行使用的数据契约版本，当前值为 `1`。 |
| `type` | string | 固定为 `episode`。 |
| `episodeIndex` | number | 当前 run 内从 `0` 开始的 episode 索引。 |
| `seed` | number | 当前 episode 实际使用的随机种子。 |
| `steps` | number | 当前 episode 实际执行的 step 数。 |
| `totalReward` | number | 当前 episode reward 总和。 |
| `done` | boolean | 环境是否到达终止状态。 |
| `truncated` | boolean | 是否因为最大 step 上限而被截断。 |
| `finalStateHash` | number | 最终逻辑状态的确定性 hash，用于回归校验。 |

---

## 4. Rollout JSONL

Rollout JSONL 由 `AiTrainingRolloutJsonLinesWriter` 写入文件，用于保存逐 step 轨迹。它面向离线检查、模仿学习数据、训练器输入和跨环境回归分析。

### 4.1 Step 行

`step` 行描述一次环境 step 之后的观测、动作和反馈。

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `schemaVersion` | number | 当前行使用的数据契约版本，当前值为 `1`。 |
| `type` | string | 固定为 `step`。 |
| `episodeIndex` | number | 当前 run 内从 `0` 开始的 episode 索引。 |
| `seed` | number | 当前 episode 实际使用的随机种子。 |
| `stepIndex` | number | 环境执行后从 `1` 开始的 step 索引。 |
| `observation` | number[] | 环境输出的扁平观测向量。 |
| `continuousAction` | number[] | 提交给环境的连续动作向量。 |
| `discreteAction` | number[] | 提交给环境的离散动作向量。 |
| `reward` | number | 当前 step 返回的 reward。 |
| `done` | boolean | 当前 step 后环境是否到达终止状态。 |
| `truncated` | boolean | 当前 episode 是否因为最大 step 上限而被截断。 |
| `stateHash` | number | 当前 step 后逻辑状态的确定性 hash。 |

---

## 5. 消费端校验

`AiTrainingJsonLinesReader` 是公共 C# 读取与校验入口。它只校验可跨环境复用的 JSONL 外壳和公共字段：

| 校验项 | 行为 |
| --- | --- |
| `schemaVersion` | 必须存在，并且必须等于当前支持版本。 |
| `type` | 必须是 `run`、`episode` 或 `step`。 |
| 必填字段 | 必须存在，并且 JSON primitive 类型必须符合契约。 |
| Step 向量字段 | `observation`、`continuousAction`、`discreteAction` 必须是 JSON array。 |
| 空行 | 直接忽略，便于人工编辑和文件拼接。 |
| 错误报告 | 遇到坏数据立即抛出 `FormatException`，消息包含 JSONL 源行号。 |

`AiTrainingJsonLinesValidator` 在 Reader 之上提供文件级摘要，返回总行数以及 run、episode、step 行数。训练 runner 通过同一条路径暴露命令行校验入口：

```powershell
dotnet run --project src/AbilityKit.AI.Training.Runner -- --validate artifacts/shooter-ai-rollout.jsonl
```

校验成功时输出一行机器可读摘要：

```text
valid=true totalRecords=3 runRecords=0 episodeRecords=0 stepRecords=3
```

校验失败时，runner 将契约或文件错误写入 stderr，并返回退出码 `3`；参数解析或用法错误返回 `2`。CI、训练器 bootstrap 脚本或数据集导入工具应区分“调用方式错误”和“输入数据无效”，不能只把所有非零退出码合并成同一种数据问题。

Reader 不校验环境私有语义，例如 observation 长度、action 维度、reward 合法范围或实体槽位含义。这些规则属于具体环境的 observation/action spec，不属于公共 JSONL 行契约。

Reader 当前也没有建立文件级关系约束。它不验证 run/episode/step 的顺序、单文件是否只包含一个 run、episode 与 step 的关联是否一致，也不拒绝未知字段；三个向量只检查“是数组”，不逐项确认元素类型、有限数值或跨行维度一致。Python `build-dataset` 会在后续阶段补一部分维度和数值读取约束，但不能把后置训练工具校验写成公共 C# JSONL validator 已覆盖。若数据将进入共享制品库，应在项目导入层补齐文件级身份、顺序、有限值和环境 spec 校验。

---

## 6. Schema 1 范围

Schema 1 是 Shooter 与 Moba headless AI 环境共享的第一版数据外壳。它刻意把 observation 和 action 保持为扁平向量，使同一条训练器流水线可以读取多个玩法示例的数据。

| 范围 | 处理方式 |
| --- | --- |
| 公共行结构 | 固定 `run`、`episode`、`step` 三类行。 |
| 版本字段 | 每行都写入 `schemaVersion = 1`。 |
| 环境观测 | 以 `number[]` 扁平向量保存，不在公共契约内解释槽位。 |
| 环境动作 | 连续动作与离散动作分开保存，不在公共契约内解释具体按键或技能。 |
| 回归稳定性 | 使用 `stateHash` / `finalStateHash` 辅助检测逻辑状态漂移。 |
| 后续扩展 | 通过可选字段或新版 `schemaVersion` 演进。 |

当后续需要引入序列观测、视觉观测、动作 mask、环境标签或数据集元信息时，应先判断旧消费端是否可以安全忽略。如果不能安全忽略，应提升 `schemaVersion`，并同步更新 Reader、Validator、CLI 校验和本文档。

---

## 7. 离线训练最小闭环

离线训练侧以 `tools/ai_training` 作为第一版工具目录，先把“可消费、可训练、可校验”的流水线固定下来，再逐步替换具体训练算法或模型格式。该目录不直接依赖 Unity、ML-Agents、PyTorch 或 ONNX，保证训练数据契约可以在最小环境下被验证。

| 阶段 | 入口 | 产物 | 说明 |
| --- | --- | --- | --- |
| 数据集构建 | `python -m tools.ai_training.cli build-dataset` | dataset JSON | 读取 rollout JSONL，只消费 `step` 行，校验 `schemaVersion`、向量字段和维度一致性。 |
| 行为克隆基线 | `python -m tools.ai_training.cli train-bc` | model JSON | 使用标准库实现的线性行为克隆 baseline，拟合连续动作并记录离散动作默认值。 |
| 产物清单 | `ModelArtifactMetadata` | metadata JSON | 固化环境名、模型类型、数据契约版本、输入输出维度、样本数、训练参数、指标和模型文件 hash。 |
| 结构校验 | `python -m tools.ai_training.cli validate-metadata --metadata` | 机器可读输出 | 只校验 metadata JSON 本身的必填字段、类型和基础格式。 |
| 完整产物校验 | `python -m tools.ai_training.cli validate-metadata --metadata --dataset --model` | 机器可读输出 | 同时校验 dataset、model 与 metadata 的环境、schema、维度、样本数和模型文件 hash。 |

标准命令形态如下：

```powershell
dotnet run --project src/AbilityKit.AI.Training.Runner -- --environment shooter --episodes 8 --max-steps 600 --rollout artifacts/shooter-rollout.jsonl
py -3 -m tools.ai_training.cli build-dataset --rollout artifacts/shooter-rollout.jsonl --environment shooter --output artifacts/shooter-dataset.json
py -3 -m tools.ai_training.cli train-bc --dataset artifacts/shooter-dataset.json --model artifacts/shooter-bc-model.json --metadata artifacts/shooter-bc-model.metadata.json
py -3 -m tools.ai_training.cli validate-metadata --metadata artifacts/shooter-bc-model.metadata.json
py -3 -m tools.ai_training.cli validate-metadata --metadata artifacts/shooter-bc-model.metadata.json --dataset artifacts/shooter-dataset.json --model artifacts/shooter-bc-model.json
```

完整产物校验是训练产物进入 C# 运行时前的推荐局部质量门。它必须拒绝以下不一致情况：

| 校验项 | 要求 |
| --- | --- |
| 产物类型 | metadata 的 `artifactType` 必须是 `abilitykit.ai.model-artifact.v1`。 |
| 模型类型 | metadata 与 model 的 `modelType` 必须一致，当前 baseline 为 `abilitykit.behavior_cloning.linear.v1`。 |
| 数据契约版本 | metadata 的 `dataSchemaVersion` 必须与 dataset 的 `schemaVersion` 一致。 |
| 环境名 | metadata 的 `environment` 必须与 dataset 的环境一致。 |
| 输入输出维度 | metadata、dataset、model 的 observation、continuous action、discrete action 长度必须一致。 |
| 样本数 | metadata、dataset、model 的样本数必须一致。 |
| 文件完整性 | metadata 的 `modelSha256` 必须等于 model 文件当前内容的 SHA-256。 |

当前 baseline 模型是训练链路的局部完整性校验入口，也是第一版可执行运行时模型格式，不是最终算法边界。后续引入神经网络、ONNX 导出或服务器侧真实模型执行器时，必须继续沿用 metadata 清单中的环境、数据契约版本、observation/action 维度和模型 hash 校验，不能绕过产物契约直接加载文件。

---

## 8. 运行时推理加载闭环

C# 运行时通过 `AbilityKit.AI.Inference` 包消费训练产物。当前第一版运行时闭环不引入 Python、PyTorch、ONNX Runtime 或 Unity 依赖，而是使用 `BehaviorCloningModelExecutor` 直接加载 `abilitykit.behavior_cloning.linear.v1` JSON 模型，并通过既有 `IAiModelExecutor` / `AiModelPolicy` 边界执行推理。

加载入口如下：

```csharp
using var executor = BehaviorCloningModelExecutor.LoadArtifact(metadataPath, modelPath, expectedEnvironment: "shooter");
var policy = new AiModelPolicy(executor);
var output = policy.Run(new AiModelInput(executor.Spec, observation));
```

`BehaviorCloningModelExecutor.LoadArtifact` 在创建执行器前执行与 Python 完整产物校验一致的运行时防线：

| 校验项 | 运行时行为 |
| --- | --- |
| metadata 路径与 model 路径 | 不能为空，并且文件必须能被解析为 JSON。 |
| artifact type | 非 `abilitykit.ai.model-artifact.v1` 时拒绝加载。 |
| model type | 非 `abilitykit.behavior_cloning.linear.v1` 时拒绝加载。 |
| schema version | `schemaVersion` 与 `dataSchemaVersion` 不是 `1` 时拒绝加载。 |
| 环境名 | 传入 `expectedEnvironment` 时，metadata 环境必须匹配。 |
| model hash | model 文件内容的 SHA-256 必须等于 metadata 的 `modelSha256`。 |
| 维度 | metadata 与 model 中的 observation、continuous action、discrete action 长度必须一致。 |
| 权重形状 | `continuousWeights`、`continuousBias`、`discreteDefaults` 必须与动作维度匹配。 |

运行时执行逻辑保持确定性：连续动作由线性权重和 bias 计算，离散动作使用训练阶段统计出的默认值。该实现用于打通“rollout -> dataset -> model -> metadata -> C# policy”的端到端闭环。后续如果新增 ONNX 或其他模型格式，应新增独立 `modelType` 和执行器实现，但仍复用 metadata 作为所有运行时加载路径的公共入口。

---

## 9. 当前证据、限制与晋升条件

| 能力 | 源码事实 | 证据等级上限 |
|---|---|---|
| JSONL schema 1 | run、episode、step writer/reader/validator 与 runner `--validate` 已实现 | E3：公共外壳和错误路径可由专项测试验证 |
| Dataset 构建 | Python CLI 提供 `build-dataset` | E3：局部工具契约，不等于训练平台 |
| 行为克隆训练 | Python CLI 提供 `train-bc`，输出线性模型和 metadata | E3：baseline 算法闭环，不代表模型质量满足项目目标 |
| Metadata 校验 | CLI 提供 `validate-metadata`，可联动 dataset/model/hash | E3：产物一致性，不证明场景效果 |
| C# 推理加载 | `BehaviorCloningModelExecutor` 校验环境、schema、hash 和维度 | E3：加载与确定性执行边界 |

2026-08-16 本地复核中，Python 离线训练测试 `6/6`、C# 推理测试 `7/7` 通过。两组结果仍是各自进程内的局部 E3：仓库没有提交双方共同消费的 canonical fixture，也没有自动串联完整的 C# rollout、Python 训练和 C# 加载。

`tools/test-gates.json` 与 `.github/workflows/abilitykit-test-gates.yml` 当前均没有 AI Training 或 AI Inference 专项 gate。因此不能把局部 Python/C# 测试写成 PR、nightly 或发布链已接线，也不能把“可训练、可加载”写成“模型效果达标”。环境私有 observation/action 语义、数据来源治理、训练可复现性、模型质量阈值、目标硬件性能、灰度和回滚仍由采用项目补齐。

要晋升到 E4，需要固定环境、数据集身份、随机种子、训练参数和场景指标，并证明生成产物在 C# runtime 中完成真实行为验收。要晋升到 E5，还需要把数据校验、训练或受控产物复用、运行时验收、artifact 保留和失败责任接入实际 workflow。

变更必须遵循以下规则：

1. 公共行字段发生不兼容变化时提升 `schemaVersion`，同时更新 writer、reader、validator、Python parser 和兼容测试。
2. 环境私有向量语义使用独立 spec/version，不向 schema 1 的通用数组偷偷加入隐式槽位变化。
3. 新模型格式使用新的 `modelType` 和执行器；metadata 的环境、维度、数据版本和 hash 校验不得绕过。
4. 文档中的“门禁”必须注明是本地校验、场景验收还是已接线 CI gate。

---

*文档版本：v3.1 | 最后更新：2026-08-16*
