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

### P2：模板和组

- Global Blackboard Catalog；
- Template Asset 和实例绑定；
- ConditionGroup / ActionGroup；
- 展开预览；
- 版本和引用迁移。

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
