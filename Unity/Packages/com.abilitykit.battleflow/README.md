# AbilityKit BattleFlow

项目无关的战斗流程**积木模型**：把 DSL 场景拆成可复用积木（原子 + 复合），自由组合后编译成玩法中立的中性场景 IR。

## 定位

BattleFlow 是**场景作者层的复用宏**，不是运行时控制结构。它给线性的 `TestScenario`（IR）加一层"粒度项目可选、可聚合可拆细"的作者层——测试和预览共用同一份编译结果。

## 职责红线（务必遵守）

**组合节点只做「宏」，不做「控制流」。**

- `BattleCompositeBlock` 只有 **Sequence** 语义：按序展开子积木，是纯分组/复用（宏），不涉及运行期判定。
- **不要**往 BattleFlow 加 Selector / Parallel / Loop / Condition 等控制流节点——那会让它退化成行为树的克隆 + 多一层封装。

| | 行为树（`com.abilitykit.behaviortree`） | BattleFlow（本包） |
| --- | --- | --- |
| 运行时机 | 运行期每帧 tick | 作者期一次编译 |
| 语义 | "此刻该干什么"（反应式，依赖世界状态） | "这一局是什么"（脚本式，固定序列） |
| 时间性 | 不确定（条件何时成立何时走） | 确定（每步固定 atMs） |
| 产出 | 行为（Success / Failure / Running） | 确定性 `TestScenario` |

两者树状外壳一样（叶子 + 组合），但语义与运行模型不同。**控制流 / 反应式行为（自适应对手、条件分支、循环）归行为树**，通过 DSL 的 `BehaviorProfileId` 挂到角色上。场景流程保持线性，测试才能确定性、可复现。

## 积木模型（三层，粒度项目可选）

| 类型 | 角色 |
| --- | --- |
| `BattleBlock` | 积木基类（Id / DisplayName / Description） |
| `BattleAtomicBlock` | 叶子，`Compile(builder)` 编译成一个 IR 构件 |
| `BattleCompositeBlock` | 容器（Sequence），`Children` 一串子积木，可嵌套 |

- **框架给原子积木**（`SetEnvironmentBlock` / `SpawnActorBlock` / `TimelineStepBlock`），映射到 IR 的最细粒度。
- **项目给复合积木**，把原子积木聚合成「标准野怪测试」等常用套路，注册进 `BattleBlockLibrary`。
- 编辑器调色板粗细同时出现：想细拖原子积木，想快拖复合积木——粒度是项目的选择，不是框架写死的。

## 编译到 IR（不造第三套）

积木树 → 一次 `Compile`，展平成 `TestScenario`（原子积木追加构件，复合积木递归展平）。测试与预览共用同一份编译结果。

```csharp
var standardJungleTest = new BattleCompositeBlock
{
    Id = "standard-jungle-test",
    Children = new BattleBlock[]
    {
        new SetEnvironmentBlock { ProfileId = "jungle-camp" },
        new SpawnActorBlock { Alias = "target", TeamId = 2 },
        new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster", TargetAlias = "target" },
    },
};

var scenario = BattleFlowCompiler.Compile("case-1", new BattleBlock[] { standardJungleTest });
// scenario 是 TestScenario：EnvironmentProfileId + Actors + Timeline 已填好
```

## 说明

- 纯 C#（`noEngineReferences`）、C# 9 兼容、无 Unity / 无实体系统依赖，可在 .NET 直接测试。
- 依赖 `com.abilitykit.scenario`（中性 IR）；`EnvironmentProfileId` 是**不透明字符串 id**，由项目侧的 environment catalog 解析。
- 本包只给**作者层机制**，不内置任何业务积木；MOBA/shooter 各自提供自己的复合积木与积木库。
