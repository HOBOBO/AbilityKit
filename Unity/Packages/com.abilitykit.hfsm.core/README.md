# AbilityKit HFSM Core

> 文档类型：包入口与使用导航
> 核校状态：2026-11-25 运行时 .NET 回归 56 项通过；Unity 2022.3.62f1 中由 Flow 间接解析本地候选归档并执行 Editor 探针。未验证独立 Player 或正式注册表。

`com.abilitykit.hfsm.core` 提供不依赖 Unity 引擎的分层状态机定义、校验、运行时绑定、确定性驱动和快照接口。使用时引用程序集 `AbilityKit.HFSM.Core`；版本及直接依赖见 [package.json](package.json)。Flow 只依赖该运行时包，不会由此安装 HFSM 的 Unity 适配、图编辑器或示例。

新接入从 Definition 与 `RuntimeBindings<TOwner>` 构造 `StateMachineRuntime<TOwner>`。宿主管理 owner 和逻辑时钟：调用 `Initialize(frame, time)`，用严格递增的帧和非递减的固定时间调用 `Tick(frame, time)`，结束时调用 `Shutdown()`。快照不会自动保存 owner 或外部服务，也不提供对外部副作用的事务回滚。

定义、绑定、运行时及兼容 API 的唯一实现位于 `Runtime/Core/`，同一份源码供 Unity 和 .NET 使用。现有程序集和命名空间不变。需要 Unity 组件、图编辑或随包示例时，安装 `com.abilitykit.hfsm`，它会自动依赖本包。
