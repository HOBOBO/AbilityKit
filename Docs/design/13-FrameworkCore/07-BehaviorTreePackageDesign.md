# 行为树包设计（com.abilitykit.behaviortree）

> 文档类型：FrameworkCore canonical
> 事实基线：2026-08-20
> 文档版本：v1.0
>
> 本文定义自研行为树包的目标架构：运行时 IR 与编辑配置分离、节点包外注册扩展、运行时调试注册中心与编辑器拉取式观察、确定性节点库。它是 [`02-BehaviorTreeIntegrationDesign.md`](02-BehaviorTreeIntegrationDesign.md) 所述 BTCore 集成的**替代路线**：新包落地并完成 MOBA 迁移后，`com.abilitykit.thirdparty.behaviortreeeditor` 整体退役。

## 一、替换背景

第三方 Saroce BehaviorTree（vendored）在五个目标上的差距（2026-08-20 调研结论）：

| 目标 | BTCore 现状 |
| --- | --- |
| 导出/编辑配置分离 | 无。编辑资产与运行时是同一份 `TypeNameHandling.All` 的 Newtonsoft JSON，内嵌 CLR 类型名 |
| 节点包外扩展 | `ExternalAction` 字符串协议（TypeName + `Dictionary<string,string>`），或编译期继承 BTCore 基类 |
| 编辑器看运行时状态 | 仅支持选中 `BehaviorTree` MonoBehaviour 的场景；AbilityKit 逻辑侧树不在 GameObject 上，路径不可用 |
| 统一注册中心 + 拉取 | 不存在 |
| 常用节点齐全 | 组合/装饰各 5 个，动作 2 个，条件 1 个；随机节点无种子、Wait 用 DateTime |

关键事实：MOBA demo 实际使用的两棵树是手写 JSON（`BTEXT:` 虚拟类型方言），编辑器到运行时的导出管线已经断裂。继续在 vendored 包上叠补丁（快照、类型归一化、校验器均已存在）收益递减，五个目标中四个需要修改运行时本体。

## 二、包结构与分层

```
Unity/Packages/com.abilitykit.behaviortree/
  package.json                 # 依赖 com.abilitykit.core + com.abilitykit.deterministic
  Runtime/
    com.abilitykit.behaviortree.asmdef      # noEngineReferences: true（纯 C#）
    BT/
      Definition/              # 运行时 IR：BtTreeDefinition / BtNodeDefinition / 属性包 / 黑板 schema
      Registry/                # 节点注册中心：描述符、工厂、attribute、程序集扫描
      Runtime/                 # 执行器：BtTreeRuntime、运行栈、条件重评估、快照
      Blackboard/              # 类型化黑板
      Nodes/                   # 内置确定性节点库（组合/装饰/条件/动作）
      Debug/                   # BtDebugRegistry（运行时观察注册中心）
      Io/                      # IR 的 JSON 加载/保存（导出格式的读写权威）
  Editor/
    com.abilitykit.behaviortree.editor.asmdef
    ...                        # 授权资产、图编辑窗口、导出、调试观察窗口（见第十节）
```

- 纯 C# 运行时通过 `src/AbilityKit.BehaviorTree` 投影编译（照 `AbilityKit.HFSM.Core` 模式），脱离 Unity 可测、可在 Orleans 服务端与 console 宿主运行。
- 运行时**不依赖** Unity 类型、Odin、Newtonsoft 的 `TypeNameHandling`；JSON 仅在加载/导出边界使用。
- `com.abilitykit.behavior`（持续行为框架）保持不变：行为树是 `IBehaviorDecision` 的一种实现，由接入方（如 MOBA demo）装配，框架包不反向依赖行为树包。

## 三、运行时 IR 与序列化格式

### 3.1 数据模型

```csharp
sealed class BtTreeDefinition {
    string TreeId;                 // 稳定标识（资源名）
    int FormatVersion;             // IR 格式版本，当前 1
    string RootNodeId;
    List<BtNodeDefinition> Nodes;
    BtBlackboardSchema Blackboard; // 声明式 key -> 类型
}

sealed class BtNodeDefinition {
    string Id;                     // 稳定字符串 id（编辑器生成，导出后不变）
    string Type;                   // 注册中心类型 id，如 "builtin.sequence" / "moba.select_nearest_enemy"
    BtPropertyBag Properties;      // 类型化属性，受节点描述符 schema 约束
    List<string> ChildIds;         // 有序子节点（直接内嵌，无独立边表）
}

sealed class BtAuthoringNodeMetadata {
    string NodeId;                 // 关联运行时节点 id
    string DisplayName;            // 仅编辑态
    string Comment;                // 仅编辑态
}
```

