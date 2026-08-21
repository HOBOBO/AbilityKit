# 8.3 触发器编辑器生产链设计

> 文档类型：P0 规划草案
> 基线日期：2026-08-19
> 状态：核心决策已确认，进入 P0/P1 最小闭环实现

本文定义 AbilityKit 当前项目组触发器编辑器的领域模型、数据流、编辑边界和迁移策略。本文不要求一次实现完整的 Y3 式图编辑器，优先建立稳定的配置创作到运行时发布闭环。

## 1. 目标与非目标

### 1.1 目标

- 让项目组可以用统一编辑器配置事件、条件、行为、变量和模板。
- 编辑器配置必须能够经过校验后生成当前 Trigger Runtime 可加载的计划数据。
- 条件、行为和变量引用具备类型约束、作用域约束和可追踪来源。
- 模板、条件组和行为组可以复用，同时保留实例覆盖关系。
- 新 Source JSON 可以双向同步，未知内容不得静默丢失。
- 导出结果可重复、可审查、可在 CI 中校验。

### 1.2 非目标

- 第一阶段不追求跨项目的完全通用 DSL。
- 第一阶段不把所有配置迁移为全画布节点图。
- 编辑器不直接持有运行时实例，不把 Odin 类型作为 Runtime API。
- 编辑器不替代技能、Buff、Effect、Projectile 或 MOBA 业务服务。

## 2. 当前基线与主要缺口

当前编辑入口是 `AbilityModuleSO` 和 `AbilityListWindow`：模块保存触发器列表，触发器通过 Odin `SerializeReference` 保存强类型条件和行为，并以 JSON 配置作为兜底。现有强类型目录已经覆盖部分复合条件、参数条件和项目行为，但并未覆盖完整的 Runtime Plan 语义。

当前运行时计划还包括 Phase、Priority、InterruptPriority、Schedule、Cue、Scope、ExecutionControl、Payload/Context/Blackboard 引用和 Action/Condition Registry。编辑器若不纳入这些字段，仍会出现“编辑器能配置的内容少于运行时实际支持内容”的分裂。

现有 `TriggerTemplateSO`、Source JSON 的模板/组引用和 Unity Asset 导出链路还没有形成一条统一的 canonical pipeline。旧 JSON 导入器目前只把少量行为映射为强类型，其余内容保留为 JSON 兜底。新编辑器不以这条链路为兼容基线，旧 Trigger JSON 不要求迁移到新模型。

局部变量已有编辑器雏形；全局变量 Provider 目前只是扩展点，尚未连接项目级变量目录。因此 Global Blackboard 必须先定义明确的数据资产和所有权，不能继续依赖隐式扫描。

## 3. 推荐分层

```text
Authoring Assets
    TriggerProjectAsset / AbilityModuleAsset / TriggerTemplateAsset
              |
              v
Normalize + Validate + Resolve References
              |
              v
Canonical Source DTO / Source JSON
              |
              v
Runtime Plan Compiler
              |
              v
Runtime Plan JSON / Generated Runtime Data
```

### 3.1 Authoring 层

Authoring 层只保存编辑所需的数据和引用关系，包括节点显示顺序、模板来源、实例覆盖、局部变量和编辑器元数据。它可以使用 Unity `ScriptableObject` 和 Odin，但不能被 Runtime 直接依赖。

### 3.2 Source 层

Source 层是人类可读、可 diff、可迁移的规范中间格式。它应包含 Schema、Version、Metadata，并使用稳定的字符串名称表达事件、动作、条件和变量引用。Source JSON 同时是受支持的 AI 编辑入口，可以显式反写到 Unity Authoring Asset。

`ScriptableObject` 仍是 Unity 内的主编辑数据，Source JSON 不是第二套不受控主存储。双向同步采用“一模块一文件 + 同步基线哈希”：

