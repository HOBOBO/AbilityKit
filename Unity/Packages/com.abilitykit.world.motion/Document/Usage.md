# Motion 模块使用文档（com.abilitykit.world.motion）

本文档给出在“游戏/World 逻辑层”中接入 `MotionSystem` 的推荐方式。MotionSystem 是纯逻辑计算模块，不直接依赖 Unity/Entitas，因此接入时需要你在上层维护实体状态与生命周期。

## 1. 最小接入：单实体 Locomotion 移动

### 1.1 准备 Pipeline / State

对每个可移动实体，你通常需要维护：

- 一个 `MotionPipeline`
- 一个 `MotionState`
- 一组 `IMotionSource`（例如 locomotion、技能位移、控制、路径）

推荐做法：

- **每个实体一个 pipeline**（最直观，便于回滚序列化与调试）
- `MotionState` 由上层从 ECS 读初始位置/朝向初始化，并在每 tick 写回

### 1.2 设置 Solver / Policy / Events

- `pipeline.Solver`：如果你不需要碰撞，使用默认 `NoMotionSolver.Instance` 即可。
- `pipeline.Policy`：建议设置为 `MotionPipelinePolicy.CreateDefault()`（Control 抑制其它组）。
- `pipeline.Events`：可选，用于接收命中/完成事件。

### 1.3 添加输入移动 Source

使用内置的 `LocomotionMotionSource`：

- 支持 `MotionInputSpace.Local/World`
- `SetInput(x,z)` 设置输入向量（例如 WASD）
- `Speed` 控制速度

逻辑建议：

- 每 tick 在读取输入后更新 locomotion source 的 input
- 然后调用 pipeline tick 得到 applied delta

## 2. 推荐的“实体运动组件”组织方式

虽然 MotionSystem 不依赖 Entitas，但在 Entitas 项目中常见的组织方式是：

- ECS 中存：
  - `Position`、`Forward`、`Velocity`（可选）
  - 当前运动模式/标志（例如是否可移动、是否处于控制状态）
  - 运动相关的可回滚参数（例如正在执行的 dash 剩余时间）

- Service/Runtime 对象中存：
  - `MotionPipeline` 实例
  - 各类 `IMotionSource` 实例（locomotion / path / ability / control）

关键建议：

- 如果你要支持回滚/重演：
  - 需要能在回滚时恢复“source 的状态”。
  - 最稳妥的做法是：把 source 的必要状态做成可序列化数据（放 ECS 或快照 payload），回滚后重建 source 或恢复其字段。

## 3. Fixed-step 驱动（强烈建议）

为了与帧同步/回放/回滚兼容，建议使用固定步长（fixed dt）：

- `dt = 1f / tickRate`（例如 1/30、1/60）
- 每个逻辑 tick 都以固定 dt 调用 `MotionPipeline.Tick`

注意：

- 不要用 `Time.deltaTime` 直接驱动核心逻辑；应由上层将渲染帧累积成逻辑 tick。

## 4. 与技能位移/控制/路径的组合

MotionSystem 的核心优势是“多 source 组合”。建议按语义拆分 source：

- **Locomotion**（输入移动）：`MotionGroups.Locomotion` + `Additive`
- **Ability**（技能位移）：`MotionGroups.Ability`，按需求选 stacking
- **Control**（控制效果，如击飞/眩晕/拉拽）：`MotionGroups.Control` + `OverrideLowerPriority`
- **Path**（寻路/路径跟随）：`MotionGroups.Path`

### 4.1 同组叠加策略建议

- 需要多效果累加（例如多个持续推力） -> `Additive`
- 同一组只允许一个生效（例如多个 dash 同时只能一个） -> `ExclusiveHighestPriority`
- 需要“强制覆盖并抑制其它组”（例如眩晕/击飞期间禁止输入与技能位移） -> `OverrideLowerPriority` + 配置 `MotionPipelinePolicy`

### 4.2 跨组抑制（Control 抑制 Locomotion/Ability/Path）

- 设置：`pipeline.Policy = MotionPipelinePolicy.CreateDefault()`
- 触发抑制的条件：
  - Control 组的最佳 source stacking = `OverrideLowerPriority`

## 5. 实现碰撞/阻挡：IMotionSolver

当你需要“撞墙停止/滑墙/可穿透”等行为时，实现 `IMotionSolver`：

- 输入：`id`、`state`、`MotionOutput`（包含 desired delta）、`dt`
- 输出：`MotionSolveResult`（包含 applied delta 与 hit 信息）

典型策略：

- **停止**：检测到碰撞则 applied delta = 0
- **滑墙**：将 desired delta 在法线方向投影去掉，保留切向分量
- **反弹**：按法线反射速度/位移

注意：

- Solver 必须是纯逻辑（不要依赖 Unity Physics）
- 为回滚/重演友好：
  - 使用确定性的数据结构
  - 不读取真实时间
  - 若依赖地图数据，应来自确定性配置/网格

## 6. 事件（IMotionEventSink）使用建议

`MotionPipeline` 可触发：

- `OnHit`：碰撞命中
- `OnArrive`：source 完成（到达终点等）
- `OnExpired`：source 完成（超时/效果结束）

建议：

- 事件用于驱动“逻辑后续”（例如技能位移结束切换状态）
- 表现（VFX/SFX/动画）建议在 View 层订阅逻辑事件再执行

## 7. 回滚/预测场景下的集成方式（建议方案）

如果你的项目使用帧同步/预测回滚：

- MotionSystem 作为 world 内的纯逻辑子模块
- 上层回滚模块负责：
  - 在每帧保存必要状态（`MotionState` + sources 的必要参数）
  - 回滚时恢复状态并重演

推荐的保存粒度：

- 至少保存：`MotionState.Position/Forward/Velocity/Time`
- 需要保存 source 状态的场景：
  - 轨迹/路径执行到一半（需要保存进度）
  - dash/击飞持续中（需要保存剩余时间/当前速度）

实现方式可选：

- **A：快照保存 source 状态数据，回滚后重建 source**（推荐，简单且明确）
- **B：source 对象自己支持 Export/Import**（更高效，但要注意对象生命周期与内存）

## 8. 常见问题（FAQ）

### Q1：为什么 `MotionPipeline` 会“自己删 source”？

Pipeline 会在 tick 的清理阶段移除 `null` 或 `!IsActive` 的 source，避免外部忘记移除导致列表膨胀。

### Q2：如何做“立即取消技能位移”？

- 调用 해당 source 的 `Cancel()`
- 并确保 `IsActive` 在下一 tick 变为 false（或外部直接 `RemoveSource`）

### Q3：如何保证不同平台结果一致（确定性）？

- 固定步长驱动
- 避免使用非确定随机/真实时间
- 避免依赖容器遍历顺序（Dictionary/HashSet）影响逻辑分支

