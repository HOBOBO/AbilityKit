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

## 十六、BehaviorTree Editor 结构化优化计划

> 计划基线：2026-09-03 源码审计。该计划是对现有实现的渐进重组与增强，不重写 GraphView、不改变运行时协议，也不重复已经完成的 Editor Platform 接入。

### 16.1 现状裁定

当前 `Editor/` 的全部源码平铺在同一目录和同一 asmdef 中，但实际已经包含资产、文档会话、图画布、Inspector、同步、导出、诊断、平台接线和运行时观察等不同职责。已有设计基础应保留：

- `BtAuthoringDocumentSession` 已复用 Platform `EditorDocumentSession<TDocument>`；Source Sync、Export Report、atomic writer、Localization、Commands 和 Diagnostics 均已接入，不再把它们列为“未接入项”。
- `IBtAuthoringGraphHost`、`IBtAuthoringInspectorHost`、`IBtAuthoringDocumentProvider` 和 `BtDebugObservationSession` 已形成可渐进拆分的接缝。
- `BtAuthoringGraphWindow` 目前仍同时负责窗口生命周期、模式切换、命令注册、toolbar、文档 mutation、GraphView 重建、校验呈现、保存/导出以及观察轮询，是首要的职责收缩对象。
- `BtAuthoringGraphView` 同时执行画布呈现和节点/边/布局文档 mutation；`BtAuthoringInspectorRenderer` 同时承担树级编辑、节点 schema 编辑、黑板治理、分组管理、子树导航与运行详情，扩展贡献点不足。
- `BtDebugObservationWindow` 已具备实例筛选、暂停、节点树、运行路径、黑板变化和有界事件历史，但 UI、轮询、投影和交互仍集中在一个 IMGUI 窗口；采样频率固定，历史不可导航，缺少可插拔详情/过滤/overlay 与断线状态模型。
- `BtEditorNodeCatalog` 仍是静态全程序集扫描单例；`BtAuthoringDocumentCatalog` 虽支持 provider/priority，却未纳入 Platform module 的对称注册生命周期。当前 BT 只直接使用 Platform localization service，尚未成为完整的 `IEditorModule`，也未贡献 menu/panel。

### 16.2 不可破坏边界

1. 保持 child → parent 的 GraphView 端口和 `ChildIds` 投影语义。
2. 保持 descriptor-driven 节点目录及 `PropertySchema` 通用编辑路径；不建设万能图框架，不为领域节点添加硬编码分支。
3. 保持 authoring metadata 与 runtime definition 分离；布局、分组和便签不得进入运行时 IR。
4. 运行时调试继续由 Editor 主动拉取，且只读；Runtime 不引用 Editor、UI Toolkit、IMGUI 或 AssetDatabase。
5. 自动布局只补缺失布局或在用户明确执行命令时重排；不得隐式覆盖手工布局，不移动便签。
6. headless manifest、Unity asset mirror 和项目 provider 的来源优先级及 definition hash 匹配语义保持兼容。
7. 首轮只整理目录和内部职责，不因目录美化立即拆 asmdef、批量改变 namespace 或破坏 public API。

### 16.3 目标目录与所有权

第一阶段保持 `AbilityKit.BehaviorTree.Editor` 单一程序集和现有 namespace，按职责移动源码及其 `.meta` 文件：

```text
Editor/
  Bootstrap/
    BtEditorModule.cs
  Integration/
    Localization/
    Commands/
    Diagnostics/
  Authoring/
    Assets/
    Creation/
    Documents/
    Workspace/
    Graph/
    Inspectors/
    Catalog/
  Synchronization/
  Export/
  Debugging/
    Observation/
    Projection/
    Catalog/
  Shared/
  com.abilitykit.behaviortree.editor.asmdef
```

所有权规则：

- `Bootstrap` 仅作为 composition root，注册并持有 Platform module、localization、commands、menu/panel 及 BT 扩展目录的注销句柄。
- `Authoring/Documents` 持有文档会话、serializer 和 mutation service；窗口与视图不直接散写文档集合。
- `Authoring/Workspace` 持有窗口状态、模式、选择、dirty/source-sync/export/validation 协调器；`EditorWindow` 只处理 Unity 生命周期和根 UI 装配。
- `Authoring/Graph` 只投影视图、收集交互意图和维护 selection/viewport；连接约束和 mutation 进入可测试 controller/service。
- `Authoring/Inspectors` 只组合 section/field contributor，不拥有保存、导出或全图重建策略。
- `Debugging/Observation` 持有采样、历史、暂停与实例选择；`Projection` 把 observation snapshot 映射为图 overlay；`Catalog` 负责 authoring source resolution。
- `Integration` 只含平台适配，不含 BT 运行语义；领域校验仍以 `BtTreeValidator` 为权威。