属性值是封闭标签联合：`Bool | Int64 | Fixed64 | String`。Fixed64 以 raw long 序列化。**禁止** object/CLR 类型值。

### 3.2 JSON 格式（导出格式权威）

- 无 `$type`、无程序集名、无编辑器字段。节点显示名、注释、布局、分组统一留在授权文档，均不进运行时 IR。
- 字段名稳定，新增字段必须向后兼容（加载端忽略未知字段）。
- 编辑态（节点显示信息、布局、分组、便签、撤销）只存在于编辑器授权资产中，导出时剥离。授权 schema v2 使用独立 `nodeMetadata`；v1 内嵌的 `name/comment` 加载时自动迁移。golden diff 测试保证"同授权资产两次导出字节一致"。
- 载体先 JSON（人类可读、可 golden diff、Unity Resources / Configs 目录直接可用）；格式版本字段预留二进制管线接口。

## 四、节点注册中心与包外扩展

### 4.1 描述符

```csharp
sealed class BtNodeDescriptor {
    string TypeId;                 // 全局唯一，建议 "domain.name" 前缀
    string DisplayName;
    string Category;               // 编辑器菜单分组
    BtNodeKind Kind;               // Composite | Decorator | Condition | Action
    int MinChildren, MaxChildren;  // 装饰器 = 1/1，动作条件 = 0/0，组合 ≥1/-1
    List<BtPropertyField> PropertySchema;  // 名称、类型、默认值、可选约束
    Func<IBtNode> Factory;
}
```

### 4.2 注册与发现

- 运行时：`BtNodeRegistry.Register(descriptor)`；领域包用 `[BtNodeType("moba.xxx", ...)]` 标注节点类 + `ScanAssembly` 辅助完成批量注册。
- **编辑器主动拉取**：编辑窗口从 `BtNodeRegistry.Descriptors` 构建节点创建菜单与通用属性编辑器。新增领域节点**零编辑器代码、零框架基类暴露**——编辑器认识的只是描述符，不是 CLR 继承链。这是对 BTCore "继承基类进 TypeCache / 手填 TypeName 字符串"两种模式的替换。
- 属性编辑由 PropertySchema 驱动生成（类型化控件 + 默认值 + 校验），不再有 `Dictionary<string,string>` 手填协议。
- 类型 id 在导出物中是字符串；加载时未知类型 id 是硬错误（白名单语义），不含反射回退。

## 五、执行模型

### 5.1 扁平化与运行栈（继承 BTCore 已验证语义）

- `Enable` 时前序遍历构建扁平索引：节点表、父索引、子索引、相对子序、最近组合父索引。
- 执行采用运行栈模型：主栈 + 并行分支栈；节点完成时先 `Stop` 再向父传播状态，装饰器转换状态，组合节点按 AbortType 管理条件重评估。
- 条件重评估（Self / LowerPriority / Both）语义照搬 BTCore：条件出栈时按父组合的 AbortType 生成重评估记录，重评估命中时弹出公共父以下的运行分支并按中止类型处理左/右优先级。

### 5.2 节点生命周期与上下文

```text
Init(节点上下文) -> Start -> Tick* -> Stop
```

- `BtNodeInitContext`：节点定义、类型化属性读取器（带默认值与 schema 校验）、注册中心。
- `IBtExecutionContext`：黑板、服务解析器（`Resolve<T>()`，领域 DI 入口）、时钟、节点内随机源。
- 领域节点通过服务解析器获取领域服务（对应 MOBA 的 `IMobaBTreeContextNode` 绑定），节点类不依赖任何具体宿主。

### 5.3 确定性规则（硬约束）

- 时钟：宿主每 tick 传入 `BtTickContext { int Frame; Fixed64 Time; }`。节点内**禁止** DateTime / Time.time / Environment.TickCount；等待/超时/冷却一律 Fixed64 秒或帧数。
- 随机：`DeterministicRandom`（SplitMix64）。每节点从 `树种子 ^ 节点扁平索引` 派生独立子流；随机节点的 `Sequence` 计数纳入快照。**禁止** `new Random()`。
- 遍历序：所有集合遍历顺序由扁平索引决定，不依赖字典枚举序。
- 快照往返后继续执行，相同输入序列产生相同结果（逐帧一致性测试覆盖）。

