# HFSM 运行时使用契约

> 文档类型：包使用契约
> 核校状态：2026-11-25 对照 `StateMachineRuntime`、Definition、RuntimeBindings、快照接口和两个随包 Samples；.NET HFSM 56 项通过，未执行 Unity Editor/Player。

从[包入口](../README.md)导入 HFSM Runtime Sample。当前主线读取稳定 Definition，绑定运行时代码，再使用逻辑帧和固定时间推进；编辑器 GraphAsset 是编辑源，不是运行时实例。

## 装配与推进

`DefinitionJson.Load` 解析定义，`DefinitionValidator.ValidateOrThrow` 检查结构；构造 `StateMachineRuntime<TOwner>` 时传入 owner、definition 和 `RuntimeBindings<TOwner>`。Bindings 用稳定键注册状态、条件及动作工厂，运行时在构造阶段校验并编译定义。详见[真实样例](../Samples~/HfsmRuntimeSample/Runtime/HfsmRuntimeSample.cs)：它展示状态、条件、转移动作绑定、初始化、触发和监视注册。

调用方持有 runtime 和 owner，并负责单一逻辑时钟：

1. 建立并验证 Definition 和对应 Bindings。
2. 创建 runtime，首次调用 `Initialize(frame, time)` 建立初始活动路径。
3. 按非递减时间和严格递增 frame 调用 `Tick(frame, time)`；条件变化后需要同帧重评时调用 `ReevaluateCurrentFrame()`。
4. 用 `Trigger(triggerId)` 派发显式触发；返回 true 表示已接受，包括进入 pending 的转移。
5. 停止前调用 `Shutdown()`；调用方管理 runtime 及 owner 的外部资源。

`Fixed64` 时间必须来自宿主逻辑时钟。运行时拒绝负 frame/time、倒退时间及重复或倒退 Tick frame；它不会读取 Unity wall clock 或自行补帧。

## 快照与失败

`CaptureSnapshot()` 捕获定义 hash、时钟、层级机结构状态，以及实现 `IStateSnapshotParticipant` 的状态 payload。它不自动快照 owner、外部服务或未实现该接口的状态对象。

`RestoreSnapshot()` 先校验结构与 participant payload，再写 participant 和运行时字段；它不重放 Enter/Exit。若 participant 写入抛错，runtime 会进入 Faulted，已经发生的 participant 写入不会事务回滚。运行时观察器的诊断通知异常会被吞掉，不影响模拟；状态、条件、动作等运行回调异常会使 Initialize/Tick/Shutdown 失败并将 runtime 置为 Faulted，调用者应停止正常推进并按应用策略处理故障。

Definition 在构造时被复制并编译。修改原始 authoring 对象不会改变已有 runtime；变更定义需创建新实例。多个 runtime 不应共享带有跨运行可变状态的绑定对象，状态和动作使用工厂实例化以隔离运行状态。

## 源码和验证范围

- Definition、Bindings、快照参与接口位于 `Runtime/HFSM/Core/`。
- [Runtime Sample](../Samples~/HfsmRuntimeSample/README.md) 展示导入、定义文件替换、触发与 Monitor。
- [Complete Showcase](../Samples~/CompleteHfsmShowcase/README.md) 展示编辑器资产导出和项目侧复杂动作策略。

本轮通过的 HFSM 56 项是现有 .NET 镜像的选定测试，不覆盖 Unity 图形界面、场景导入、Mono/IL2CPP 或项目自身时钟与快照集成。