如 P2 完成后依赖图证明 authoring 和 debugging 可以单向分离，再评估增加内部 asmdef；在此之前不拆程序集，避免 Unity 编译顺序和 public/internal 可见性成本。

### 16.4 目标抽象与扩展协议

#### Authoring workspace

- `BtAuthoringWorkspaceController`：持有 session、selection、mode 和 command context，暴露保存、导出、校验、undo/redo、切换文档等用例；窗口不直接 mutation。
- `IBtAuthoringMutationService`：集中新增/删除节点、连接、布局、分组、便签、根节点和黑板 key 变更；每次 mutation 定义单一 undo 边界和刷新影响范围。
- `IBtAuthoringSelectionService` 与 `BtAuthoringViewportState`：消除 200ms selection 轮询，保存每文档的选择、缩放和滚动状态。
- `IBtAuthoringInspectorSectionContributor`：按 tree/node/observation context 贡献可排序 section；内置 metadata、property schema、child order、blackboard、group 和 runtime details 均走同一协议。
- `IBtPropertyFieldEditor`：按 `BtPropertyFieldKind`/`BtValueType` 选择 editor；内置 Bool/Int64/Fixed64/String/Enum/BlackboardKeyRef，包外可注册额外呈现，但持久化值仍必须落入既有 `BtPropertyValue` 协议。
- `IBtAuthoringDiagnosticContributor`：在 runtime validator 结果之外贡献 authoring-only 诊断；统一支持 stable code、severity、path、locate，fix action 只能通过 mutation service 执行。
- `IBtNodeCatalogSource`：显式提供 descriptor 集合、来源和优先级；保留 assembly scan 作为默认 source，并支持 domain reload 后失效与冲突诊断。

#### Runtime debugging

- `BtObservationController`：分离 registry polling、selected instance、connection state、pause/freeze、sampling interval 和 session 生命周期。
- `BtObservationSnapshot`：一次不可变采样，包含 frame、node states、blackboard、active path 与 source mapping；窗口和 GraphView overlay 消费同一份快照，避免重复拉取。
- `IBtObservationDetailContributor`：为选中实例或节点贡献只读详情 section；不得返回 runtime mutation action。
- `IBtObservationFilter`：为实例、节点、黑板和事件提供可组合过滤；内置文本、running-path、state、changed-only。
- `IBtObservationOverlayContributor`：在不改 GraphView 语义的前提下贡献 badge、tooltip、border/marker；内置 Running/Success/Failure/Inactive 和诊断 overlay。
- 历史从“字符串事件列表”演进为有界结构化 sample/event timeline，支持选择历史帧、比较两个样本、跳到发生变化的节点/key，并显式显示 Live、Frozen、Disconnected、No Sample 状态。

这些 BT 专属协议留在 BehaviorTree Editor；只有被至少两个领域编辑器验证复用的通用 panel/toolbar/state 原语才下沉 Editor Platform。

### 16.5 分阶段实施顺序

#### P0：安全重组与平台模块化

1. 使用 Unity-aware move 保留每个脚本 `.meta` GUID，按 16.3 目录归档；保持 namespace、类型名和 public surface 不变。
2. 拆分当前混合的 localization/command 文件，新增单一 `BtEditorModule`，通过 `AbilityKitEditorPlatform.Modules` 对称注册 localization、menu/panel 和后续 extension catalogs；domain reload 时可重复初始化且无重复注册。
3. 引入 workspace context/controller，但先以 facade 委托现有逻辑；让 `BtAuthoringGraphWindow` 从约 800 行职责集合收缩为生命周期、UI composition 与 Unity 对话框适配。
4. 将硬编码 chrome 文案补齐 stable localization keys；descriptor-owned 名称、分类和 tooltip 保持由 descriptor 提供。
5. 回归 public 打开入口、asset inspector、create wizard、project inspector 和 observation menu。

#### P1：Authoring 职责拆分与可扩展 Inspector

