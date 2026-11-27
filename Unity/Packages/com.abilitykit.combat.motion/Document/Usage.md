# Motion 模块使用文档（com.abilitykit.combat.motion）

> 本文说明 `AbilityKit.Combat.MotionSystem` 的推荐接入方式、对象池用法、快照恢复和常见组合策略。
> 文档类型：使用指南；适用范围：0.1.0 当前 Runtime，依赖见 [package.json](../package.json)。
> 核校状态：2026-11-25 部分核校；装配顺序、时钟、PathFollower 恢复与事件签名，证据见第 11 节。其他片段未逐一运行。

## 1. 模块定位

`com.abilitykit.combat.motion` 提供战斗移动的组合层：

- `MotionPipeline`：统一调度多个 `IMotionSource`。
- `IMotionSource`：输入移动、技能位移、击退、路径跟随等移动来源。
- `IMotionSolver`：约束、碰撞、范围限制和最终位移求解。
- `IMotionEventSink`：接收命中、到达、过期等事件。
- `IMotionSnapshotSource`：导出/导入移动源内部进度，支持预测、回滚和重放。

适合 MOBA、ARPG、RTS、帧同步或服务端权威移动等场景。

## 2. 推荐的实体移动组件组织方式

每个可移动实体建议持有一个 `MotionPipeline`，并在实体销毁或退场时归还：

```csharp
using AbilityKit.Core.Mathematics;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Combat.MotionSystem.Collision;

public sealed class CombatMover
{
    private readonly int _entityId;
    private MotionPipeline _pipeline;
    private MotionState _state;
    private MotionOutput _output;

    public CombatMover(int entityId, Vec3 spawnPosition)
    {
        _entityId = entityId;
        _pipeline = MotionPipelinePool.Rent();
        _state = new MotionState(spawnPosition);
        _output = default;
    }

    public void Tick(float dt)
    {
        _pipeline.Tick(_entityId, ref _state, dt, ref _output);
    }

    public void Dispose()
    {
        MotionPipelinePool.Release(_pipeline);
        _pipeline = null;
    }
}
```

如果已有生命周期框架，也可以直接 `new MotionPipeline()`，但推荐使用 `MotionPipelinePool.Rent()` / `MotionPipelinePool.Release()` 降低实体频繁创建销毁时的 GC 压力。

## 3. 输入移动（Locomotion）

`LocomotionMotionSource` 用于玩家摇杆、键盘或 AI 输入移动。

```csharp
var locomotion = LocomotionMotionSource.Rent(
    speed: 5f,
    space: MotionInputSpace.World,
    priority: 0);

locomotion.SetInput(x: 1f, z: 0f);
pipeline.AddSource(locomotion);
```

不再使用时归还：

```csharp
pipeline.RemoveSource(locomotion);
LocomotionMotionSource.Release(locomotion);
```

输入空间：

- `MotionInputSpace.World`：输入直接解释为世界方向。
- `MotionInputSpace.Local`：输入根据 `MotionState.Forward` 转换为局部前后/左右移动。

## 4. 技能位移、击退和路径移动

### 4.1 轨迹位移

`TrajectoryMotionSource` 适合冲刺、跳跃、突进等按时间采样的位移。

```csharp
var trajectory = new LinearTrajectory3D(
    start: Vec3.Zero,
    end: new Vec3(4f, 0f, 0f),
    duration: 0.35f);

var dash = TrajectoryMotionSource.Rent(
    trajectory,
    priority: 20,
    groupId: MotionGroups.Ability,
    stacking: MotionStacking.ExclusiveHighestPriority);

pipeline.AddSource(dash);
```

`TrajectoryMotionSource` 会在轨迹结束后自动变为 inactive，`MotionPipeline` 后续 tick 会自动移除 inactive source，并向 `IMotionEventSink` 发出过期事件。

### 4.2 固定位移/击退

`FixedDeltaMotionSource` 适合击退、拉拽、短时间持续推力。