- 导入前检查 Asset 是否相对上次同步基线发生变化；
- 导出前检查 JSON 是否相对上次同步基线发生变化；
- 两侧都发生变化时报告冲突，不静默覆盖；
- 用户可以在查看差异后显式选择强制导入或强制导出；
- 文件监听只提示外部变化，不自动写入 Asset。

### 3.3 Runtime 层

Runtime 层是编译产物，使用 Trigger Runtime 所需的 ID、索引和已解析参数。Runtime JSON 或生成数据禁止人工编辑。

## 4. 核心领域模型

### 4.1 项目资产

```text
TriggerProjectAsset
├── Metadata
├── EventCatalog
├── GlobalBlackboardCatalog
├── TypeCatalog / TypeRegistry Settings
└── TemplateCatalog
```

项目资产负责项目级目录和规则，不负责保存某个技能或 Buff 的具体触发器。

### 4.2 模块资产

```text
AbilityModuleAsset
├── ModuleId
├── DisplayName
├── ModuleType
├── LocalBlackboardDeclarations
└── Triggers[]
```

模块类型可以是 Ability、Buff、Passive、Projectile、Summon 或项目自定义类型。模块类型只影响默认目录和校验规则，不应改变 Trigger 的核心模型。

### 4.3 触发器定义

```text
TriggerDefinition
├── Id / Name / Enabled
├── Event Reference
├── Phase / Priority / InterruptPriority
├── Scope / AllowExternal
├── Schedule / Cue / ExecutionControl
├── TemplateBinding
├── ConditionTree
├── ActionTree
├── LocalBlackboard
└── Note
```

条件和行为不再以“平面列表 + 任意字典”为长期模型，而是分别形成可递归的 Condition Tree 和 Action Tree。旧列表数据可以在导入时转换为根节点。

## 5. 事件目录

事件不能只作为可自由输入的字符串。项目事件目录至少应描述：

```text
EventDefinition
├── Id
├── DisplayName / Category
├── Payload Type
├── Payload Fields
├── AllowExternal
├── Deterministic
└── Description
```

事件目录的用途包括：

- 事件下拉选择和分类搜索；
- Payload 字段提示；
- 条件参数的类型过滤；
- 外部触发和确定性规则校验；
- 事件改名或废弃时的迁移诊断。

## 6. 条件、行为和类型注册

每个条件和行为都应同时提供运行时注册信息和编辑器描述：

```text
TypeDescriptor
├── Type
├── DisplayName / Category / Order
├── Description
├── Parameters[]
├── SupportsChildren
├── AllowedValueSources
├── Deterministic
└── Runtime Resolver / Compiler Mapping
```

参数描述至少需要包括名称、类型、必填性、默认值、可选值、可用值来源和作用域限制。

条件第一阶段固定支持：

- All；
- Any；
- Not；
- 项目原子条件；
- 可选 Condition Group 引用。

行为第一阶段固定支持：

- 原子行为；
- Sequence；
- 项目常用的 Effect、Buff、Projectile、Summon、Presentation 和变量行为；
- 可选 Action Group 引用。

不支持强类型编辑的历史类型必须显示为“兼容原始节点”，同时保留原始 Type 和 Args，并在校验面板中显示警告。

## 7. 值引用与黑板边界

编辑器统一使用 Value Reference 表达参数来源：

| 来源 | 语义 | 默认权限 |
|------|------|----------|
| Const | 配置常量 | 读 |
| Payload | 当前事件数据 | 读 |
| Context | 当前执行上下文 | 读 |
| Local | 当前模块/触发器变量 | 读写，按声明限制 |
| Global | 项目或战斗全局变量 | 按目录限制 |
| TemplateParam | 模板实例绑定 | 读 |
| Expr | 表达式计算结果 | 读 |

边界规则：