1. 提取 mutation service 和 connection policy，GraphView 只产生意图；修复删除节点、边变更、移动、分组与黑板变更的统一 dirty/undo/diagnostic invalidation。
2. 使用 selection service 替代定时轮询；按影响范围做增量刷新，undo/redo 或 document replacement 才允许全图 rebuild。
3. 将 Inspector 拆成 contributor catalog + 内置 sections，并实现 property field editor registry；未知 schema kind 显示可定位诊断而不是静默缺失。
4. 使用 Platform diagnostics list/control 统一过滤、定位和 fix 呈现；校验结果不再通过解析 message 中的引号推测 NodeId，长期目标是 runtime validator 直接产出结构化 path，过渡期保留现有 adapter。
5. 增加 workspace user state：split width、面板折叠、搜索、viewport、最近文档；项目共享配置才进入 ProjectSettings。

#### P2：Authoring 工作流实用性

1. 加入 source-sync 状态卡、冲突决策和导出报告面板，使单树窗口与项目资产 Inspector 使用相同 model。
2. 节点搜索升级为关键词/category/type/state 过滤、最近使用和收藏，但创建仍完全由 descriptor 驱动。
3. 增加 multi-select 批量编辑、复制/粘贴 authoring 片段、重复节点和安全删除预览；粘贴必须重新生成 NodeId 并校验外部连接/黑板引用。
4. 增加树级 overview：root、孤立节点、子树依赖、黑板引用和诊断摘要；大型树默认按需刷新。
5. 为大树建立性能预算：静态空闲不轮询文档，拖动合并 undo，节点状态仅更新变化项，避免每 150/200ms 全量 LINQ 与全 Inspector 重绘。

#### P3：Runtime debugger 增强

1. 以 observation controller + immutable snapshot 统一独立观察窗口和观察图；采样频率可配置，暂停时不再自动拉取，仅显式单步采样。
2. 增加结构化 timeline、历史帧导航、A/B snapshot diff、节点状态转换和 blackboard changed-only 视图。
3. 实例列表增加 state/tree/owner 过滤、固定实例、断线保留和重新绑定提示；弱引用失效显示 Disconnected，不静默切到其他实例。
4. 通过 detail/filter/overlay contributor 提供包外只读调试扩展；贡献者异常隔离为 diagnostics，不中断主窗口。
5. 图观察支持 active path、最近变化、source-tree boundary、选中同步与“打开授权源/定位原节点”；若 definition hash 不匹配，明确显示 metadata fallback 原因。
6. 本阶段仍限定进程内调试；远程 transport、录制文件格式和跨进程 attach 另立协议，不混入 Editor API。

### 16.6 实施状态（2026-09-03）

- **P0 已完成**：新增 `BtEditorModule` composition root 与 authoring workspace controller/state；运行时快照恢复改为完整预验证与失败回滚，补齐空 custom state 恢复、生命周期异常策略、stop reason 和结构化校验诊断。观察窗口在实例断开后继续展示保留的只读快照，复制操作使用当前 displayed snapshot，不再从 live runtime 重新捕获。
- **P1 已完成**：运行时复用预编译 `BtTreeTopology` 及 NodeId/index 映射，并提供增量 debug view；Authoring 修改统一进入 `BtAuthoringMutationService`/workspace controller 的 undo、dirty 与 diagnostics invalidation 边界。`InspectorWidth`、搜索、每文档 selection、viewport/zoom、foldout 与 panel visibility 已接入 document-scoped user state，toolbar/search/layout/document orchestration 已迁入 `BtAuthoringWorkspacePresenter`。
- **P2 已完成基础闭环**：GraphView 已接入 copy/paste/duplicate，粘贴重新生成 NodeId，仅恢复选区内部连接，并同步复制 layout、metadata、groups 与 notes；已提供安全删除影响分析、多选批量属性、黑板读写/类型变更影响和 Search V2（模糊评分、过滤、最近、收藏、标签）。新增轻量 overview 与 100/500/1000 节点 authoring 预算测试；专用硬件上的长期主线程/分配基线仍属于持续性能治理。
- **P3 已完成协议基础**：observation controller、immutable snapshot、`Live/Frozen/Disconnected/NoSample`、结构化 timeline、历史导航、离线 scrub/playback/speed、A/B diff 和 Graph projection 已接入；新增 transport-neutral debug delta、Editor transport adapter、snapshot migration 与 generated/AOT registry。具体网络传输、鉴权和跨进程 attach 仍由宿主基于 transport boundary 实现。
- **测试与编译证据**：BehaviorTree Runtime xUnit 为 125/125；Unity BehaviorTree Editor EditMode 为 110/110（failed=0、skipped=0），结果见 `Unity/local/Logs/unity-editmode-AbilityKit.BehaviorTree.Editor.Tests-latest.xml`；Runtime、Editor、Editor Tests、Complete Runtime Observation Runtime/Editor 定向生成项目均为 0 errors；`Unity/Unity.sln --no-restore -m:1` 为 0 errors；package 定向 `git diff --check` 通过。严格 UTF-8、U+FFFD 与 GUID 审核分别为 0 invalid、0 replacement character、0 duplicate GUID。