## 六、黑板协议

- `BtBlackboard`：key 必须在 `BtBlackboardSchema` 中声明（名称 + 类型）；写入类型不匹配是硬错误。
- 类型集与属性一致：Bool / Int64 / Fixed64 / String。Fixed64 存 raw。
- 节点在描述符中可声明读写 key 列表（`Reads`/`Writes` 元数据）：加载校验时检查 key 存在且类型一致，捕获拼写错误。
- 帧事实 / 意图 / 持久记忆的 key 分区约定由接入方定义（MOBA 的 `self.* / intent.* / memory.*` 协议整体迁移，不在框架包内）。

## 七、快照与回滚

- `BtTreeRuntimeSnapshot`（版本字段=1）：树状态、每节点状态与运行子索引、运行栈、条件重评估记录、黑板值（类型化 raw）、有状态节点的自定义负载（如 Wait 的剩余时间）、随机节点的 Sequence。
- 快照不含树定义；`Restore` 前校验**定义哈希**（节点 id + 类型 + 结构 + 黑板 schema 的确定性哈希），不匹配即拒绝——对应 BTCore 快照的 guid 逐项校验，但升级为整体哈希前置校验。
- 接入方通过 `IBehaviorRuntimeSnapshot`（既有协议）桥接；MOBA 迁移后 `MobaBTreeDecision.CaptureSnapshot/RestoreSnapshot` 直接包装本快照。

## 八、调试注册中心（编辑器拉取）

```csharp
static class BtDebugRegistry {
    BtTreeDebugHandle Register(IBtTreeDebugView view);   // 运行时实例启动时注册
    void Unregister(handle);
    IReadOnlyList<BtTreeDebugEntry> Entries;             // treeId、实例 id、owner 标签
    IBtTreeDebugView TryGet(handle);                     // 拉取：节点状态、当前路径、黑板、快照
}
```

- 纯 C#、进程内、无 Unity 引用；注册是**运行时→注册中心的单向登记**，运行时不知道任何编辑器类型。
- 编辑器观察窗口在 `EditorApplication.update` 里轮询 `Entries` 并按需拉取 `IBtTreeDebugView`，渲染节点状态着色、当前运行路径与黑板值——**编辑器主动获取**，不修改任何运行时结构。
- `BtDebugObservationSession` 独立负责采样、节点/黑板差异和有界历史；观察窗口只负责选择实例与绘制，采样语义可脱离 IMGUI 单测。
- 观察图通过可注册的 authoring document provider 旁加载编辑元数据；内置 provider 同时支持 AssetDatabase 资产与 headless manifest。来源优先级是显式契约：项目 provider > headless 权威源 > Unity 资产镜像；相同优先级按后注册者优先，每个注册句柄独立注销。子树展开结果携带来源树和原始节点 id，观察端无需解析展开 id；找不到来源时退回描述符名称与自动布局。
- 同一注册中心服务纯 .NET 场景（console/headless/服务端 dump），调试视图契约与 Unity 解耦。
- 注册受 `BtTreeRunOptions.DebugName` 控制：非空才注册；注册中心持有弱引用，宿主仍应通过 `Dispose`/`Disable` 正常终止运行节点并注销句柄。

## 九、内置节点库（确定性实现）

| 类别 | 节点 |
| --- | --- |
| 组合 | Sequence、Selector、Parallel（成功策略 RequireAll / FirstSuccess）、RandomSelector、RandomSequence（种子派生） |
| 装饰 | Inverter、ForceSuccess、ForceFailure、Repeater（次数/直到成功/直到失败）、Retry（失败重试次数）、Timeout（Fixed64 秒）、Cooldown（Fixed64 秒）、Once（首次评估后锁定）、UntilSuccess、UntilFailure |
| 条件 | BlackboardCompare（Bool/Int64/Fixed64 的等于/比较，key vs 常量或 key vs key）、Probability（种子派生）、BlackboardHasKey |
| 动作 | Wait（Fixed64 秒或帧数）、SetBlackboard、Log（走 Core 日志，非 UnityEngine.Debug）、Succeed、Fail |
| 引用 | Subtree（treeId 引用另一棵树，加载期内联展开：id 前缀 + 黑板并集 + 环检测 + 来源追踪 `BtTreeCompiler`） |