- Payload 表达一次事件携带的数据，不应被强制复制到黑板。
- Context 是运行时上下文访问协议，不作为任意对象容器。
- Blackboard 只保存需要跨动作、跨步骤或跨触发器共享的状态。
- 每个黑板 Key 都必须有类型、默认值、读写权限、作用域和描述。
- 全局黑板由项目目录资产提供，Provider 只作为扩展查询接口。

建议增加 `GlobalBlackboardCatalogAsset`，由它定义全局 Key 的稳定名称和类型；局部变量则属于模块或触发器资产，不依赖静态编辑器上下文推断。

## 8. 模板和可复用组

### 8.1 模板

模板资产应包含：

- TemplateId；
- TemplateVersion；
- 参数定义、类型和默认值；
- 条件树和行为树；
- 参数允许的值来源；
- 描述和兼容策略。

模板实例只保存引用和覆盖：

```text
TemplateBinding
├── TemplateId
├── TemplateVersion
└── Bindings / Overrides
```

编辑器显示模板来源和展开预览；导出时根据 Runtime 需要展开为最终计划。模板引用必须检查缺失、版本不兼容和循环引用。

### 8.2 条件组和行为组

组引用用于复用局部规则，但不能隐藏最终执行顺序。编辑器应支持：

- 查看组来源；
- 展开只读预览；
- 复制为本地内容；
- 检查组引用循环；
- 导出前确定性展开。

首版组定义归属于单个 Module，ConditionGroup 和 ActionGroup 各自保存稳定 ID、显示信息和一个树根。节点通过 `groupReference` 引用同类型组；引用节点不得同时保存 Type、Args 或 Children。Source JSON 保留引用关系，校验、展开预览和复制为本地内容共享同一个确定性解析器，后续 Runtime 导出也必须复用该解析器。跨 Module 复用由后续 Template Asset 承担，避免组目录过早演变成缺少版本协议的全局依赖。

## 9. 校验和诊断

校验分为三个层次：

### 9.1 编辑时校验

- ID 为空或重复；
- Event 不存在；
- Type 不存在；
- 必填参数缺失；
- 参数类型不匹配；
- Blackboard Key 不存在；
- 作用域不允许；
- 只读变量被写入；
- 复合节点没有子项；
- 模板参数未绑定。

### 9.2 编译前校验

- 无法解析的 Action/Condition；
- 无法解析的 Payload/Context 字段；
- 非法 Schedule、Cue 或 ExecutionControl；
- 重复 TriggerId；
- 不确定性规则违反项目配置；
- 模板或组展开失败。

### 9.3 Runtime 兼容校验

- Runtime Plan 能否加载；
- 计划索引是否完整；
- ActionId/FunctionId 是否存在；
- Schema/Version 是否兼容；
- 运行时所需参数是否完整。

错误需要带有稳定 Code、资源路径、触发器 ID 和字段路径，支持点击定位。警告不能被导出日志替代，必须出现在编辑器校验面板中。

## 10. 导入、导出和迁移

确认后的数据方向：

```text
Unity Authoring Asset <-> Canonical Source JSON -> Runtime Plan JSON
```

规则如下：

- Unity Asset 是 Unity 内的主要编辑入口；
- Source JSON 是受支持的 AI 编辑和反写入口；
- Source JSON 是可审查的规范中间格式；
- Runtime Plan JSON 是生成产物；
- 生成产物禁止人工编辑；
- 导出采用临时文件和原子替换；
- JSON Schema 和 Version 必须显式保存；
- 字段顺序和数组顺序保持稳定；
- 未知字段或节点导入时必须阻断或保留，不静默丢失；
- 新链路不承担 Legacy Trigger JSON 兼容；
- 导入导出需要 golden file 和 round-trip 测试。

旧版 `AbilityModuleSO` 与新 Authoring Asset 并存。新编辑器不直接修改历史资源的序列化结构，也不为 Legacy Trigger JSON 增加长期适配层；需要的真实配置应按新模型重新建立。

### 10.1 JSON 反写协议