### 16.7 兼容与迁移策略

- 移动脚本时脚本和 `.meta` 成对移动；提交前核对 GUID 未变化。新增目录提交对应 `.meta`，不让操作系统移动破坏 Unity 资产引用。
- P0/P1 保持 `AbilityKit.BehaviorTree.Editor` 程序集名、根 namespace、`BtAuthoringGraphWindow.Open/OpenObservation`、`IBtAuthoringDocumentProvider`、`BtAuthoringDocumentCatalog.RegisterProvider` 和既有 public session/diagnostic API。
- 新扩展 registry 返回独立 `IDisposable` 句柄；重复注册、同优先级冲突、贡献者异常和 domain reload 都必须有确定行为与测试。
- 旧静态 facade 在内部转发到新 service；至少经过一个版本和全仓消费者搜索后再标记 obsolete，不在目录迁移批次删除。
- 不手改 Unity-generated `.csproj`；新增/移动文件后先由 Unity 刷新项目。必要的临时 MSBuild validation target 仅用于陈旧 project graph 验证，刷新后删除。

### 16.8 验收门禁

每个批次至少执行：

1. `git diff --check`，并检查 moved script 的 `.meta` GUID 保持不变。
2. BehaviorTree Runtime、Editor、Editor Tests 相关生成项目定向 `dotnet build --no-restore`；随后执行单线程 `Unity.sln` 构建并保持 0 errors。
3. Unity Test Runner EditMode：现有 golden、DocumentSession、SourceSync、Catalog、Platform integration、ObservationSession 回归全部实际执行。
4. 新增架构测试：module 对称注册、无重复 contribution、mutation 单 undo 边界、read-only observation 拒绝写入、contributor 排序/异常隔离、domain reload 恢复。
5. 新增 UX/状态测试：dirty 文档切换、语言即时刷新、诊断定位/fix、source conflict、viewport 恢复、断线 observation、历史帧 diff。
6. 大树基准：至少覆盖 1,000 节点 authoring 打开/搜索/移动/校验与 1,000 节点 observation 增量采样；记录分配和主线程耗时基线，禁止靠降低正确性规避预算。
7. `dotnet build` 只记为源码编译证据；只有 Unity Test Runner 实际执行后，才能声明 EditMode/NUnit 回归通过。

## 十七、源码入口

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

## 十八、类型命名与源码布局治理

### 18.1 审计结论

2026-09-03 对包源码、Unity 其他 package 和 `src/` 消费者做一次性符号扫描，得到：

- `Bt*` / `IBt*` 声明共 248 个，其中 public 211 个、internal 37 个；Runtime 104 个、Editor/Editor Tests 144 个。
- 65 个类型已有包外源码引用，183 个当前没有包外引用。高引用公共契约包括 `BtValueType`、`BtNodeState`、`BtPropertyValue`、`BtTreeRuntime`、`BtTreeDefinition`、`BtNodeRegistry` 和扩展节点基类。
- 50 余个生产源码文件声明多个顶层类型；最高聚合文件一次包含 10 个类型。前缀泛滥和文件聚合是两个独立问题，不能用一次机械 rename 同时处理。
- `Bt` 并非领域术语的一部分。位于 `AbilityKit.BehaviorTree.*` 后，大多数类型继续携带该前缀没有信息增益；但现有 public API 已被多个包消费，直接删除前缀属于破坏性迁移。

完整机器可读清单位于 `local/Logs/behaviortree-prefixed-type-audit.csv`，该文件只作本地审计证据，不作为发布包内容。

### 18.2 目标命名空间与目录

程序集首轮保持不变，源码逐步迁入以下职责目录；namespace 在新 API 引入时与目录对齐：