条件中断语义：挂在组合节点下的条件节点按组合 `AbortType` 参与重评估（属性在组合节点上）。领域节点（MOBA 13 个）在 demo 包内以同样机制注册，不进框架包。

## 十、编辑器与导出管线

复用触发器编辑器（TriggerAuthoring）已验证的基建模式：

- **授权资产**：`BtAuthoringAsset : ScriptableObject`，编辑态 JSON（节点显示信息/布局/分组/便签 + 运行时树结构）。编辑元数据通过 NodeId 旁挂，不进入 `BtNodeDefinition`。外部变更横幅、source sync 同模式。
- **图编辑窗口**：GraphView；节点视图、端口数量、菜单分组、属性面板全部由 `BtNodeRegistry.Descriptors` 拉取驱动；通用类型化属性编辑器替代逐节点 Inspector 类。
- **编辑器内部职责**：`BtAuthoringGraphWindow` 只编排窗口生命周期、模式、工具栏和保存/导出；`BtAuthoringGraphView` 管画布交互与连线投影，`BtAuthoringNodeView` 管单节点呈现，`BtNodeSearchProvider` 管节点创建目录，`BtAuthoringInspectorRenderer` 通过窄宿主接口渲染通用属性和观察详情。各层不持有领域节点实现。
- **文档会话**：`BtAuthoringDocumentSession` 以 Editor Platform `DocumentSession` 为基础，持有当前文档、只读标记、dirty 生命周期和有界 undo/redo；窗口不再分别维护互相依赖的状态集合，观察图从会话层拒绝写入。
- **一键导出**：授权资产 → 运行时 IR JSON（剥离布局），输出路径可配置（Resources / Configs）；导出前强制跑 `BtTreeValidator`，错误不清空旧产物。导出结果适配 Platform Export Report，文件写入使用 canonical atomic writer，内容相同时报告 `Unchanged`。
- **golden 测试**：示例授权资产导出结果与 golden JSON 比对；"加载→执行→快照"链路的端到端测试进 EditMode 测试目录。
- **运行时观察窗口**：按第八节拉取注册中心，节点着色（Running/Success/Failure/Inactive）、当前路径高亮、黑板表格、快照 diff。

### 10.1 Editor Platform 接入边界

BT 编辑器渐进复用 `com.abilitykit.base.editor` 的 Editor Platform：

- Localization 使用稳定 key、中英文资源、fallback 和 `LanguageChanged` 即时刷新，窗口销毁时对称退订；
- Toolbar、菜单和快捷键复用稳定 command id 及 `CanExecute`，观察模式命令保持只读；
- `BtTreeValidator` 的领域结果适配为 Platform Diagnostics，定位动作仍由 BT 根据 NodeId/黑板路径实现；
- Source Sync 使用 Platform classifier/policy 统一 InSync、资产变更、JSON 变更、Conflict、Missing 和 Invalid 状态，BT codec、hash、Undo、dirty 和资产 baseline 仍由 BT 拥有；
- Runtime export 使用 Platform Export Report 和 atomic writer，但运行时 IR schema、child → parent 边方向、descriptor-driven 节点目录及校验规则不进入 Platform。

Platform 接入不替换 GraphView，也不创建万能图编辑器。编辑器代码仅存在于 `Editor/` asmdef；运行时零 Unity 依赖保证服务端/console 不受影响。

## 十一、MOBA 迁移映射

| 现状 | 迁移后 |
| --- | --- |
| `MobaBTreeDecision` 内部持有 BTCore `BTree` | 外壳（`IBehaviorDecision`/`IBehaviorRuntimeSnapshot`/响应式语义/黑板协议）不变，内部换 `BtTreeRuntime` |
| `BTEXT:` 类型归一化 + 生成式清单 + 反射回退 | 删除。IR 类型 id 即字符串，注册表查不到是硬错误 |
| `NormalizeSerializedTypes` / `ValidateTreeStructure` | 由 `BtTreeValidator` + IR 加载校验整体替代 |
| 13 个领域节点（ExternalCondition/Action 子类） | `[BtNodeType]` 注册节点 + 类型化属性（searchRadius 等）+ 服务解析器取领域服务 |
| 2 棵手写树 JSON | 转译为新 IR 格式（结构等价，golden 对拍行为） |
| `MobaBTreeAssetLoader` | 路径与缓存策略不变，反序列化目标换 IR |