Source JSON 采用一模块一文件，文件中包含稳定的 `moduleId`、`schema` 和 `version`。Asset 保存最近一次成功同步的 Source 路径和内容哈希。

| 状态 | 含义 | 默认操作 |
|------|------|----------|
| InSync | Asset 与 JSON 都等于基线 | 允许导入或导出 |
| AssetChanged | 只有 Asset 改动 | 允许导出，导入需确认 |
| JsonChanged | 只有 JSON 改动 | 允许导入，导出需确认 |
| Conflict | 两侧都改动 | 阻断自动同步，要求显式选择方向 |
| Untracked | 尚无同步基线 | 首次导入或导出建立基线 |

同步成功后更新 Asset 基线哈希和文件内容。所有写文件操作采用临时文件后原子替换；所有写 Asset 操作接入 Unity Undo、Dirty 和 SaveAssets 流程。

### 10.2 编解码器抽象（格式不固定为 JSON）

Source 读写建立在 `ITriggerSourceCodec<TDocument>` 抽象上（`Editor/Utilities/TriggerSourceCodec.cs`）：

- 注册表 `TriggerSourceCodecs` 按文件扩展名（忽略大小写）解析格式；JSON 是默认实现且默认值固定，注册新 codec 只增加可解析的扩展名，不抢占默认。
- codec 契约：`Deserialize` 必须对未知字段报错或显式保留（JSON 实现为 `MissingMemberHandling.Error`），不得静默丢弃；`Serialize` 输出必须能被同一 codec 完整还原。
- 内容基线哈希在 `TriggerSourceCanonical` 中基于固定的 DOM 规范投影（camelCase、忽略 null、无缩进、字符串枚举）计算，与 codec 无关——同一内容换格式导出/导入，基线哈希不变，不产生假冲突。
- 临时文件原子写入与路径归一化属于格式无关管线，不进 codec。
- 模块与模板文档各有一份注册（同扩展名可分别指向不同实现）；未注册扩展名读写时以 InvalidSource 类错误显式失败，并列出受支持扩展名。

## 11. 编辑器形态和实施顺序

### P0：协议和样例冻结

- 确认 Authoring、Source、Runtime 三层职责；
- 确认事件、值引用和黑板作用域；
- 盘点当前项目实际使用的 Action、Condition、Event 和模板；
- 选取一个技能、一个 Buff、一个被动作为 golden examples；
- 冻结新 Source JSON Schema，不接入 Legacy Trigger JSON。

### P1：列表式生产闭环

- 模块资产管理；
- 触发器列表和完整头部字段；
- 事件目录；
- 强类型条件和行为；
- Local/Global/Payload/Context 引用；
- 基础校验；
- Source 和 Runtime 导出；
- Source JSON 双向同步与冲突检测；
- Undo/Redo 和批量校验。