```text
Runtime/
  Authoring/                 AbilityKit.BehaviorTree.Authoring
    Model/
    Export/
    Templates/
  Blackboard/               AbilityKit.BehaviorTree.Blackboard
  Definition/               AbilityKit.BehaviorTree.Definition
  Execution/                AbilityKit.BehaviorTree.Execution
    Lifecycle/
    Snapshots/
    Validation/
  Nodes/                    AbilityKit.BehaviorTree.Nodes
    Actions/
    Composites/
    Conditions/
    Decorators/
  Registry/                 AbilityKit.BehaviorTree.Registry
  Diagnostics/              AbilityKit.BehaviorTree.Diagnostics
  Serialization/            AbilityKit.BehaviorTree.Serialization
Editor/
  Bootstrap/                AbilityKit.BehaviorTree.Editor.Bootstrap
  Integration/              AbilityKit.BehaviorTree.Editor.Integration
  Authoring/
    Assets/                  AbilityKit.BehaviorTree.Editor.Authoring.Assets
    Creation/                AbilityKit.BehaviorTree.Editor.Authoring.Creation
    Documents/               AbilityKit.BehaviorTree.Editor.Authoring.Documents
    Workspace/               AbilityKit.BehaviorTree.Editor.Authoring.Workspace
    Graph/                   AbilityKit.BehaviorTree.Editor.Authoring.Graph
    Inspectors/              AbilityKit.BehaviorTree.Editor.Authoring.Inspectors
    Catalog/                 AbilityKit.BehaviorTree.Editor.Authoring.Catalog
  Synchronization/          AbilityKit.BehaviorTree.Editor.Synchronization
  Export/                   AbilityKit.BehaviorTree.Editor.Export
  Debugging/
    Observation/            AbilityKit.BehaviorTree.Editor.Debugging.Observation
    Projection/             AbilityKit.BehaviorTree.Editor.Debugging.Projection
    Contributors/           AbilityKit.BehaviorTree.Editor.Debugging.Contributors
```

`Runtime/BT/Runtime` 的重复层级最终消失。目录表达物理所有权，namespace 表达 API 语境，类型名只表达职责，例如目标 API 使用 `TreeRuntime`、`NodeDefinition`、`PropertyValue`、`AuthoringDocument`、`GraphWindow` 和 `ObservationController`，不再重复 `Bt` 或 `BehaviorTree`。

### 18.3 文件拆分规则

1. public 顶层类型原则上一类型一文件，文件名与类型名一致。
2. internal 类型在生命周期完全一致、仅服务于一个 owner 且不会独立测试时可与 owner 共文件；DTO 家族也必须按协议边界拆分，不能仅因“都用于录制”聚合十个类型。
3. 内置节点一节点一文件，按 Actions/Conditions/Composites/Decorators 分类；稳定 type id（如 `builtin.sequence`）不随 CLR 类型名变化。
4. enum、context、result、snapshot 等可独立复用的值对象单独成文件。
5. 脚本移动必须连同 `.meta` 移动；对现有文件抽取新脚本时，原 owner 保留旧 GUID，新脚本生成新 GUID。

### 18.4 兼容迁移策略

按风险而不是按目录批量改名：

- **N0：物理整理**。先拆文件、移动目录并保持程序集、namespace、类型名和序列化协议不变。这一批零 API 行为变化。
- **N1：internal API**。Editor 和 Runtime internal 类型直接移入职责 namespace 并移除 `Bt`；同批更新包内测试。
- **N2：零包外引用的 public Editor API**。先确认不是 Unity 序列化入口；非序列化类型提供新命名，旧类型保留至少一个发布周期并标记 `Obsolete`。窗口、资产和 Inspector 类型使用 `MovedFrom` 或保留薄壳，且保持脚本 GUID。
- **N3：public Runtime API 与扩展协议**。新无前缀 API 进入细分 namespace；旧 API 作为兼容 facade/adapter 保留。由于 class、enum 和 attribute 不能靠 type forwarding 无损解决所有源码与二进制场景，迁移以源码兼容、JSON 兼容和 Unity 资产兼容为目标，最终删除安排在 major version。
- **N4：消费者迁移与退役**。先迁移本仓库 package、`src/`、Samples 和文档；连续一个版本全仓零旧 API 使用后才允许删除 facade。

禁止事项：不改变 JSON 字段、format version、节点 type id、枚举数值、definition hash 语义；不在同一批同时做行为修改；不通过继承包装 sealed DTO；不为追求“零前缀”制造 `Runtime.Runtime` 或含义模糊的根命名空间类型。