迁移验收：既有 Hero AI / Summon AI 冒烟与技能选择测试全绿（决策输出逐帧等价），随后才执行第十二节的退役删除。

## 十二、退役计划（已执行，2026-08-20）

1. ~~MOBA 迁移合入且 gate 全绿~~（回归 43/45，仅剩两个经 baseline 验证的既有失败；MobaConsoleSmoke 10/10）。
2. ~~删除第三方包与投影工程~~：`com.abilitykit.thirdparty.behaviortreeeditor` 整包、`src/AbilityKit.BTCore{,.Tests}`、sln 条目已删；`com.abilitykit.behavior` 的 BTree 桥接层（`BTreeDecisionAdapter`/`BTreeBlackboardBridge`/`IExternalNodeFactory`/`IBlackboard`，零消费者）及其 asmdef/package.json/csproj 依赖边已删；`demo.moba.codegen` 的 `MobaBTreeNodeAnalyzer`/`MobaBTreeNodeManifestGenerator`/`MobaBTreeNodeContract` 与 AKSG7001/7002 诊断已删；`Samples.Logic` 的 BTCore 引用与 `SampleBlackboard` 接口继承已清理。
3. ~~文档收口~~：`02-BehaviorTreeIntegrationDesign.md` 标记归档、`00-index.md` 已更新。
4. Odin（desperatedevs）另有消费方，**未**随本项退役。

## 十三、测试与证据

| 级别 | 内容 |
| --- | --- |
| E0 | 包源码 + src 投影工程存在 |
| E3 | `src/AbilityKit.BehaviorTree.Tests`（57 用例全绿）：执行语义（Sequence/Selector/Parallel/装饰器/超时冷却抢占/Self 中断）、生命周期（重复 Enable/Restart/RestartWhenComplete）、快照往返 + 定义哈希拒绝、确定性（同种子逐帧一致、概率模式跨种子区分、快照恢复后随机流精确续走）、校验负向集（结构/类型/属性/黑板）、JSON roundtrip 与 golden 稳定、注册中心与跨程序集扫描、调试注册中心、黑板类型读写 |
| E3 | 确定性包：`DeterministicRandom` 状态捕获/恢复（77 用例全绿） |
| E3 | MOBA 迁移后：既有 Behavior/Brain 测试与冒烟全绿 |
| E3（本轮编译证据） | Localization、Commands、Diagnostics、DocumentSession、Source Sync 和 atomic export 的 Editor 回归源码已进入测试程序集并通过定向 `dotnet` 编译，0 errors |
| E4（待执行） | 编辑器 golden、语言切换、同步冲突、Dirty、Undo/Redo、定位和导出测试必须由 Unity Test Runner 实际执行后才能计为通过 |
| E5 | 挂 `core-stability`（P1）gate；MOBA 侧回归进既有 gate；Editor Platform 总体验收门禁待补齐 |

`dotnet build` / `dotnet msbuild` 只证明 Unity 生成项目中的测试源码可以编译，不等于 Unity EditMode tests 已运行。本轮没有实际运行 Unity Test Runner，因此不能把新增编辑器回归声明为已通过。

已知非目标：热重载/在线下发（缓存失效与版本协议另立设计）；行为树可视化图布局自动整理；服务端远程调试（注册中心先进程内）。

实施边界：条件重评估已按标准语义**专项验证**（Self / LowerPriority / Both 三态，`ConditionalAbort_*` 系列测试）。实现为干净语义而非 BTCore 的 `-1` 激活移植：条件挂靠最近一个 `AbortType != None` 的组合祖先，翻转时（Self/Both 任意方向、LowerPriority 仅翻真且运行在更低优先级分支）中止该组合当前运行后代、回到条件分支重评。BTCore 原 `CompositeIndex=-1` + 组合出栈上提的激活协议经分析为无效路径，未沿用。

## 十四、内容管线：快速创建、目录与导出协议

> 文档类型：FrameworkCore canonical 补充（2026-08-21）
>
> 解决"树配置怎么建、一批配置怎么管、导出到哪、按什么格式"的内容工作流。