当前进度：项目级 Event Catalog、Global Blackboard Catalog、Template Catalog、MOBA 初始目录和一键项目初始化已经落地；模块 Inspector 已形成列表式生产闭环，支持触发器导航、递归 Condition/Action 编辑、事件感知的 Payload/Blackboard ValueRef 选择器、模板引用和参数绑定、可跳转诊断、Undo/Redo，以及带冲突处理的 Source JSON 导入导出。Condition 与当前 MOBA PlanAction 已全部补齐强类型参数 Schema。模块内 ConditionGroup / ActionGroup 已支持 JSON 双向同步、嵌套引用、缺失/重复/循环校验、只读展开预览和复制为本地树。Authoring Schema 2.2 已具备 canonical Runtime export、独立 Template Asset、模板 Source JSON 双向同步、精确版本和参数绑定校验。Global Blackboard 初始化计划和项目级 Build Gate 也已接入 Runtime/MOBA 启动链。模块↔项目的双向成员登记已由 TriggerAuthoringProjectMembership 统一维护（修复了构建门禁模块清单无人写入的断点），Project 资产新增 Inspector 与模块创建/登记入口，一键初始化附带起始模块；约定字段（Phase/Scope/Schedule Mode/InterruptPolicy）改为受约束下拉并补齐 Cue/ExecutionControl 编辑；模板绑定等破坏性操作增加确认；诊断点击可定位到具体节点并高亮；描述符目录已清理为单源注册（消除中英文重复注册覆盖）。Source 编解码已抽为 ITriggerSourceCodec 注册表（按扩展名解析、JSON 默认、哈希基于 DOM 规范投影与格式无关，见 10.2），新增第二种格式只需注册实现并通过同一套 round-trip/哈希稳定性测试。Y3 式体验第一批已落地：三栏工作台窗口（Window/AbilityKit/Trigger Authoring Workspace，项目/模块树含未挂载模块警示 + 嵌入式模块编辑 + Source 同步与项目校验卡，含复制路径/打开目录入口）、可搜索节点浏览器（AdvancedDropdown：分类树 + 搜索 + 最近使用，替换节点选择的 GenericMenu）、触发器列表增强（搜索过滤、事件列、E/W 诊断角标、上移下移、右键菜单）、节点级重排（↑↓，走 Undo）与节点剪贴板复制/粘贴（带 marker 的 Source 形态文本，跨触发器/跨模块，根空位与子节点两处粘贴入口）。生产链第一批已落地：Project 资产新增 RuntimeOutputRoot（相对 Unity 工程根，MOBA 初始化默认指向 demo 的 Resources/ability/triggers）与一键 Runtime 导出（TriggerAuthoringProjectExport：先跑完整项目门禁，再按模块原子写出 {moduleId}.runtime.json，入口在 Project Inspector、右键菜单与工作台校验卡）；Golden Examples（技能/Buff/被动三个模块，Create Golden Example Modules 菜单落资产并登记）带联动验收测试（校验+canonical Runtime 编译全绿+引用搜索）；Source 外部变更/文件缺失在 Inspector 顶部横幅提示（Import/Dismiss，可关断、状态变化自动复位，工作台同步卡同步提示）；引用搜索 TriggerAuthoringReferenceFinder 覆盖事件/组/模板/全局黑板键，Inspector 内事件字段、组行、模板绑定三处 Refs 入口，结果在独立浮窗可 ping 定位。Golden 触发器 Id 已按模块分段唯一（技能 101/102、Buff 201、被动 301），满足项目级跨模块 TriggerId 聚合校验；golden 验收测试含端到端链路（项目一键导出到临时目录 → 逐文件 TriggerPlanJsonDatabase.LoadFromJson 加载 → 触发器数守恒）。新增 tools/run-unity-editmode-tests.ps1 一键跑 Unity EditMode 测试（要求工程未被其它 Unity 实例占用，产物落 local/Logs/）。模块编辑 UI 已抽取为共享绘制器 TriggerAuthoringModuleDrawer（纯 IMGUI 类，无 Editor 生命周期，Repaint 经事件回调宿主）：Module Inspector 是它的瘦宿主，工作台窗口直接持有同一绘制器实例（替代 Editor.CreateEditor 嵌入，切换模块经 SetAsset 复用编辑状态）。迁移面调查（只读）：demo 87 个旧 JSON 共 101 条触发器，条件/动作类型对新链描述符集映射覆盖率 100%（21 动作+9 条件零缺失），但 80/101 触发器 event 为空且字段命名/值形态不同——迁移为逐条重录（判定 event 归属需团队领域判断），非机械换名。

### P2：模板、组和展开预览

- Template Asset 和实例绑定；（已完成首版）
- ConditionGroup / ActionGroup；（已完成模块内版本）
- 展开预览；（已完成组引用版本）
- 版本校验；（已完成精确版本）
- 引用迁移。

### P3：可视化和调试

- 条件树和行为树视图；
- 触发器引用关系；
- 测试 Payload；
- Dry Run；
- Runtime Trace 预览；
- 黑板初始值和执行结果查看。

