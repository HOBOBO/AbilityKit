# 5.6 HFSM 分层状态机

> 本文基于 `Unity/Packages/com.abilitykit.hfsm` 源码说明 AbilityKit 的 HFSM 能力。HFSM 来自 UnityHFSM 风格的分层有限状态机，并在包内扩展了 Unity Graph Asset、编辑器、导出器、运行时可视化和 ActionBehavior 体系。Core HFSM 已进入 Shooter Bot AI 与 MOBA View Flow；Graph 到 runtime 的自动构建链路仍不完整，不能把两者视为相同成熟度。

---

## 目录

- [5.6 HFSM 分层状态机](#56-hfsm-分层状态机)
  - [目录](#目录)
  - [1. 能力定位](#1-能力定位)
  - [2. 源码入口](#2-源码入口)
  - [3. 运行时核心结构](#3-运行时核心结构)
  - [4. 状态生命周期与退出时间](#4-状态生命周期与退出时间)
  - [5. 转移模型](#5-转移模型)
  - [6. 分层状态机](#6-分层状态机)
  - [7. Unity Graph Asset 与编辑器链路](#7-unity-graph-asset-与编辑器链路)
  - [8. 导出与描述器边界](#8-导出与描述器边界)
  - [9. 核心执行、初始化与失败边界](#9-核心执行初始化与失败边界)
    - [9.1 初始化与状态切换](#91-初始化与状态切换)
    - [9.2 回调与检查接口](#92-回调与检查接口)
    - [9.3 结构恢复不是生命周期回放](#93-结构恢复不是生命周期回放)
  - [10. 生产接入与成熟度证据](#10-生产接入与成熟度证据)
  - [11. 和 Flow、Pipeline、Service 状态的边界](#11-和-flowpipelineservice-状态的边界)
  - [12. 扩展边界](#12-扩展边界)
  - [13. 和其他文档的关系](#13-和其他文档的关系)

---

## 1. 能力定位

HFSM 解决的是“对象或系统在有限状态之间切换，且状态可能嵌套、转移需要条件和退出时间”的问题。它和普通 enum switch 的差异在于：

| 需求 | HFSM 提供的能力 |
|------|-----------------|
| 状态有进入、逻辑、退出回调 | `State<TStateId,TEvent>` 的 `OnEnter`、`OnLogic`、`OnExit` |
| 转移有条件 | `Transition<TStateId>.ShouldTransition()` |
| 状态不能立即退出 | `needsExitTime`、`canExit`、pending transition |
| 任意状态都可能跳转 | transitions from any |
| 事件触发转移 | trigger transitions by event |
| 状态机可以嵌套 | `StateMachine<TOwnId,TStateId,TEvent>` 继承 `StateBase<TOwnId>` |
| 需要工具可视化 | `HfsmGraphAsset`、editor window、runtime monitor、exporter |

HFSM 的设计重点是“状态归属清晰、转移可解释、退出时间可控”。如果只是顺序执行几个步骤，Flow 更轻；如果是技能阶段和上下文推进，Pipeline 更贴近业务；如果是对象行为模式切换、AI 状态、表现状态或房间阶段，HFSM 更直接。

| 层级 | 应负责 | 不应由该层统一规定 |
|------|--------|--------------------|
| HFSM 包 | 状态/转移生命周期、分层、退出时间、检查接口和 Graph 工具基础 | 某款游戏的角色状态全集、动画参数和 AI 决策策略 |
| Host / World 接入 | 创建、Init/Logic/Dispose 时机、时间源和诊断采集 | 默认替业务处理回调异常或配置修复 |
| 项目应用层 | 状态图、transition 条件、优先级、业务副作用和失败策略 | 把一次性顺序流程强行建模为全局状态机 |
| MOBA / Shooter 示例 | 证明 Core 可用于 View Flow 与 Bot AI，并展示 profile 组装 | 代表通用 Graph-to-runtime 闭环已经成熟 |

框架提供稳定状态机语义，项目保留状态目录和行为编排所有权。这正是战斗工具集比固定应用层更合适的抽象粒度。

---

## 2. 源码入口

| 类型 | 源码 | 职责 |
|------|------|------|
| `StateMachine<TOwnId,TStateId,TEvent>` | [StateMachine.cs](../../../Unity/Packages/com.abilitykit.hfsm/Runtime/HFSM/Core/StateMachine/StateMachine.cs) | 分层状态机核心，管理状态、转移、pending transition、active state |
| `State<TStateId,TEvent>` | [State.cs](../../../Unity/Packages/com.abilitykit.hfsm/Runtime/HFSM/Core/States/State.cs) | 普通状态实现，封装 enter/logic/exit/canExit/timer |
| `Transition<TStateId>` | [Transition.cs](../../../Unity/Packages/com.abilitykit.hfsm/Runtime/HFSM/Core/Transitions/Transition.cs) | 条件转移，支持 before/after transition 回调 |
| `TransitionBase<TStateId>` | [TransitionBase.cs](../../../Unity/Packages/com.abilitykit.hfsm/Runtime/HFSM/Core/Base/TransitionBase.cs) | 转移基类，保存 from/to/forceInstantly 等基础信息 |
| `HfsmGraphAsset` | [HfsmGraphAsset.cs](../../../Unity/Packages/com.abilitykit.hfsm/Runtime/HFSM/Unity/Graph/HfsmGraphAsset.cs) | Unity ScriptableObject 图资产，保存节点、边、参数、编辑器数据 |
| `JsonGraphExporter` | [JsonGraphExporter.cs](../../../Unity/Packages/com.abilitykit.hfsm/Editor/Export/JsonGraphExporter.cs) | 将 graph descriptor 导出为 JSON，用于运行时加载和调试 |
| `HfsmEditorWindow` | [HfsmEditorWindow.cs](../../../Unity/Packages/com.abilitykit.hfsm/Editor/HfsmEditorWindow.cs) | Unity 编辑器窗口入口 |
| `HfsmRuntimeMonitorWindow` | [HfsmRuntimeMonitorWindow.cs](../../../Unity/Packages/com.abilitykit.hfsm/Editor/RuntimeMonitor/HfsmRuntimeMonitorWindow.cs) | 编辑器运行时监控入口 |

---

## 3. 运行时核心结构

`StateMachine<TOwnId,TStateId,TEvent>` 自身也是一个 `StateBase<TOwnId>`，因此父状态机可以把子状态机当作一个普通状态挂载。这是分层状态机的核心。

```mermaid
flowchart TB
    Parent["Parent StateMachine"] --> ChildAsState["Child StateMachine as StateBase"]
    ChildAsState --> Active["activeState"]
    SM["StateMachine<TOwnId,TStateId,TEvent>"] --> Bundles["stateBundlesByName"]
    Bundles --> Bundle["StateBundle"]
    Bundle --> State["StateBase<TStateId>"]
    Bundle --> Transitions["transitions"]
    Bundle --> TriggerTransitions["triggerToTransitions"]
    SM --> Any["transitionsFromAny"]
    SM --> TriggerAny["triggerTransitionsFromAny"]
    SM --> Pending["PendingTransition"]
```

运行时字段体现了几个关键设计：

| 字段 | 作用 |
|------|------|
| `stateBundlesByName` | 以状态 ID 保存状态对象、普通转移和事件转移 |
| `activeState` | 当前正在运行的状态 |
| `activeTransitions` | 当前状态可用的普通转移列表 |
| `activeTriggerTransitions` | 当前状态可用的事件转移索引 |
| `transitionsFromAny` | 从任意状态都可判定的普通转移 |
| `triggerTransitionsFromAny` | 从任意状态都可响应的事件转移 |
| `pendingTransition` | 因退出时间未满足而挂起的转移 |
| `startState` | 初始状态 |
| `rememberLastState` | 状态机重入时是否恢复上次状态 |

`OnLogic()` 的判定顺序是运行时契约，而不是图上的视觉顺序：

1. 先按添加顺序检查 from-any 普通转移。
2. 未命中时，再按添加顺序检查 active state 的普通转移。
3. 每组都在第一条成功转移后短路，本层每次 `OnLogic()` 最多执行一条转移。
4. 判定结束后总会调用当前 active state 的 `OnLogic()`；若刚完成转移，本 Tick 运行的是新状态逻辑。

因此全局转移天然优先于局部转移，同一组内的添加顺序也是优先级。条件不应依赖 Dictionary 枚举顺序，也不要假定转移 Tick 只执行 enter 而不执行新状态 logic。

---

## 4. 状态生命周期与退出时间

`State<TStateId,TEvent>` 把状态行为压缩成四类回调：

| 回调 | 来源 | 作用 |
|------|------|------|
| `onEnter` | 构造注入 | 进入状态时执行，并重置 timer |
| `onLogic` | 构造注入 | 每次状态机 logic tick 时执行 |
| `onExit` | 构造注入 | 离开状态时执行 |
| `canExit` | 构造注入 | 判断 pending transition 是否可以真正执行 |

退出时间是 HFSM 相比简单状态机更重要的部分。状态如果声明需要 exit time，状态机不会在转移条件满足时立刻切走，而是把目标转移写入 `pendingTransition`。随后状态的 `OnLogic` 或 `OnExitRequest` 在 `canExit` 返回 true 时调用 `fsm.StateCanExit()`。

```mermaid
sequenceDiagram
    participant SM as StateMachine
    participant State as Active State
    participant Trans as Transition

    SM->>Trans: ShouldTransition()
    Trans-->>SM: true
    alt active state needs exit time
        SM->>SM: store pendingTransition
        SM->>State: OnExitRequest()
        loop later logic ticks
            SM->>State: OnLogic()
            State->>State: canExit()
        end
        State->>SM: StateCanExit()
    else instant transition
        SM->>Trans: BeforeTransition()
        SM->>State: OnExit()
        SM->>SM: ChangeState(target)
        SM->>Trans: AfterTransition()
    end
```

这个机制适合动画收尾、技能前摇后摇、房间阶段确认、AI 行为退出保护等场景。转移条件可以先满足，但状态可以决定何时真正放行。

需要注意 pending 不是队列，而是单个 mutable 槽位。等待退出期间出现新的转移请求时，后一个请求会覆盖前一个；框架不保证按请求顺序排队。`StateCanExit()` 也只是处理调用瞬间已有的 pending，不会记忆一张长期退出许可证，所以在状态 `OnEnter()` 中提前调用没有效果。

`forceInstantly` 会绕过 active state 的 `needsExitTime`，清除已有 pending，并立即执行新转移。转移 listener 和 before/after callback 只在真正执行转移时调用，单纯写入或覆盖 pending 不触发它们。

---

## 5. 转移模型

`Transition<TStateId>` 由四部分组成：

| 成员 | 作用 |
|------|------|
| `from` | 来源状态 |
| `to` | 目标状态 |
| `condition` | 返回 true 时允许转移 |
| `beforeTransition` / `afterTransition` | 转移前后回调 |
| `forceInstantly` | 必要时绕过退出等待，直接执行转移 |

```mermaid
flowchart TB
    Logic["StateMachine.OnLogic"] --> Any["check from-any in add order"]
    Any -->|no match| Active["check active transitions in add order"]
    Any -->|first match| Request["RequestTransition"]
    Active -->|first match| Request
    Active -->|no match| Run["active state OnLogic"]
    Request --> Pending{Needs exit time?}
    Pending -->|yes| Store["replace pending transition"]
    Pending -->|no| Perform["PerformTransition"]
    Store --> Run
    Perform --> Run
```

状态转移的设计要点：

1. 普通转移适合每次 logic tick 判定。
2. trigger 转移同样先检查 from-any，再检查 active state；本层未触发转移时，事件才向 active child 传播。
3. from any 转移适合全局打断，例如死亡、断线、战斗结束，并且优先于局部转移。
4. null condition 被视为无条件 true；添加顺序决定同组优先级。
5. before/after 回调适合记录诊断或清理资源，但异常不被捕获，会直接传播给调用方。
6. force instant 应谨慎使用，它会绕过 exit-time 语义并丢弃此前 pending 请求。

---

## 6. 分层状态机

分层状态机让复杂状态拆成多个层级。例如战斗房间可以把 Ready、Loading、InBattle、Finished 放在外层；InBattle 内部再有 Warmup、Running、Pausing、Settlement；某个 AI actor 内部还可以有 Patrol、Chase、Cast、Retreat。

```mermaid
flowchart TB
    Room["Room StateMachine"] --> Ready["Ready"]
    Room --> Loading["Loading"]
    Room --> Battle["Battle child StateMachine"]
    Room --> Finished["Finished"]
    Battle --> Warmup["Warmup"]
    Battle --> Running["Running"]
    Battle --> Pausing["Pausing"]
    Battle --> Settlement["Settlement"]
```

分层的收益是：

| 收益 | 说明 |
|------|------|
| 局部转移收敛 | 子状态机只处理自己层级内的转移，父层不需要知道每个细节状态 |
| 父层可整体打断 | 父状态离开时，子状态机作为 state 一起退出 |
| 重用状态组 | 某些行为状态机可以在多个父状态下复用 |
| 运行时观察更清晰 | 当前活跃路径能表达为父状态到子状态的链路 |

---

## 7. Unity Graph Asset 与编辑器链路

`HfsmGraphAsset` 是 Unity 侧的图资产。它保存：

| 数据 | 字段 | 说明 |
|------|------|------|
| 图名称 | `_graphName` | 编辑器和导出显示名 |
| 节点 | `_nodes` | 多态节点集合，包含普通状态节点和状态机节点 |
| 序列化节点辅助 | `_serializedStateNodes`、`_serializedStateMachineNodes` | Unity 多态序列化兼容数据 |
| 边 | `_edges` | 状态转移边 |
| 参数 | `_parameters` | 条件和行为可引用的参数 |
| 根状态机 | `_rootStateMachineId` | 图的运行时入口 |
| 编辑器数据 | `_editorData` | zoom、pan、节点位置等视图状态 |

```mermaid
flowchart TB
    Asset["HfsmGraphAsset"] --> Nodes["HfsmNodeBase list"]
    Nodes --> StateNode["HfsmStateNode"]
    Nodes --> MachineNode["HfsmStateMachineNode"]
    Asset --> Edges["HfsmTransitionEdge list"]
    Asset --> Params["HfsmParameter list"]
    Asset --> EditorData["HfsmGraphEditorData"]
    Editor["HfsmEditorWindow"] --> Asset
    Asset --> Runtime["Runtime builder with current gaps"]
    Asset --> Export["Graph exporter"]
```

Graph asset 层有两个职责边界：

1. 保存 authoring 数据和编辑器视图数据。
2. 提供节点、边、参数的增删查和 GraphChanged 事件。

它不应该直接承载具体业务逻辑。业务行为应通过状态回调、ActionBehavior、参数绑定或运行时构建器接入。

当前 `ActionStateMachine.InitializeFromGraph()` 只能视为试验性节点构建器：它遍历 root 的 child nodes 创建状态，但没有读取或应用 `graph.Edges`，因此图中转移不会自动进入 runtime HFSM。状态 ID 还通过 `(TStateId)(object)node.GetName()` 转换，实际依赖字符串兼容；graph 或 root 为 null 时静默返回，方法也不会调用核心 `Init()`。嵌套构建路径虽传入 parent 参数，当前实现仍向外层实例添加状态，不能据此声明任意层级图已正确还原。

Graph `Validate()` 能检查节点、边和 root 的静态问题，但不能弥补 builder 没有消费边集合。投入生产前应针对具体图验证运行时状态、转移和层级，而不是只依赖资产校验成功。

---

## 8. 导出与描述器边界

`JsonGraphExporter` 不是直接读取 Unity 对象字段然后拼 JSON，而是依赖 `IGraphDescriptor`、`IGraphDataExtractor` 和 `IJsonSerializer`：

```mermaid
sequenceDiagram
    participant Tool as Export Tool
    participant Exporter as JsonGraphExporter
    participant Graph as IGraphDescriptor
    participant Extractor as IGraphDataExtractor
    participant Serializer as IJsonSerializer

    Tool->>Exporter: Export(graph, options)
    Exporter->>Extractor: Extract(graph, options)
    Extractor-->>Exporter: serializable graph data
    Exporter->>Serializer: Serialize(data, prettyPrint)
    Serializer-->>Exporter: json
    Exporter-->>Tool: ExportResult.Ok / Fail
```

这个边界当前可靠支持的是编辑器导出和静态检查：将 ScriptableObject 图变成可检查的 JSON artifact，用于版本比较、诊断或后续工具消费。仓库中尚未形成“导出 JSON 后由 runtime loader 完整还原 HFSM”的已验证闭环，不能把中间格式存在等同于热更新运行能力。

构造 exporter 时依赖为 null 会立即抛异常；进入 `Export()` 后，null graph、extractor 或 serializer 失败会返回 `ExportResult.Fail`，提取和序列化异常也会被 catch 并转成失败结果。调用方应同时区分构造期配置错误和导出期结构化失败。

---

## 9. 核心执行、初始化与失败边界

### 9.1 初始化与状态切换

root 状态机必须显式调用 `Init()`，它会选择 start state 并调用其 `OnEnter()`；没有 start state 时抛出 `MissingStartState`。嵌套状态机作为 state 进入父机时走自己的生命周期，`rememberLastState` 决定重入后恢复上次 active state 还是回到 start state。

目标状态缺失不是事务性失败。`ChangeState()` 先调用旧状态 `OnExit()`，随后才查找目标状态；查找失败会抛异常，此时旧状态已经执行退出副作用，而 active state 字段尚未成功切换。状态 ID 和配置必须在初始化前验证，不能依赖异常后保持完全未变。

### 9.2 回调与检查接口

状态 enter/logic/exit、转移 condition、before/after 和 listener 异常均不隔离，会穿透 `Init()`、`OnLogic()` 或 `Trigger()`。核心适合由受控业务代码驱动；插件回调需要在边界自行包装诊断和失败策略。

根状态机 `OnExit()` 会先保存 remember-last-state、清除 pending transition，再调用 active state 的 `OnExit()`，最后才把 `activeState` 设为 null。若 active state 退出抛错，pending 已清除但 activeState 仍保留；随后再次退出可能重复调用同一状态的副作用。这不是完整退出状态，宿主必须隔离异常并让状态退出逻辑可重复。

### 9.3 结构恢复不是生命周期回放

`RestoreRuntimeState(hasActiveState, activeStateName, rememberedStartStateName)` 面向确定性回滚，直接恢复 remembered start、active state 以及对应的普通/trigger transition 表，并清除 pending transition。它不会调用旧状态 `OnExit()`、恢复状态 `OnEnter()`、transition listener，也不会恢复状态对象内部字段。

恢复顺序也有局部提交：方法先校验 remembered start，随后立即写 `startState` 并清 pending；如果之后 active state 名称无效，方法抛错时前两项已经改变。因此回滚系统应在调用前验证两个状态 ID，并为每个子状态单独恢复数据。该 API 只负责状态机结构指针，不能单独构成完整 snapshot/rollback 协议。

`GetAllStateNames()`、`GetAllStates()`、`GetAllTransitions()` 等 inspection API 使用 LINQ `ToArray()` 生成快照，源码也标记为昂贵操作。它们适合编辑器、监控和低频诊断，不应每 Tick 调用。

---

## 10. 生产接入与成熟度证据

证据按 E0 源码、E1 构建、E2 真实消费者、E3 自动契约、E4 运行验收、E5 持续门禁分层；一个层面的证据不能替另一个层面背书。

| 运行面 | 仓库证据 | 成熟度判断 |
|--------|----------|------------|
| Core `.NET` 构建 | 2026-08-16 执行 `dotnet build src/AbilityKit.HFSM.Core/AbilityKit.HFSM.Core.csproj -c Release --no-restore` 为 0 警告、0 错误 | 证明 Core 在 `net10.0` 编译闭合，不证明 Unity Graph 或转移/恢复行为 |
| Core HFSM | Shooter Bot AI 使用 `HfsmHierarchicalRuntimeProfileBuilder`、递归行为树和自定义逻辑时间源；MOBA View Flow 使用双层状态机和 trigger transition | 有真实生产接入 |
| Runtime profile builder | Shooter profile 构建后立即 `Init()`；无效 transition 会被跳过，start state 无效时回退首个有效 state | 可用但需上层配置验证和诊断 |
| Unity Action/Graph | 包内有 Action、Graph 和 Behavior 基础测试 | 有工具基础，运行图转移尚未闭环 |
| Editor/exporter | 有窗口、monitor、descriptor 和 JSON exporter | 可用于 authoring/观察，未证明 runtime round-trip |
| Core 契约测试 | package Unity 测试主要覆盖 Action/Graph/Behavior，未发现 Core transition、pending 与 `RestoreRuntimeState` 的直接契约测试 | 全局优先级、pending 覆盖、exit-time、强制转移、恢复和初始化失败缺独立回归 |

Shooter 与 MOBA 的生产证据都直接构建 Core HFSM，不依赖 `ActionStateMachine.InitializeFromGraph()`。文档和采用评审应分别声明“Core 已生产使用”与“Graph 工具链待补强”，不能用前者替后者背书。

优先补充核心转移顺序、同 Tick 新状态 logic、trigger 下传、pending 覆盖、`StateCanExit()` 时点、`forceInstantly`、remember-last-state、目标缺失副作用、退出异常重试、结构恢复及子状态协同恢复，以及 Graph edges 到 runtime 和导出 round-trip 测试。本轮不运行 Unity，测试资产范围只作为源码审计结论，不能写成新鲜运行证据。

---

## 11. 和 Flow、Pipeline、Service 状态的边界

| 能力 | 适合表达 | 不适合表达 |
|------|----------|------------|
| HFSM | 状态、转移、退出时间、分层行为、可视化状态图 | 纯顺序脚本、大量一次性节点组合 |
| Flow | 顺序、并行、等待、竞速、finally、事件唤醒 | 长期状态归属和复杂转移图 |
| Pipeline | 带上下文的业务阶段执行、暂停恢复、中断、运行实例查询 | 通用图形化状态 authoring |
| Service 内部状态 | 简单业务阶段、少量 enum、局部生命周期 | 复杂分层转移和可视化调试 |

判断是否该用 HFSM，可以看三点：

1. 是否有稳定的状态集合和状态间转移关系。
2. 是否需要等待状态自己确认可以退出。
3. 是否需要在编辑器或运行时观察当前状态路径。

如果答案是否定的，直接使用 service 字段、Flow 节点或 Pipeline phase 通常更轻。

---

## 12. 扩展边界

- 新增状态行为时，优先封装为状态回调或 ActionBehavior，不要让状态机核心知道业务服务类型。
- 需要全局打断时，使用 from any transition，但要控制优先级和触发条件，避免普通状态转移被意外覆盖。
- 需要动画、技能后摇、网络确认等退出保护时，使用 exit-time/pending transition，而不是在外部强行切状态。
- Graph Asset 应保存 authoring 数据，运行时对象应由 builder 或 adapter 创建，避免编辑器资产直接持有运行时实例。
- 导出链路应通过 descriptor/extractor/serializer 组合扩展，避免每种格式都直接依赖 Unity 序列化字段。
- 分层状态机不要过度嵌套。超过两到三层后，应检查是否有一部分其实是 Flow 或 Pipeline。
- 运行时监控和 trace 应读取状态机公开检查接口，不应反射修改内部字段。

---

## 13. 和其他文档的关系

| 文档 | 关系 |
|------|------|
| [核心概念](../01-OverviewAndGettingStarted/02-CoreConcepts.md) | 该文只提到状态机类概念，本文补充 HFSM runtime 和 Unity 工具链 |
| [系统设计](../02-LogicalWorldDesign/04-SystemDesign.md) | Service 可以维护简单状态，HFSM 适合复杂状态图和退出时间 |
| [Flow 流程引擎](05-FlowEngine.md) | Flow 负责流程树，HFSM 负责状态图，两者都可用于启动和行为编排但边界不同 |
| [技能系统架构](../08-GameplayModules/01-SkillSystemArchitecture.md) | 技能 Pipeline 适合释放阶段，HFSM 适合长期行为或表现/AI 状态 |
| [测试流程](../10-EngineeringQuality/01-TestingWorkflow.md) | HFSM 有 Unity 测试工程入口，但当前仅覆盖 Action/Graph 基础行为，核心契约仍需补测 |

---

文档类型：Canonical 设计 | 事实基线：2026-08-16 | 证据等级：E0 源码、E2 Shooter/MOBA 消费者、历史 E3 Action/Graph/Behavior 基础测试；Core 转移/恢复全契约与 E4/E5 未完成

*文档版本：v3.2 | 最后更新：2026-08-16*