```csharp
var knockback = FixedDeltaMotionSource.Rent(
    deltaPerSecond: new Vec3(-6f, 0f, 0f),
    duration: 0.2f,
    priority: 30,
    groupId: MotionGroups.Ability,
    stacking: MotionStacking.Additive);

pipeline.AddSource(knockback);
```

### 4.3 路径跟随

`PathFollowerMotionSource` 会对传入的点数组做防御性拷贝，避免外部修改路径造成非确定性。

```csharp
var points = new[]
{
    new Vec3(0f, 0f, 0f),
    new Vec3(2f, 0f, 1f),
    new Vec3(4f, 0f, 1f),
};

var path = PathFollowerMotionSource.Rent(
    points,
    speed: 3f,
    arriveEpsilon: 0.05f,
    priority: 10,
    groupId: MotionGroups.Path,
    stacking: MotionStacking.ExclusiveHighestPriority);

pipeline.AddSource(path);
```

## 5. 跨组抑制策略

默认策略中，`MotionGroups.Control` 通常用于眩晕、强制位移、强控等控制类效果，并会抑制 `Locomotion`、`Ability`、`Path` 等组。

可通过 `MotionPipeline.Policy` 配置自定义抑制关系：

```csharp
pipeline.Policy = new MotionPipelinePolicy();
pipeline.Policy.SetSuppressedGroups(
    MotionGroups.Control,
    MotionGroups.Locomotion,
    MotionGroups.Ability,
    MotionGroups.Path);
```

需要由外部同步/恢复适配完整策略值时，可以单独捕获和静默恢复：

```csharp
MotionPipelinePolicyState saved = pipeline.Policy.CaptureState();
MotionPipelinePolicy.ValidateState(in saved);
pipeline.Policy.RestoreState(in saved);
```

状态包含全部 `SuppressorGroupId -> SuppressedGroupIds` 规则。捕获按 key 升序排列，数组彼此独立；恢复完整替换规则并复制输入数组。空 `Rules` 表示无规则；规则目标数组必须非空，key 必须唯一且升序。组编号及内层目标的顺序和重复值不做限制。非法状态会抛出包含字段或索引的 `ArgumentException`，且不改变当前策略。

该 API 只管理 Policy 的纯值映射，不保存 Policy 对象身份、别名关系、Pipeline 成员、来源状态、Solver、Events 或导航世界，也不触发通知或推进。它不依赖 FrameSync，也不单独构成完整世界回滚能力；宿主负责 Policy 存在性、共享关系和其他运行依赖。

常见约定：

- `Locomotion`：玩家/AI 常规移动，通常 `Additive`。
- `Ability`：技能位移，通常 `ExclusiveHighestPriority`。
- `Control`：强控或强制移动，通常 `OverrideLowerPriority`。
- `Path`：导航路径移动，通常 `ExclusiveHighestPriority`。

## 6. 碰撞与约束求解

`ConfigurableMotionSolver` 通过 `IMotionCollisionWorld` 接入项目自己的碰撞世界。

```csharp
pipeline.Solver = new ConfigurableMotionSolver(
    world,
    (int moverId, in MotionState state, in MotionOutput input, float dt) =>
        new MotionConstraints(
            new MotionCollisionConstraints(
                enable: true,
                allowPassThrough: false,
                endOverlapPolicy: MotionEndOverlapPolicy.ProjectToNearestFree,
                radius: 0.4f,
                skin: 0.02f,
                obstacleMask: 1,
                ignoreMask: 0),
            MotionLeashConstraints.Disabled));
```

`MotionEndOverlapPolicy`：

- `AllowInside`：允许结束点仍在重叠内。
- `ProjectToNearestFree`：尝试调用 `TryProjectToFree` 投影到最近可用位置。
- `ClampToLastValid`：保留 sweep 已求出的可用位移。
- `Reject`：重叠时拒绝本帧位移。

如果需要诊断异常或投影失败，可实现 `IMotionSolverDiagnostics` 并传入 solver。

## 7. 快照与回滚

支持快照的 source 实现 `IMotionSnapshotSource`：