### 18.5 首批迁移矩阵

| 旧类型/文件 | 目标 | 风险 | 首批动作 |
| --- | --- | --- | --- |
| `BtActions.cs` / `BtConditions.cs` / `BtComposites.cs` / `BtDecorators.cs` | `Nodes/<Kind>/<NodeName>.cs` | 中：节点类型 public，部分有包外引用 | 仅拆文件和移动 `.meta` 所属 owner，不改类型名/namespace/type id |
| `BtAuthoringModels.cs` | `Authoring/Model/` 下独立模型文件 | 中：authoring DTO public | 仅拆文件，不改 JSON 属性和 CLR 类型 |
| `BtNodeBase.cs` / `BtParentNodes.cs` | `Execution/Nodes/` 下 contract/context/base 独立文件 | 高：扩展节点继承面 | 仅拆文件；无前缀 API 放到 N3 |
| `BtTreeRuntimeSnapshot.cs` | `Execution/Snapshots/` 下独立 snapshot 文件 | 高：存档协议 | 仅拆文件；保持版本和字段 |
| `BtNodeDescriptor.cs` | `Registry/Descriptors/` | 高：包外节点描述协议 | 仅拆文件；保持 schema |
| `BtAuthoringMutationService.cs` | `Editor/Authoring/Documents/Mutation/` | 低：核心类型 internal | 拆文件后进入 N1 去前缀 |
| `BtAuthoringWorkspacePresenter.cs` | `Editor/Authoring/Workspace/Presentation/` | 低：类型 internal | 拆 presenter、search result、overview、clipboard adapter，进入 N1 |
| `BtObservationRecording.cs` | `Editor/Debugging/Observation/Recording/` | 中：当前 public 但零包外引用 | 先拆 DTO/recording/replay，再按 N2 提供无前缀 API |

### 18.6 N4 消费者迁移（2026-09-04 完成）

无前缀 API 已覆盖 `Definition`、`Blackboard`、`Execution`、`Nodes`、`Registry`、`Diagnostics`、`Serialization`、`Authoring` 及细分 Editor namespace。MOBA runtime/tests、BehaviorTree CLI、Complete Runtime Observation Sample Runtime/Editor、BehaviorTree Editor 与普通 Editor Tests 均已迁移。旧 API 仅允许存在于 obsolete compatibility facade/adapter、`MovedFrom` 历史身份和显式 compatibility canary；普通消费者不得通过扩大 `CS0618` suppression 回退。

`DebugRegistry` 已提供 `Register` / `Unregister`。调试桥根据真实 capability 选择普通或增量 adapter：普通 `TreeDebugView` 往返后不会实现 `TreeDebugDeltaView`；真实增量视图则保留 sequence、full/delta、frame、changed nodes、optional blackboard，并原样透传 `knownSequence` 和 `includeBlackboard`。对应正向与负向 Unity 测试均已纳入 110/110 EditMode 结果。

兼容期间保持 JSON 字段、format version、node type id、枚举数值、snapshot schema/version、definition hash、确定性执行语义和生命周期 stop reason 不变。CLI 对两棵 golden tree 的双目标导出仍为 4/4 Unchanged。`ExecutionContext` / `System.Threading.ExecutionContext` 与 `ValueType` / `System.ValueType` 的同名问题由消费方使用明确 namespace 或 alias 消歧，不恢复类型前缀。

### 18.7 API 聚合拆分与 canonical ownership 反转

`Runtime/BT/Api/BehaviorTreeApi.cs` 等七个聚合文件已按 namespace 零行为拆分完毕（2026-09-07）：`Definition`（12 类型）、`Nodes`（7）、`Blackboard`（2）、`Registry`（9）、`Serialization`（2）、`Diagnostics`（14）各成一目录，public 顶层类型原则上一类型一文件；原聚合文件 GUID 保留给明确 owner，新文件生成新 GUID。拆分批次未改变协议或实现所有权。Runtime 131/131 测试仍绿。