## 12. P0 验收标准

P0 设计进入实现前，至少应冻结以下决策：

1. Unity `ScriptableObject` 是主编辑数据源，Source JSON 支持显式反写。
2. Source JSON 是版本控制、审查和 AI 编辑格式。
3. Runtime Plan JSON 完全由编译器生成。
4. 第一阶段覆盖当前项目实际使用的事件、条件、行为和黑板类型。
5. 模板保留引用关系，编译时展开。
6. 新链路不兼容 Legacy Trigger JSON。

P0 设计完成后的第一批实现必须能够证明：

```text
编辑一个真实触发器
    -> 通过校验
    -> 导出 Source/Runtime JSON
    -> Runtime Loader 成功加载
    -> 触发器可以在测试上下文中执行
```

本设计确认后，下一步应先建立 Descriptor、Authoring DTO、Source DTO 和校验器的最小闭环，再扩展 Odin 界面，避免先堆 UI 后反复改数据模型。

## 13. Authoring v2 Runtime Plan 导出基线

Authoring v2 使用独立的 canonical exporter，不复用旧 `AbilityModuleSO` / `TriggerEditorConfig` 导出链。导出顺序固定为：

```text
Authoring Validate
    -> ConditionGroup / ActionGroup 确定性展开
    -> Condition 后缀表达式与 Action 调用编译
    -> Runtime DTO 序列化
    -> 临时文件原子替换
```

首版已覆盖：

- `all` / `any` / `not`、常量条件、数值比较、`has_buff`、`health_percent` 和当前项目上下文谓词；
- `seq` 与当前 MOBA 正式注册的 PlanAction；
- Number / Integer / Boolean / Entity / ObjectId 常量；
- String 常量经稳定字符串表转换；
- Payload、Context、TemplateParameter 与 Expression 数值引用；
- Global Blackboard 稳定 domain/key ID、默认值初始化和 Runtime resolver 注册；
- IntegerList 常量按运行时 indexed named-arg 约定展开；
- EventId、Phase、Priority、Scope、AllowExternal、CueId 和触发器 Template binding；
- disabled trigger 跳过统计、输出确定性和 Runtime Loader 加载验证。

canonical exporter 坚持 enabled trigger 全有或全无。以下配置在 Runtime 契约补齐前产生稳定诊断并阻止整个数据库输出：

- 非默认 `InterruptPriority`；
- 触发器级 Schedule；
- InterruptPolicy 与 StopPropagation 控制；
- Vector3 或动态 IntegerList 单值引用；
- 动态 String 引用；
- Local Blackboard 已进入 owner-aware Runtime 初始化契约；Module board 在同一 owner 内共享，Trigger board 在同一 owner 内隔离；
- 未在当前项目 Runtime PlanAction 集合注册的动作；
- Runtime 无映射的条件类型。

Action `Arity` 按正式 Runtime Source writer 的约定取具名参数数量的 `min(2, count)`，完整参数仍写入 `Args`。当前 Runtime `ActionCallPlanValidator` 对 `NamedArgs.Count == Arity` 的要求与该 writer 在参数超过两个时存在历史不一致；canonical exporter 暂不修改共享 Runtime 契约，由 Runtime 专项批次统一修正。

Runtime Plan JSON 是生成物，不参与 Source JSON 的反写与同步哈希。Template Asset 已复用本导出器的值引用编译规则，并在进入导出器前完成模板存在性、版本和参数绑定校验。

## 14. Authoring v2 Template Asset 基线

Authoring Schema `2.2` 引入独立 `TriggerAuthoringTemplateAsset` 和项目级 `TriggerAuthoringTemplateCatalogAsset`。模板资产保存稳定 `TemplateId`、`TemplateVersion`、事件、参数 Schema、Condition 和 Actions；模块实例只保存 `TemplateId + Version + Bindings`，不序列化 Unity GUID、实例 ID 或模板树副本。

