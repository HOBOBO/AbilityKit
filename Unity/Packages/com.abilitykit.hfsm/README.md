# AbilityKit HFSM

> 文档类型：包入口与使用导航
> 核校状态：2026-11-25 对照 `StateMachineRuntime`、Definition/Bindings 和随包样例；HFSM 确定性运行时测试 56 项通过。未验证 Unity Editor 场景和图编辑器交互。

`com.abilitykit.hfsm` 提供分层状态机定义、运行时编译和确定性驱动。当前主要模型是 **Definition → RuntimeBindings → StateMachineRuntime**；旧 `StateMachine/StateBase/Transition` 页面保留为兼容 API 材料，不作为新接入示例。

## 安装与首次运行

包版本、Unity 最低版本及直接依赖以 [`package.json`](package.json) 为准。Package Manager 提供两个 Samples：**HFSM Runtime Sample** 用于最小加载与触发；**Complete HFSM Showcase** 展示图编辑导出、复合动作、快照恢复和 Runtime Monitor。先从[Runtime Sample 步骤](Samples~/HfsmRuntimeSample/README.md)导入并运行。

运行时由调用方创建 owner、Definition 和 `RuntimeBindings<TOwner>`，再创建 `StateMachineRuntime<TOwner>`。调用 `Initialize(frame, time)` 后以严格递增的逻辑帧调用 `Tick(frame, time)`；每次运行结束调用 `Shutdown()`。本包不选择游戏时钟、不推进 Unity Update，也不拥有 owner。

## 文档

- [包内使用契约](Documentation~/Usage.md)：运行时装配、确定性时钟、触发、快照与故障边界。
- [包内文档索引](Documentation~/README.md)：Samples、旧入口和源码入口。
- [历史 API 材料](Document/README.md)：兼容模型与旧式示意，需按文首范围理解。