2026-09-07 继续（N0 物理整理收尾）：Runtime/Editor 目录已按 §18.2/§16.3 落地——删除 `Runtime/BT` 冗余层、合并 `Api` 中转目录进各领域目录、`Debug→Diagnostics`/`Io→Serialization`/`Runtime→Execution` 改名、Editor 根目录 22 个平铺文件归档到 `Authoring/{Assets,Creation,Documents,Graph,Inspectors,Catalog}`/`Export`/`Synchronization`/`Integration`/`Debugging/Observation`；39 个文件去掉 `Bt` 文件名前缀与类名一致（类名已无前缀）；删除 `NodesGlobalUsings.cs` 的 `global using`（C# 10 违规，违反框架 C# 9 约束）并为 32 个依赖文件补齐显式 using。Runtime 131/131、Unity 编译检查 0 errors。

canonical ownership 按以下顺序逐簇反转：Definition 值对象与转换 → Blackboard → Registry/descriptor → Serialization → Execution/runtime/snapshot → Diagnostics/debug adapters。目标由“新 API 调用 obsolete legacy 实现”变为“obsolete legacy API 转发新 canonical 实现”。每一簇必须具备双向 DTO/enum 转换、JSON golden、definition hash、snapshot roundtrip、determinism 和 debug capability 测试后才能切换；禁止一次性全文件反转。

2026-09-07 首簇 Definition 已落地：新增等价性门禁测试（legacy ↔ canonical 的 hash/JSON 双向往返等价，3 测试），`BtTreeDefinition.ComputeDefinitionHash`/`DeepClone` 反转为转发 `TreeDefinition` 实现（删除重复的 hash/deepclone/`HashString`/`CloneValue` helper），`TreeValidator` 引用 legacy `BtCompositeNode.AbortTypeProperty` 改为新 `CompositeNode.AbortTypeProperty`。Registry 簇 `NodeRegistry.ScanAssembly` 已反转：legacy `BtNodeTypeAttribute` 扫描逻辑搬入新方法，单 descriptor 转换替代整 registry 往返（消除对 legacy `ScanAssembly` 的调用）；`ToLegacy`/`ReplaceWithLegacy`/`FromLegacy` 仍被 `BtExecutionCompatibility`/Authoring 使用，保留。Blackboard 簇已反转：legacy `BtBlackboard` 持新 `Blackboard` 转发（`Create`/`Wrap`/`Canonical` 构造面），新 `Blackboard` 删除 `_legacyProjection` 双向同步桥，`Inner`/`FromLegacy` 改为共享语义（复制→共享，消除手动同步），删除 `SyncToCanonical` no-op 方法及 27 处调用。Serialization 簇已反转：legacy `BtTreeJson` 转发新 `TreeJson`（`Save`/`Load`/`SaveSnapshot`/`LoadSnapshot` 走 `TreeDefinition`/`TreeRuntimeSnapshot` 转换），删除重复的 Newtonsoft settings/`BtPropertyValueConverter`/`BtPropertyBagConverter`（`CreateSettings` 无外部调用者，属死代码）。Diagnostics 簇已反转（N4 阶段）：legacy `BtDebugRegistry` 转发新 `DebugRegistry`、legacy `BtTreeRuntime` 转发新 `TreeRuntime`，adapters 是必要双向转换桥接。六簇 canonical ownership 反转全部完成，新 API 不再调用 legacy 实现。Runtime 134/134、Unity 编译检查 0 errors。

`Documentation~` 属 UPM 隐藏文档目录，不由 AssetDatabase 导入，其目录和 Markdown `.meta` 为明确例外；其余 package metadata 审核为 0 orphan、0 duplicate GUID。

---

*文档版本：v1.17 | 最后更新：2026-09-07 | 状态：BehaviorTree Editor P0-P3、完整 Sample、整仓无前缀消费者迁移与双向 debug capability 修正已完成；Runtime 134/134、Unity 编译检查 0 errors。API 聚合文件零行为拆分、Runtime/Editor 物理目录整理（去 `Runtime/BT` 层、合并 `Api` 中转、`Debug→Diagnostics`/`Io→Serialization`/`Runtime→Execution` 改名、Editor 平铺归档、39 文件去 `Bt` 文件名前缀、删除 `global using` 恢复 C# 9）均已完成。canonical ownership 反转六簇全部完成（Definition hash/deepclone 转发 + 等价性门禁、Registry `ScanAssembly` 逻辑搬移、Blackboard legacy 转发新 + 删双向同步桥、Serialization `BtTreeJson` 转发 `TreeJson`、Execution/Diagnostics N4 阶段已 canonical），新 API 不再调用 legacy 实现；最终删除 legacy `Bt*` facade + 转换（major version）。*