模板使用独立 Source JSON 文档：

```text
schema + version + metadata + template
```

它与 Module Source JSON 采用相同的严格未知字段检查、内容哈希、冲突检测和原子写入。AI 可以直接编辑模板 JSON 并反写 Template Asset；Catalog 和 Unity 引用不进入 JSON。

首版模板契约：

- 模板版本采用精确字符串匹配，不隐式接受 SemVer 范围；
- 参数声明类型、必填、常量默认值和实例允许来源；
- 实例绑定覆盖模板默认值，先完成最终值合并再编译，避免 Runtime 字符串表残留死数据；
- 模板树通过 `TemplateParameter` 读取参数，未声明参数、类型不符和模块局部 Group 引用会阻断；
- 模板实例不能同时保存本地 Condition/Actions，不进行隐式树合并；
- 模板事件必须与实例 Trigger 事件一致；
- 首版模板不能引用另一个模板，因此模板循环依赖在数据结构上不可表达；后续若增加组合模板，必须先引入显式依赖图和循环诊断。

Runtime export 在校验通过后从 Catalog 解析模板资产，使用模板树替代实例本地树，再复用 canonical exporter 的 Condition、Action、ValueRef 和字符串表编译链。Runtime JSON 继续使用现有 Trigger `Template` binding 协议，不让共享 Triggering Runtime 依赖 Unity Authoring Asset。

Runtime Loader 的 fallback `Actions -> ExecutionRoot` 转换也必须在同一个 Template binding 作用域中执行；转换器使用保存/恢复 binding 状态的作用域，避免模板参数在 fallback root 中丢失或污染下一条 Trigger。

## 15. Project Build Gate 与 Global Blackboard Runtime 契约

`TriggerAuthoringProjectAsset` 显式保存本项目参与构建的 Module Asset 清单。构建校验不通过全工程隐式猜测模块归属；Module 和 Template 都必须反向引用同一个 Project，Catalog 和 Module 清单共同形成确定的构建输入。

项目校验覆盖：

- Event、Global Blackboard 和 Template Catalog 必填、空引用及重复稳定 ID；
- Global Blackboard domain、类型和常量默认值；
- Module/Template 的 Project 归属以及重复 ModuleId/TemplateId；
- 每个 Module 的 Authoring 校验和 canonical Runtime export；
- 多 Module Runtime 聚合、跨模块 TriggerId、字符串表与 Blackboard 初始化冲突；
- 聚合 JSON 由正式 `TriggerPlanJsonDatabase` 再加载一次。

校验可从 Project Asset 菜单或 `Tools/AbilityKit/Trigger Authoring/Validate All Projects` 执行，并通过 `IPreprocessBuildWithReport` 在 Player Build 前自动阻断错误。没有 Module 的 Project 只产生警告，便于先创建项目目录再逐步接入模块。

Runtime Plan JSON 增加 `Blackboards` 初始化段。每个 global domain 生成一个稳定 BoardId，每个 Catalog Key 生成稳定 KeyId，并携带支持类型的默认值。`TriggerPlanJsonDatabase` 在单文件、目录和聚合加载中保留该段，`InitializeBlackboards` 只接受可写 resolver；MOBA 的 Trigger Plan 启动阶段在所有文件合并后统一应用初始化计划。相同 board 可以确定性去重，默认值冲突在项目聚合时阻断。

Local Blackboard 同样进入 `Blackboards` 初始化段，但使用 `Scope=owner`，不会由世界级 `InitializeBlackboards` 物化。`OwnerBlackboardStore` 按 ownerKey 应用这些初始化模板，并以 Global resolver 作为只共享全局 board 的回退；`TRG2057` 仅阻断 Global Trigger 引用 Local Blackboard。

## 16. Owner-aware Local Blackboard Runtime 契约