### 14.1 角色与权威

- **authoring 文档**：结构、布局、显示信息的内容权威；既可由 `BtAuthoringAsset` 承载，也可由 manifest 指向独立 JSON。二者绑定时按规范化文档哈希做双向同步与冲突检测。
- **授权资产**（`BtAuthoringAsset`）：Unity 内的 authoring 文档载体。一个资产一棵树，不自动高于已绑定的外部 authoring JSON。
- **项目目录资产**（`BtAuthoringProjectAsset`）：一批树的管理单元——显式注册树资产、声明导出目标列表、持有默认模板偏好。同触发器编辑器的 Project/Module 模式。
- **运行时 JSON**（导出产物）：运行时唯一消费格式，加载端（Unity Resources / console Configs / 服务端）只认它。

### 14.2 标识与命名约定

- `TreeId` 全项目唯一（目录资产校验重复），同时是资源名与导出文件名：`<导出目录>/<TreeId>.json`。
- 加载端约定沿用 `MobaBTreeAssetLoader`：按 TreeId 找 `<root>/<TreeId>`，无扩展名推断。
- 授权资产文件名建议与 TreeId 一致（快速创建向导默认如此）。

### 14.3 快速创建

- 菜单 `Assets/AbilityKit/Behavior Tree/Create Tree Wizard`：输入 TreeId + 显示名，选择模板（空树 / 反应式英雄 / 召唤守卫 / golden 示例），可选立即注册进指定项目目录资产。
- 模板为纯 C# 构造器（`BtAuthoringTemplates`），与 golden 共用实现——模板即"永远新鲜的 golden"。

### 14.4 目录管理

- 项目目录资产列出全部树（路径 + TreeId + 校验状态）；提供"扫描添加"（发现未注册的授权资产）与未注册提醒。
- 校验：TreeId 重复、空导出目标、导出目标目录不存在（可自动创建）。
- 批量操作：Validate All / Export All（按所属项目资产分组；未注册资产明确列出但不导出）。

### 14.5 导出协议

- **格式**：运行时 IR JSON（`BtTreeJson`，camelCase、无 CLR 类型名、无编辑元数据、golden 字节稳定）。`BtTreeJson` 会拒绝把授权文档误当运行时定义加载；二进制载体通过 `formatVersion` 预留，不在本版实现。
- **目标**：项目资产持有 `ExportTargets`（相对仓库根的目录列表），导出对**全部目标扇出**（Unity Resources + console Configs 一份源同时落两处，消灭手工双维护）。
- **增量**：导出内容 SHA-256 与目标现存一致则跳过写盘（报告标记 unchanged）。
- **门禁**：导出前强制 `BtTreeValidator`，错误不清空旧产物（既有约定）。
- **报告**：每次批量导出产出 per-tree/per-target 结果（Exported / Unchanged / Skipped-Error）。
- **headless 权威源**：项目清单用 `sourceKind=AuthoringDocument` 指向独立授权目录；`RuntimeDefinition` 仅作旧清单兼容。MOBA 源位于 `tools/bt-export/authoring/moba`，Resources / Console 目录都只是可重建产物。

### 14.6 实现分层

- 管线核心（模板、目录 DTO、扇出/增量/报告）在 `Runtime/BT/Authoring/`（纯 C#，dotnet 测试覆盖）；编辑器壳（向导/菜单/Inspector）在 `Editor/`，只做 UI 与资产 IO。
- 编辑器壳内部继续按会话、窗口编排、画布、节点视图、搜索目录、属性 renderer 分层；窗口与 renderer 之间使用面向文档/刷新命令的窄接口，避免把 `EditorWindow` 变成所有交互实现的公共依赖。
- MOBA demo 通过 headless manifest 管理两棵 authoring JSON；导出目标为 view.runtime Resources 与 Console Configs。Unity 观察工具直接读取同一 manifest，不要求再生成一份镜像项目资产。

## 十五、包外扩展点（框架节点 + 开放编辑体验）

> 内置节点放框架包，领域节点放业务包；业务包**只写描述符、不写编辑器代码**即可获得与内置节点一致的编辑体验。

### 15.1 扩展机制（运行时，纯 C#）