```csharp
if (source is IMotionSnapshotSource snapshotSource &&
    snapshotSource.ExportSnapshot(out var snapshot))
{
    // 保存 snapshot 到预测/回滚状态
}
```

恢复时，先用相同配置创建或租借 source，再导入 snapshot：

```csharp
var restored = TrajectoryMotionSource.Rent(trajectory);
restored.ImportSnapshot(in snapshot);
pipeline.AddSource(restored);
```

注意：轨迹对象本身通常应由技能配置或回放上下文重新创建，`MotionSourceSnapshot` 只保存运行进度，不保存完整轨迹数据。

### 7.1 PathFollower 完整领域状态

`PathFollowerMotionSource` 的旧 `ExportSnapshot / ImportSnapshot` 仍依赖目标实例原路径，继续保留索引夹取、非正速度/阈值留旧值等兼容规则。需要独立保存完整路径及来源数据时，使用以下入口：

```csharp
PathFollowerState saved = path.CaptureState();
PathFollowerMotionSource.ValidateState(in saved);
path.RestoreState(in saved);
```

`PathFollowerState` 包含 `Points`、`Speed`、`ArriveEpsilon`、`GroupId`、`Priority`、`Stacking`、`Index` 和 `IsActive`。结构赋值本身不复制数组；Capture 返回独立路径数组，Restore 验证并复制输入后一次替换来源数据，准备失败保留原值。两个入口均不推进、寻路或发送到达通知；正常 Tick 不复制路径。

完整恢复要求非 null 路径及有限坐标、有限正速度和阈值、已定义的 Stacking，以及 `[0, Points.Length]` 内的索引；空路径必须非活动。允许中途取消和非空路径的 `IsActive == true && Index == Points.Length` 边界，恢复不将其提前规范化。GroupId/Priority 允许任意整数，`default(PathFollowerState)` 不是合法重置状态。Validate/Restore 对非法状态抛出指明字段的 `ArgumentException`；Capture 保留普通配置中的特殊值，不默默修正。

恢复不增删管线成员、不重新排序，也不恢复 Actor、导航输入或业务重规划计数。接入方须另外保存 MotionState 并管理身份、成员、顺序及依赖一致性；普通归池仍要求先解绑。来源和输入数组均不支持并发修改。此 API 不依赖帧同步包，不持有帧历史，不等于项目已支持完整世界回滚。

## 8. 对象池使用建议

当前模块推荐优先使用以下池化入口：

- `MotionPipelinePool.Rent()` / `MotionPipelinePool.Release()`
- `LocomotionMotionSource.Rent()` / `LocomotionMotionSource.Release()`
- `TrajectoryMotionSource.Rent()` / `TrajectoryMotionSource.Release()`
- `FixedDeltaMotionSource.Rent()` / `FixedDeltaMotionSource.Release()`
- `PathFollowerMotionSource.Rent()` / `PathFollowerMotionSource.Release()`
- `ScaledMotionSource.Rent()` / `ScaledMotionSource.Release()`

兼容历史代码的 public 构造函数仍保留，但在高频创建销毁路径上不推荐使用。

归还 source 前建议先从 pipeline 移除，避免仍被 tick：

```csharp
pipeline.RemoveSource(source);
TrajectoryMotionSource.Release(source);
```

## 9. 事件处理

实现 `IMotionEventSink` 可接收命中、到达、过期等事件：

```csharp
using AbilityKit.Combat.MotionSystem.Collision;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Combat.MotionSystem.Events;

public sealed class MotionEvents : IMotionEventSink
{
    public void OnHit(int moverId, in MotionState state, in MotionHit hit)
    {
        // 播放碰撞反馈或通知战斗逻辑
    }

    public void OnArrive(int moverId, in MotionState state)
    {
        // 路径到达
    }

    public void OnExpired(int moverId, in MotionState state)
    {
        // 技能位移结束、击退结束等
    }
}
```

## 10. 常见问题

### Q1：为什么 source 结束后没有立即手动释放？

