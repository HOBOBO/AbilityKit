# AbilityKit 触发器复杂技能分层设计

## 结论

当前 `com.abilitykit.triggering` 的整体方向已经可以适配大部分复杂技能需求。它不只是一个简单的“事件-条件-行为”系统，而是已经具备以下几类能力：

- 事件触发：用于接收战斗事件、技能事件、命中事件等瞬时输入。
- 条件判断：用于表达是否可触发、是否中断、是否继续执行。
- 参数引用：通过 value ref 从常量、payload、blackboard、var、expression 等来源取值。
- 行为执行：用于执行一次性 action、组合 action、模板化 action。
- 调度行为：用于延迟、周期、持续一段时间的行为执行。
- 持续对象：用于承载带生命周期、内部状态、tick 更新的运行时对象。

因此，复杂技能不应该全部塞进一次性的事件-条件-行为链路里，而应该采用“规则触发 + 调度生命周期 + 持续运行单元”的分层模型。

## 核心设计原则

### 1. 事件-条件-行为负责边界，不负责长期模拟

事件-条件-行为适合处理：

- 技能释放时是否通过条件。
- 命中时是否触发后续效果。
- buff 添加、刷新、移除。
- 创建召唤物、创建区域、发射子弹。
- 中断、完成、销毁等生命周期边界。

它不适合直接承载：

- 每帧跟随。
- 多对象空间布局。
- 持续轨道运动。
- 长时间存在物的内部状态推进。
- 高频 tick 下的复杂模拟。

这些长期模拟应该放入持续对象、runtime controller、领域 service 或 process unit。

### 2. value ref 是模板参数主线

模板参数不应该默认写死为“先写入黑板，再由行为读取”。更正式的方式是：模板声明 value ref，运行时由统一 resolver 解引用。

推荐来源：

- `Const`：固定配置值，例如基础伤害、基础半径。
- `PayloadField`：单次事件输入，例如本次伤害量、命中目标、技能等级。
- `Blackboard`：实体或战斗中的持久变量，例如攻击力、护盾、连击计数。
- `Var`：领域变量，例如 actor.hp、skill.level、area.count。
- `Expr`：组合计算，例如 `atk * scale + bonus`。

黑板是 value ref 的一个数据源，不是模板参数系统本身。

### 3. schedule 负责时间，continuous 负责生命周期对象

调度层和持续对象层需要分工清楚：

- `ISchedulableBehavior`：适合触发器行为本身的延迟、周期、持续执行。
- 独立业务定时由宿主管理，到期可发布即时事件；扁平计划动作和执行树时间节点分别由各自的调度入口承载。早期通用调度及其 Behavior 适配已退役，不再作为接入方案。
- `IContinuous` / `ProcessUnit`：适合长期存在、拥有内部状态、可暂停/恢复/中断的运行时对象。

简单说：

- 瞬时效果用 action/executable。
- 延迟、周期行为用 schedulable behavior。
- 长期存在的玩法对象用 continuous/process unit。

## 分层架构

### Template / Trigger 层

职责：声明技能规则，而不是实现所有运行时细节。

包含：

- 事件类型。
- 触发条件。
- action 列表。
- 参数 value ref。
- 生命周期 cue。
- 调度方式。
- 持续对象类型和配置。

例如：

- 当释放技能时，创建 3 个召唤物。
- 召唤物数量来自技能等级。
- 轨道半径来自配置。
- 持续时间来自 buff 或模板。
- owner 死亡时结束。

### Resolver / Context 层

职责：把模板中的引用解析成运行时数值或对象。

包含：

- payload 字段读取。
- blackboard 读取。
- var domain 解析。
- expression 计算。
- context source 解析。
- action 参数绑定。

行为层不应该关心数值来源，只消费解析后的参数。

### Action / Executable 层

职责：执行一次性决策或创建运行时对象。

适合：

- 创建 projectile。
- 创建 area。
- 添加 buff。
- 创建 summon group。
- 启动 continuous/process unit。
- 写入表现 cue。
- 触发后续 trigger。

对于复杂持续技能，action 的职责通常是“启动一个运行时实例”，而不是自己每帧模拟。

### Schedule 层

职责：管理时间维度。

适合：

- 延迟触发。
- 周期 tick。
- 持续时间结束。
- tick 间隔控制。
- 暂停、恢复、中断的调度传播。

schedule 不应该包含大量业务空间逻辑，它只决定什么时候执行。

### Continuous / Runtime Controller 层

职责：管理长期存在的玩法对象。

适合：

- 召唤物跟随。
- 召唤物轨道旋转。
- 持续区域。
- 持续引导技能。
- 链接类效果。
- 环绕护盾。
- 多段持续蓄力。