Local Blackboard 已使用显式 owner resolver 接入 canonical Runtime Plan，不通过 thread-static 或隐式 current-owner 状态传递。Runtime JSON 继续使用 `Blackboards` 初始化段，并由 `Scope` 区分 `global` 与 `owner`；`InitializeBlackboards` 只初始化 Global，`ConfigureOwnerBlackboards` 只保存 Owner 初始化模板。

- Module Local BoardId：`local.module:<moduleId>`；同一 owner 内的同模块 Trigger 共享。
- Trigger Local BoardId：`local.trigger:<moduleId>:<triggerId>`；同一 owner 内按 Trigger 隔离。
- 同名 key 优先解析 Trigger 声明，否则回退 Module 声明。
- Local Blackboard 只允许 owner-bound Trigger 引用；Global Trigger 会由 `TRG2057` 阻断。
- `OwnerBlackboardStore` 为每个非零 ownerKey 创建独立 resolver，并以世界 Global resolver 作为解析回退；释放 owner 不会清理 Global 状态。
- MOBA 使用真实 owner context id 作为 ownerKey，不降级为 actorId。
- `ApplyTriggers` 创建并复用 owner 状态；`Stop`、`OnDeinit` 和 `Dispose` 都释放 owner 状态；释放后重新 Apply 会从默认值重建。
- Local 默认值必须是 `DictionaryBlackboard` 可表示的常量；不支持的类型和默认值在 canonical export 阶段阻断。

Blackboard 写入已使用独立 `BlackboardTarget` 参数接入，不再把写目标当作普通 `NumericValueRef` 提前解引用。`set_num_var` 与 `add_num_var` 继续提供明确的数值写入语义；泛型 `set_var` 已成为正式 Runtime Action，并通过 typed value union 支持 Number/Integer、Boolean、String 常量。Authoring 会校验 target/value 类型一致，Runtime 初始化 Schema 同时执行 `CanRead/CanWrite` 和目标类型校验，手工修改 Runtime JSON 不能绕过权限。Runtime、Export JSON 与 Readable JSON 均保留 Bool/String 显式类型，支持 JSON 双向反写；动态 Bool/String 的 Payload、Blackboard、Expression 引用仍明确拒绝，等待后续 typed reference 契约。

Blackboard Snapshot 第一批已落地为 Owner store 的显式契约：`BlackboardSnapshot` 使用版本号和 typed entries 保存 Local Blackboard 的 Int/Bool/Float/Double/String 当前值，支持 JSON 序列化与反序列化；Global fallback 不进入 Owner snapshot。恢复要求 owner 仍存在、版本匹配、board/key/type 完整匹配，并在全部校验通过后才写回。

MOBA 已提供 opt-in `MobaOwnerBlackboardRollbackProvider`（registry key `10008`）：仅当宿主注册了 `IOwnerBlackboardSnapshotStore` 时加入 Rollback registry，以 owner 集合快照作为 payload；导入时 active owner 集合不一致会失败，不会隐式创建或释放 owner。Provider 同时要求 Passive、Continuous、Trigger subscription 暴露的 ownerKey 并集与快照 owner 集合完全一致；任一方向缺失都会在导出/导入阶段主动失败。

该 provider 已能力门控接入 MOBA Rollback registry，但 owner 生命周期仍由各业务服务负责。回滚期间不能假设 Local Blackboard 会自动创建或恢复 Buff、Passive、Continuous 和 Trigger subscription；宿主必须在回滚前后按统一顺序重同步 owner 创建/销毁、触发器订阅和持续行为绑定，再恢复 Blackboard 值。Provider 实现 `IRollbackStatePreflightProvider`，由 `RollbackCoordinator` 在任何 provider 写入前完成 JSON、生命周期和 Blackboard schema 校验；失败会直接终止恢复，不进入部分恢复状态。生命周期协调完成前，需要回滚确定性的玩法仍不应依赖可变 Local Blackboard。