`MotionPipeline` 只负责从内部列表移除 inactive source，不会假设 source 的所有权。业务侧如果使用池化 source，应在确认不再引用后调用对应 `Release()`。

### Q2：如何避免不同平台结果不一致？

- 输入数组会做防御性拷贝，避免外部修改。
- 同一组使用明确的 priority 决定胜出 source。
- 旧 `MotionSourceSnapshot` 依赖同一来源配置；`PathFollowerState` 完整值可独立保存路径和来源数据。两者都不恢复世界。
- 尽量避免在 solver 中读取非确定性状态。

### Q3：是否完全没有分配？

模块已将 pipeline、常用 source 和内部临时集合接入 core 对象池。路径点数组与轨迹累计长度数组仍可能因防御性拷贝产生分配；如需要严格零 GC，可继续接入数组池或预构建不可变轨迹资源。

## 11. 装配、固定步长与局部恢复

1. 宿主为实体持有管线、已采用的姿态及 `MotionState`，先配置所需 `Policy`、`Solver` 和 `Events`，再绑定移动来源。默认 `NoMotionSolver` 不做碰撞解算，`Policy` 不会自动装上默认跨组抑制规则。
2. 每个逻辑 Tick 先更新输入、路径意图，再由唯一驱动者调用一次 `Tick`；随后采用结果并按需同步碰撞体。`Tick` 使用调用方传入的秒数，不读取帧号，也不会补跑。`FixedStepRunner` 只是在没有世界时钟的宿主中把正 `dt` 累积换算为步数；`ConsumeOneStep()` 只返回步长，不能再叠加到已有世界 Tick 上。
3. 完成通知不是碰撞或位移已提交的回执；到达/过期可能发生在 solver 和姿态写回之前。碰撞通知在状态写回之后。不要在同步回调中重入修改来源列表；将业务变更排队至安全点。
4. 恢复先由宿主确定作用域与稳定身份，预检并恢复姿态、`MotionState`、来源值、既有成员顺序及 Policy；Solver、事件接收者和导航数据由拥有者重绑。引用次序不是持久化数据；`RestoreSourceOrder` 只接收当前成员的完整排列。退场先解绑再归还池化对象，管线不会归还来源。

### 完整示例：路径跟随与同实例恢复

以下 Console 示例不装配碰撞，恢复同一管线中的来源和实体运动值，再用相同步长重演。没有重新查询路径，也没有恢复时钟或输入历史。

```csharp
using System;
using AbilityKit.Core.Mathematics;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Combat.MotionSystem.Generic;

using var pipeline = new MotionPipeline();
var follower = new PathFollowerMotionSource(new[] { new Vec3(3f, 0f, 0f) }, speed: 2f);
pipeline.AddSource(follower);
var state = new MotionState(Vec3.Zero);
MotionOutput output = default;
const float step = 0.5f;
pipeline.Tick(1, ref state, step, ref output);
var savedMotion = state;
var savedPath = follower.CaptureState();
pipeline.Tick(1, ref state, step, ref output);
if (Math.Abs(state.Position.X - 2f) > 0.0001f)
    throw new Exception("固定步长推进不符合预期");
var expected = state;

follower.RestoreState(in savedPath);
state = savedMotion;
pipeline.Tick(1, ref state, step, ref output);
if (state.Position.X != expected.Position.X || state.Time != expected.Time)
    throw new Exception("局部恢复后的重演结果不同");
pipeline.RemoveSource(follower);
Console.WriteLine("Motion: fixed step and local replay OK");
```

预期输出末行。2026-11-25 的本地 .NET Release suite 与该示例只验证包级契约，不覆盖 Unity、完整战斗、跨平台确定性、帧同步检查点或状态同步权威快照。源码见 [MotionPipeline](../Runtime/MotionSystem/Core/MotionPipeline.cs)、[FixedStepRunner](../Runtime/MotionSystem/Core/FixedStepRunner.cs) 和 [PathFollowerMotionSource](../Runtime/MotionSystem/Generic/PathFollowerMotionSource.cs)。