该层应该拥有实例状态，例如：

- owner id。
- summon ids。
- elapsed time。
- angle offset。
- radius。
- angular speed。
- duration。
- pause state。
- termination reason。

### Domain System 层

职责：承接领域级模拟和批量更新。

当某类玩法对象数量较多、逻辑较重、需要统一索引或同步时，应从单个 process unit 提升为领域 service/system。

例如：

- projectile system。
- area system。
- summon orbit system。
- aura system。
- link beam system。

### Output / Snapshot / Cue 层

职责：把逻辑层结果输出给表现层。

规则：

- 逻辑层不直接调用 View。
- 表现事件通过 output/snapshot/cue 输出。
- 客户端接收快照后处理表现。
- 持续对象的位置、创建、销毁、cue 都应走统一同步出口。

## 示例：召唤物围绕 owner 转圈

需求：释放技能后生成多个召唤物，召唤物围绕 owner 按固定角度间隔旋转，并跟随 owner 移动。

推荐拆分：

### 触发入口

事件：`skill.cast.completed`

条件：

- owner 存活。
- 技能资源足够。
- 当前召唤物数量未超过上限。

行为：

- 解析 summonCount、radius、angularSpeed、duration。
- 创建 summon orbit runtime instance。
- 输出 summon spawn cue 或 snapshot。

### 参数来源

- summonCount：`PayloadField(skillLevel)` 或 `Expr(baseCount + skillLevel)`。
- radius：`Const` 或 `Blackboard(owner.orbitRadiusBonus)`。
- angularSpeed：`Const`。
- duration：`Const` 或 buff duration。
- ownerActorId：payload/context。

### 持续对象

创建 `SummonOrbitProcessUnit` 或 `SummonOrbitController`。

实例状态：

- ownerActorId。
- summonActorIds。
- radius。
- angularSpeed。
- baseAngle。
- elapsedSeconds。
- durationSeconds。

每帧 tick：

1. 查询 owner 位置。
2. 计算每个 summon 的角度：`baseAngle + elapsed * angularSpeed + index * 360 / count`。
3. 计算目标位置。
4. 更新 summon transform 或写入移动意图。
5. owner 无效、duration 到期、被中断时结束。

### 生命周期边界

仍然走 trigger/action：

- 创建时触发 `summon.orbit.started`。
- 每次命中触发 `summon.hit`。
- owner 死亡触发 `summon.orbit.interrupted`。
- 持续时间结束触发 `summon.orbit.completed`。
- 召唤物销毁触发清理 action。

这样既保留了条件-行为模式，也不会把每帧空间模拟塞进模板 action。

## 适用场景映射

| 技能类型 | 推荐承载方式 |
| --- | --- |
| 一次性伤害 | action / executable |
| 命中后追加效果 | event + condition + action |
| 延迟爆炸 | schedulable behavior / schedule effect |
| 周期伤害 | schedulable behavior / schedule effect |
| buff 持续效果 | continuous / buff domain system |
| projectile 飞行 | projectile domain system |
| AOE 区域 | area domain system / continuous |
| 召唤物跟随 | process unit / summon domain system |
| 召唤物轨道 | process unit / summon orbit system |
| 引导技能 | continuous / schedulable behavior |
| 光环效果 | aura domain system / continuous |
| 链接类技能 | link domain system / continuous |

## 后续实现约束

1. 不要把所有参数都写入黑板。黑板只承载持久状态和共享变量。
2. 模板参数统一用 value ref 表达，行为层通过 resolver 获取结果。
3. 事件 payload 用于单次触发上下文，不要先转存黑板再读取。
4. 持续模拟使用 continuous/process unit 或领域 system。
5. schedule 只管理时间和触发节奏，不承载复杂业务状态。
6. 长期存在对象需要明确生命周期：start、tick、pause、resume、interrupt、complete、dispose。
7. 表现输出走 snapshot/cue，不从逻辑层直接调用 View。
8. 当某类持续对象数量变多，应沉淀成领域 system，而不是创建大量分散逻辑。

## 对当前框架的判断

当前触发器框架已经具备复杂技能系统的关键骨架：

- value ref 解决模板参数来源问题。
- action/executable 解决瞬时行为问题。
- schedulable behavior 解决延迟和周期行为问题。
- schedule effect 解决统一时间调度问题。
- continuous/process unit 解决长期运行对象问题。
- cue/snapshot 可以继续承接表现输出边界。

后续主要工作不是推翻架构，而是收敛主路径、减少重复抽象、补充领域级 runtime service 示例，并把复杂技能统一按上述分层落地。