- 节点类实现 `BtNodeBase`/`BtConditionNodeBase`/`BtActionNodeBase`（或组合/装饰基类），标注 `[BtNodeType(typeId, 显示名, 分类, Kind)]`。
- 可选实现 `BtNodeDescriptorProvider` 补充完整描述符（端口约束、属性 schema、配色）。
- `BtNodeRegistry.ScanAssembly(assembly)` 一次扫描登记；编辑器目录 `BtEditorNodeCatalog` 扫描全部已加载程序集——**新增节点自动出现在菜单/属性面板/图编辑器中，零编辑器代码**。
- 领域依赖经 `IBtServiceResolver.Resolve<T>()` 注入（对应 MOBA 的 `MobaBTreeRuntimeContext`），节点不引用任何宿主类型。

### 15.2 属性 schema 的表达力（编辑器解释，不写 drawer）

`BtPropertyField` 支持：

| 能力 | 声明 | 编辑器呈现 | 校验 |
| --- | --- | --- | --- |
| 枚举 | `BtPropertyField.Enum(name, options, defaultIndex, tooltip)` | 下拉 | 值 ∈ [0, options.Count) |
| 黑板 key 引用 | `BtPropertyField.KeyRef(name, tooltip)` | 黑板 key 下拉 | 非空值必须已声明 |
| 数值范围 | `min`/`max` 参数 | 范围提示 | （值域校验预留） |
| 排序 | `order` | 面板按 order 升序 | — |
| 提示 | `tooltip` | tooltip | — |

- `BtNodeDescriptor.ColorHint`（hex）与 `MenuOrder`：节点主题色（缺省按 Kind 配色）与菜单排序。
- 内置节点已全部改用新 schema 作为示范（abortType/op/mode/level 等为枚举，leftKey/rightKey/key/fromKey 等为 key 引用）。

### 15.3 校验的联动

黑板 key 引用字段在加载/导出校验时自动检查 key 存在——**key 改名后无需手工排查，`Validate All`/导出即报**（`references undeclared blackboard key`）。

### 15.4 完整例子

测试程序集内 `ExternalMoodActionNode`（`src/AbilityKit.BehaviorTree.Tests/BtExtensionPointsTests.cs`）演示一个外部节点：`[BtNodeType]` + `BtNodeDescriptorProvider` + 枚举字段 + key 引用字段，经 `ScanAssembly` 后描述符携带完整 schema。MOBA 的 13 个领域节点（`demo.moba.runtime/MobaBTreeNodes.cs`）是生产级示例。

## 十六、源码入口

- 运行时 IR：`Unity/Packages/com.abilitykit.behaviortree/Runtime/BT/Definition/`
- 节点注册中心：`Unity/Packages/com.abilitykit.behaviortree/Runtime/BT/Registry/`
- 执行器：`Unity/Packages/com.abilitykit.behaviortree/Runtime/BT/Runtime/BtTreeRuntime.cs`
- 快照：`Unity/Packages/com.abilitykit.behaviortree/Runtime/BT/Runtime/BtTreeRuntimeSnapshot.cs`
- 内置节点库：`Unity/Packages/com.abilitykit.behaviortree/Runtime/BT/Nodes/`
- 调试注册中心：`Unity/Packages/com.abilitykit.behaviortree/Runtime/BT/Debug/BtDebugRegistry.cs`
- 授权模型/导出器/golden：`Unity/Packages/com.abilitykit.behaviortree/Runtime/BT/Authoring/`
- JSON IO：`Unity/Packages/com.abilitykit.behaviortree/Runtime/BT/Io/`
- 编辑器（授权资产/源同步/图编辑器/运行时观察/导出）：`Unity/Packages/com.abilitykit.behaviortree/Editor/`
- 编辑器测试：`Unity/Packages/com.abilitykit.behaviortree/Tests/Editor/`
- 投影工程：`src/AbilityKit.BehaviorTree/`，测试：`src/AbilityKit.BehaviorTree.Tests/`

---

*文档版本：v1.5 | 最后更新：2026-09-03 | 状态：P0-P4 领域实现已完成，并完成 Editor Platform 的 Localization、Commands、Diagnostics、DocumentSession、Source Sync 与 atomic export 渐进接入；相关测试源码已定向编译为 0 errors。本轮未运行 Unity Test Runner，Unity EditMode golden、语言切换、同步冲突、Dirty、Undo/Redo 与定位验收仍待实际执行。*
