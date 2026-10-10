# MOBA 标签规则与结果标志

## 行为门禁与可追溯预览

导表报告的 `diagnostics` 列出暂无静态消费者的持续模板，以及暂无授予源的 Active 事实标签。这些是待核对提示，不会自动启用 Planned 词汇。`python LubanConfig/Moba/export_pipeline.py --preview-template 10000001 --preview-tags State.SuperArmor` 可逐阶段查看 Required、Blocked 与规则组的合并结果。

`Production/Defines/strong_types.xml` 的 Luban `MobaTagBehaviorGate` enum 定义稳定的业务门禁名称与数值；`J-标签表.xlsx` 的“行为门禁”子表将 `Id` 声明为此 enum，并配置各门禁的 `BlockedFlagIds`。调试名称直接取枚举成员名。标签规则命中后得到结果位，门禁只对缓存结果位做掩码比较。战斗规则与 AI 直接引用 Luban 生成的 `moba_luban.MobaTagBehaviorGate`。`Targetable`、`Damageable`、`Cast`、`Stunned`、`Move`、`Attack` 和 `Controlled` 已覆盖现有消费点。强制位移仍使用自身持续对象的执行要求，不借用自主移动门禁。

伤害结算入口检查目标存在、存活及 `Damageable` 行为门禁；目标查询单独检查 `Targetable`。因此不可选取不会自动免疫已经命中的伤害。需要免伤时在持续模板授予对应事实，并由“标签结果映射”配置 `Damageable` 所阻断的结果位；伤害业务不比较标签名或结果枚举。以后增加新的伤害阻断条件，优先修改规则组与门禁映射；来源与目标关系、伤害类型等上下文条件仍由伤害管线处理。

导表时生成 `local/luban-export/tag-usage-report.json`，包含持续模板授予源、Buff/持续流程直接授予源、Excel 表对模板的引用、规则叶节点和组引用、规则到结果位映射、行为门禁与技能/持续对象执行阻断引用。`tagImpact` 按标签列出模板、直接授予源、规则组、结果位及行为门禁；`authorableGameplayFacts` 是编辑器可选的已发布 Active 战斗事实标签。用 `python LubanConfig/Moba/export_pipeline.py --impact-tag State.Stunned` 查询静态影响，用 `--preview-tags State.Stunned --explain-tags` 预览实际规则命中。报告只读，Excel 仍是编辑源。

Battle Debug 的“活跃对象”面板保留持续对象授予标签及规则判定；Actor 级有效标签、来源、结果位与门禁统一在“标签”页查看。选中 Actor 后点击“抓取”，编辑器才从该 Actor 的活跃持续对象与标签服务取得脱离运行时的快照；窗口重绘不查询标签。勾选“包含规则树”可额外展开 `All/Any/Not/GroupRef` 的命中结果及叶子来源。快照记录世界、Actor、帧和标签 bundle 版本，暂停或结束的对象不贡献有效标签；设置 A 后再次抓取 B 可比较标签、结果位、门禁与来源变化，也可导出或导入 JSON。离线诊断 Artifact 仅有旧 `ActorTagStore` 采样，不能当作正式持续对象标签快照；离线查看请导入已保存的快照。抓取和完整规则展开只用于调试，不加入战斗 Tick。

运行时有效标签按 Actor 缓存。持续对象激活、暂停、恢复、结束时标脏；活跃 Buff 刷新其需求后显式标脏并协调依赖对象。下一次查询重建标签与结果位。协调循环对有持续或移除条件的对象使用无诊断分配的快判；调试界面仍可获取完整判定。规则查询在服务内部直接读取缓存容器，公开的 `GetEffectiveTags` 仍返回副本。`MobaTagResultTests.Multi_actor_gate_and_rule_reads_reuse_cached_tags` 用 100 个 Actor、每个 1000 轮门禁及规则读取记录耗时、线程分配和重建次数；回归断言以缓存重建次数为准，耗时不是跨机器验收阈值。跨进程传输只能使用已发布定义 ID，不能传进程本地 `GameplayTag.Value`。

## 权威源与发布

`LubanConfig/Moba/Production/Datas/J-标签表.xlsx` 是标签配置的唯一编辑源。旧 `Configs/MobaTagAuthoring` JSON 源已移除，运行时 JSON 和 `.bytes` 都由 Luban 导出。`J-效果表.xlsx` 的 Buff、持续流程以及 Pipeline 使用模板 ID 引用。模板 ID `10000001` 和 `10010001` 已改为通用语义，因现有 Trigger Authoring 与 Pipeline 引用而保留历史编号；新增通用模板使用 `3001-3099`。

运行 `python LubanConfig/Moba/export_pipeline.py --apply` 发布 Unity/Console 产物，再运行 `--check-published` 检查一致性。`migrations/migrate_tag_workbook.py`、`migrations/finish_tag_workbook.py` 及 `migrations/plan_tag_catalog.py` 记录一次性迁移，日常修改只编辑工作簿。

发布同时生成 `luban/moba/tag_bundle_manifest.json`。其中 `bundleVersion` 是 7 张标签表以及 `buffs`、`continuous_processes`、`skills` 的 `.bytes` 文件 SHA-256 清单的整体 SHA-256；运行时注册标签目录前逐表校验，并拒绝在同一进程加载不同 bundle。`CatalogVersion` 仅标识标签定义目录，规则或授予源变化仍会改变 `BundleVersion`。这两个版本不能用整数 `ConfigVersion` 代替。多人房间以 `tagBundleVersion` 标签交换版本，并在入战资源加载前完成比较；服务端独立验证发布包的边界见下文。

## 表结构

正式多人房间创建时从已发布包验证并写入 `tagBundleVersion` 房间标签；服务端将该版本纳入启动 manifest hash，客户端从权威快照读取并在资源加载前与本地已验证 bundle 比较，缺失或不一致时拒绝入战。服务端当前使用创建者提交的版本，尚未独立加载本地标签 bundle；如果服务端运行依赖标签配置的权威战斗模拟，还需要比较服务端本地发布包版本。

| 子 sheet | 字段与职责 |
|---|---|
| 标签定义 | `Id/Name/Namespace/DisplayName/ParentId/Kind/Status`；`Name` 是 ASCII 点分运行时路径，`DisplayName` 是中文显示名，父级显式引用标签 ID；`Kind=Category/Fact` 区分分类与可授予事实，`Status=Active/Planned` 区分已接入与仅预置 |
| 标签规则组 | `Id/Name/RootNodeId`；命名、复用条件 |
| 标签规则节点 | `Id/GroupId/ParentId/Order/Operator/TagId/Exact/RefGroupId`；一行一个节点 |
| 结果标志定义 | `Id/Key/BitIndex/Name`；只为代码消费的结果生成 `MobaTagResultFlags : ulong` |
| 标签结果映射 | `Id/FlagId/RuleGroupId`；命中规则时设置结果位，同一结果多个映射按 OR 合并 |
| 行为门禁 | `Id/Name/BlockedFlagIds`；`Id` 是 Luban `MobaTagBehaviorGate` enum，Excel 填成员名，阻断位引用结果标志 ID |
| 持续标签模板 | 简单条件填各阶段 Required/Blocked 标签 ID；复合条件引用规则组；`ApplicationTagIds` 授予事实标签，`ExecutionBlockedFlagIds` 设置执行门禁 |

持续模板的激活、移除、持续条件是三种**不同的生命周期判断**。激活 Required 必须全部存在、Blocked 任一存在即拒绝启动；移除 Required 全部存在且 Blocked 全部不存在时结束对象；持续 Required 全部存在且 Blocked 全部不存在时保持生效，否则暂停，条件恢复后继续。同一阶段的平铺标签与规则组按 AND 合并。简单的“不能有 A 或 B”填 `BlockedTagIds`；需要跨标签的 OR、Not 或组复用时才填规则组。没有激活或持续条件表示不额外限制；没有移除条件表示不自动移除。`ApplicationTagIds` 是对象生效时授予的事实，并非判断条件，所以纯授予模板的三个条件阶段留空是正确配置。

`Tag` 节点填 `TagId`；`All/Any/Not` 通过同组 `ParentId` 组织子节点；`GroupRef` 填 `RefGroupId`，复用另一个规则组。每组必须有一个根；`Not` 必须有且仅有一个子节点。兄弟节点 `Order` 唯一。`Exact=false` 时后代标签满足祖先条件；`true` 时只匹配显式相同标签。空标签集合可以满足取反条件。

导出器把模板及 Buff/持续流程直接授予的标签与模板激活、移除、持续条件交叉索引到 `local/luban-export/tag-usage-report.json` 的 `templateDependencies`。同一载体授予自身激活前置所需标签会被拒绝；含负向边的持续条件循环也会被拒绝，避免暂停/恢复反复振荡。`positiveDependencyCycles` 列出纯正向环及可用的外部授予源，作者须确认实际启动路径。报告的 `templateConsumers` 同时列出 Buff、技能 Pipeline 和 Trigger 动作引用，修改共用模板前应查看完整消费者列表。

## 通用持续模板目录

持续模板表达**持续对象在生命周期内授予的事实、激活条件与执行门禁**。它不是 Buff 定义，不包含持续时间、伤害、击飞轨迹、层数、驱散策略或目标选择。以下目录按王者荣耀类 MOBA 的常见机制分层预置；这里只承诺现有标签和结果规则的语义，不把竞品技能数值或特例当作本项目已实现的行为。

| 模板 ID | 通用语义 | 授予标签/条件 | 使用边界 |
|---|---|---|---|
| `10000001` | 普通强制位移的目标可受控条件 | 激活与持续的 Blocked 标签均为 `State.ControlImmune`、`State.SuperArmor`；激活拒绝，持续出现时暂停 | 供对敌位移动作使用，不用于施法者自己的跳跃 |
| `10010001` | 施法期间霸体 | `State.SuperArmor` | 廉颇现有三条 Pipeline 引用；新技能也可复用，施法执行门禁仍取技能表 |
| `3001-3004` | 控制免疫、霸体且免伤、单独免伤、不可选取 | `State.ControlImmune`、`State.SuperArmor`、`State.Invulnerable`、`State.Untargetable` 的相应组合 | 只授予事实；伤害、目标筛选和控制服务读取已有结果规则 |
| `3005-3011` | 眩晕、禁锢、沉默、恐惧、魅惑、睡眠、失能 | 各自授予一个已激活事实；激活前阻断 `State.ControlImmune`、`State.SuperArmor` | 只对普通控制适用；施加后获得免疫是否驱散，由具体机制配置，不默认暂停或移除 |
| `3012` | 压制标记 | `State.Suppressed`；无通用抵抗条件 | 压制与普通霸体交互必须在具体控制动作验收后配置，不应复用普通控制门禁 |
| `3013` | 减速标记 | `Debuff.Slow` | 嬴政两个减速 Buff 已引用此模板；减速幅度继续由修饰器定义 |
| `3014` | 不可选取且免伤 | `State.Untargetable`、`State.Invulnerable` | 仅用于确需两个结果同时存在的生命周期 |
| `3015` | 自主移动执行门禁 | 不授予标签；执行阻断引用结果标志 `CannotMove` | 预备给位移表的自主移动持续对象；强制位移不可引用 |

`3005-3011` 不会自动生成控制 Buff；内容作者仍需让 Buff 或运动持续对象引用模板，配置持续时间与行为。模板被引用后，目标获得标签，结果映射计算禁攻、禁施法和禁移动等位。持续对象结束或暂停时，标签及结果位按多来源规则恢复。若一个 Buff 使用模板授予某标签，不再同时在 Buff 的 `TagIds` 写相同标签。`3001-3015` 是通用目录；英雄特殊组合只在无法由通用模板表达时新增，并记录来源机制。

`Planned` 词汇覆盖引导、蓄力、冲刺、跳跃、隐身、护盾、强化普攻、投射物免疫、击飞、击退、牵引、冰冻、石化、缴械、致盲、重伤和持续伤害等方向。当前导表器禁止将这些词汇写入运行时模板。每项转为 `Active` 前须完成授予源、消费服务、抵抗/优先级规则、回滚和验收测试；例如隐身还需要可见性与选目标服务，击飞还需要运动生命周期和高度断言。模板目录可以继续按这些机制扩展，但不能让一行模板名称替代未完成的战斗能力。

模板规则组 ID 为 0 表示不配置该条件；持续模板不配置移除条件时不会因空条件自动移除。仅配置 `RemovalBlockedTagIds` 时，该标签不存在即触发移除；规则组条件与平铺必需/禁止标签条件共同生效。旧 `RemovalTagIds` 没有符合持续对象承载契约的授予者，生产行也未使用，已从正式表和运行时契约删除。MOBA 不向 Owner 级 `GameplayTagService` 写入战斗标签；同一标签的多来源叠加由活跃持续对象集合自然保留，最后一个来源结束时才撤销。

标签命名空间保持隔离：`GameplayState` 为活跃持续对象持有的战斗事实，`SkillSelection` 仅用于按需给技能做 AI/配置筛选。规则与持续模板不能使用选技分类标签。旧数字标签 `1/2/3` 和逐英雄、逐技能槽位的分类标签已退出生产表；召唤物和玩法表的旧 `Tags` 列必须留空，单位身份与玩法类型由专用字段表示。标签 `Name` 的每个层级必须有自己的定义行，`ParentId` 必须指向直接父级且命名空间相同；运行时层级匹配仍由 `GameplayTagManager` 按点分路径执行。

标签 ID 分段：`90` 是可选技能标签根 `Skill`，只在确有筛选需求时于 `2000-2999` 配置语义标签，不再按英雄或技能槽位分配；若只需指定一个技能，AI 直接使用 `requiredSkillId`。`1000` 为 `State`，`1040` 为 `Debuff`；已投入使用的战斗事实 ID 和名称保持不变，`1000-1099` 其余空洞及旧拼写同义项 ID 永不复用。新增 `State.*` 使用 `1100-1499`，新增 `Debuff.*` 使用 `1500-1999`。现有 `Debuff.Slow=1006` 是冻结的历史 ID，不代表负面效果占用状态分段。新增标签先选语义父级和稳定 ID，再决定是否授予与配置结果规则；结果 flag 名不是标签事实名。

## 战斗目录规划

预置目录参考王者荣耀常见的压制、击飞、缴械、隐身、护盾、强化普攻、持续伤害等英雄机制，按**可持续持有且可被其他系统查询的事实**分类。它是 MOBA 配置词汇表，不声明示例已实现这些机制。现有 `State.Stunned`、`State.Suppressed`、`State.ControlImmune`、`State.SuperArmor`、`State.Invulnerable`、`State.Untargetable` 和 `Debuff.Slow` 等是 `Active`，对应规则组决定其结果位；新增目录均为 `Planned`。新目录不为这些旧标签造同义项。

| ID 段 | 标签层级 | 用途与例子 |
|---|---|---|
| `1100-1119` | `State.Casting.*` | 引导、蓄力；只在对应施法 Pipeline 生命周期内授予 |
| `1120-1139` | `State.Movement.*` | 冲刺、跳跃、被迫位移；运动持续对象是授予源 |
| `1140-1159` | `State.Visibility.*` | 隐身、伪装、显形；显形与隐身的优先关系应由可见性服务决定 |
| `1160-1179` | `State.Defense.*` | 护盾、减伤、反伤；数值仍归 Buff、护盾或伤害服务 |
| `1180-1199` | `State.Attack.*` | 强化普攻、强化技能待触发；消耗时由对应持续对象结束标签 |
| `1200-1219` | `State.Immunity.*` | 投射物、减速、强制位移免疫；实际拦截仍需对应规则或服务 |
| `1220-1239` | `State.Survival.*` | 免死保护、复活阶段；死亡处理仍归生存服务 |
| `1500-1519` | `Debuff.Control.*` | 嘲讽、缴械、致盲、击飞、击退、牵引、冰冻、石化 |
| `1520-1539` | `Debuff.Movement.*` | 禁止位移；与既有 `Debuff.Slow` 区分 |
| `1540-1559` | `Debuff.DamageOverTime.*` | 灼烧、中毒、流血；伤害跳数由 Buff/触发器配置 |
| `1560-1579` | `Debuff.Vision.*` | 视野受限 |
| `1580-1599` | `Debuff.Healing.*` | 治疗降低、禁止治疗；治疗计算服务负责应用 |
| `1600-1619` | `Debuff.Defense.*` | 易伤、物理防御削弱、法术防御削弱；数值由修改器处理 |

| 王者荣耀机制参考 | 标签落点 | 接入时仍需完成的行为 |
|---|---|---|
| 庄周解控、花木兰霸体 | 复用 `State.ControlImmune`、`State.SuperArmor` | 明确不同控制类型的阻断范围与优先级 |
| 张良、东皇太一的压制 | 复用 `State.Suppressed` | 施法、移动、普攻门禁及提前结束的生命周期 |
| 王昭君冰冻、廉颇击飞 | `Debuff.Control.Frozen`、`Debuff.Control.Airborne` | 控制持续对象授予、解除与结果规则；当前廉颇击飞不授予预置标签 |
| 兰陵王隐身 | `State.Visibility.Invisible` | 可见性、选目标规则和显形交互 |
| 孙悟空强化普攻 | `State.Attack.EmpoweredBasic` | 下一次普攻的增伤及消耗标签时机 |
| 扁鹊叠毒 | `Debuff.DamageOverTime.Poisoned` | 层数、周期伤害、驱散和叠加规则 |
| 盾山格挡飞行物 | `State.Immunity.Projectile` 仅作为可查询事实 | 投射物碰撞几何、朝向和命中仲裁不能由标签代替 |

`Category` 节点只用于非精确层级查询，不能作为事实授予；`Planned` 项不能被规则、持续模板、Buff、技能等生产配置引用。使用预置标签前，先实现授予源与消费逻辑，补上验收测试，再将事实及其父分类切换为 `Active`。例如参考庄周的免控可沿用 `State.ControlImmune`，参考张良的压制可沿用 `State.Suppressed`，而兰陵王式隐身需补可见性与选目标规则后才授予 `State.Visibility.Invisible`。属性、阵营、实体类型、技能冷却、一次性命中事件和纯表现不进入标签目录。技能表的 `Tags` 默认为空；仅当多个技能需要同一 AI/配置筛选类别时，才在 `Skill.*` 下增加语义标签并逐项引用。

规则组 ID 与标签定义 ID 是不同的表内身份：`2001-2999` 用于可复用结果规则，`10000000-19999999` 用于技能或模板专用规则，现有 `10000001` 保持不变。规则节点 ID 为 `GroupId × 100 + 组内序号`（1-99），父子关系仍由 `GroupId/ParentId` 决定，不把节点 ID 当作标签 ID。规则组 `Name` 使用中文业务名称；结果标志 `Key` 保留稳定英文程序名，`Name` 使用中文。

## 运行时边界

正式配置解析未知标签名时直接报错。Buff 已激活持续对象刷新需求后会显式标脏标签缓存；持续规则协调按角色排队处理生命周期回调中的嵌套请求。`MobaGameplayTagCatalog.CatalogVersion` 是按定义 ID、名称、父级、命名空间、类别和状态计算的目录指纹；同一进程不能加载两份不同版本的 MOBA 目录。稳定配置 ID 可通过 `TryGetDefinitionId` 查询；进程内的 `GameplayTag.Value/NetIndex` 不能直接作为网络协议 ID，跨进程使用前还须在连接握手中校验完整 `BundleVersion`。

`MobaTagWorkbookRuntime` 从二进制加载并编译规则组；标签字符串仅在加载与编译边界解析。MOBA 的标签事实只由活跃 `IContinuous` 的 `ApplicationTags` 投影，Buff、位移和技能 Pipeline 通过持续对象或桥接对象参与。`MobaEffectiveTagQueryService` 合并这些标签并缓存结果位；持续对象激活、暂停、恢复、结束及角色移除会使缓存失效。一个持续对象结束后，其他对象仍持有的标签及其结果位保持有效。查询返回标签容器副本，外部不能直接修改缓存。

每个 MOBA 技能的施法 Pipeline 均绑定 `SkillPipelineContinuousRuntime`，即使没有标签模板也有完整生命周期；`PipelineContinuousTagTemplateId=0` 只表示不额外携带标签和条件。配置了模板但解析或激活失败时，施法失败，不再静默跳过标签约束。前置施法阶段有配置时也使用同一桥接机制。

规则在世界初始化时编译。重新导表后需要重新启动战斗世界，运行中的世界不会自动重载规则。增量切换配置组时，未选为二进制的表默认读取 Luban 发布 JSON，与 Excel 保持同一数据来源。

战斗侧不可移动、控制免疫、不可选取、免疫伤害等判定读取位掩码。技能施放门禁从每条技能的配置掩码读取：当前普通攻击行使用 `CannotAttack` 结果位，其他主动技能行使用 `CannotCast` 结果位；沉默允许普攻，硬控同时阻断两者。这些位的选择不再由施法代码按技能类型写死。生命、阵营等非标签事实仍由对应领域服务判断。当前缓存重算仍需遍历映射规则，但连续热路径查询只读取缓存并执行位运算；这里没有声明经过基准测量的整体性能提升。

生产标签结果服务初始化必须提供 `ITextAssetLoader` 和已发布标签二进制表；缺失时明确报错。向战斗规则服务传入的有效标签服务必须实现 `IMobaTagResultQueryService`。独立测试仍可创建不带标签服务的战斗规则实例，只检查生命、阵营等事实。

默认禁攻组复用硬控组，并包含恐惧与睡眠；沉默单独影响禁施法。嬴政的两份减速 Buff 在效果表引用通用减速模板 `3013`，由模板授予 `Debuff.Slow` 供驱散动作查询；该标记本身不产生控制结果位。通用行为查询未提供结果服务时门禁失败；生产脑查询读取缓存位，`HasTag` 查询同一持续对象标签投影。旧的无前缀、大小写混用及下划线同义标签没有生产授予源，已从定义和规则节点中清除；不在代码里维护业务别名。标签变化的表现事件由持续对象生命周期前后的有效标签差量产生；表现 Cue 的添加与移除共用稳定键，初始化时已有活跃标签只记录快照，不重播添加事件。

结果位是派生缓存，不作为另一份可编辑状态，也不允许调用方直接设置。已发布 `Key/BitIndex` 不能删除、改名或重新分配；新增结果使用未占用 bit，范围为 0-63。`Key` 以大写英文字母开头，不使用保留成员 `None`。导出器检查 bit 与引用，运行时再检查生成枚举与二进制一致。标签内部 Id/NetIndex 仍依赖进程目录，不是跨进程协议身份。

Buff 的直接标签与所引模板授予标签合并；持续流程也可在模板基础上追加直接标签。位移动作只允许填写 `continuous_process_id` 或 `continuous_tag_template_id` 之一；选择过程时，标签模板由过程表引用。非零模板 ID 无法解析时运行时拒绝该动作，不能退化为空需求。模板注册表向每次调用返回独立需求对象，修改某个实例不会污染后续实例。

## 持续行为执行门禁

`持续标签模板` 的 `ExecutionBlockedFlagIds` 引用 `结果标志定义.Id`，逗号分隔，留空表示不设置执行阻断。发布器校验引用并编译为结果位掩码；运行时由持续对象持有该模板需求，统一执行入口按拥有者的有效标签结果位进行一次位运算。业务持续对象不比较具体标签，也不引用结果枚举。激活、移除和持续生效条件由各阶段的平铺 Required/Blocked 标签与可选规则组共同判断；执行阻断只冻结该对象的执行推进，不结束对象，也不撤销它授予的标签。

技能 Pipeline 使用相同门禁检查自身更新时间轴。Buff 与周期行为在阻断帧跳过计时和周期触发；位移及投射物发射的外部源不依赖持续管理器的 Tick，因此命中阻断位时结束对应持续对象，由其生命周期回调停止运动源或发射器。强制位移模板不配置“不能移动”，以免禁足误挡击飞。其他使用持续标签模板的行为可逐条配置自己的阻断结果位。

`J-技能表.xlsx` 的 `技能.ExecutionBlockedFlagIds` 是每个技能的通用高频执行门禁，引用结果标志定义的 ID；Luban 发布时校验并编译为 `ExecutionBlockedResultMask`。普通攻击当前引用结果标志 ID `8`，其余主动技能引用 ID `3`，保持原有控制效果范围。施法前及世界帧中的运行技能检查该掩码；Pipeline 持续对象也合并该掩码，在直接推进时间轴时阻断执行。通用施法霸体模板 `10010001` 仍被廉颇 Pipeline 引用，但不再重复配置 ID `3`。新增技能需显式填写所需门禁；空列表表示没有标签结果位门禁。生命与配置存在性等非标签条件仍由代码检查。位移表迁移方案见 `Docs/MobaMotionConfigPlan.md`。

运行中的施法 Pipeline 遇到配置的执行阻断位时暂停时间轴推进，阻断解除后继续；死亡或角色无效仍取消技能。施法开始前的门禁保持拒绝启动语义。需要打断已开始技能的控制效果应使用明确的中断动作。

## 校验与测试

Trigger Authoring 的 `source_target_tag_rules` 条件分别引用来源和目标规则组 ID，至少填写一侧；两侧都填写时必须同时命中。规则组从 Luban `tag_rule_groups` 目录选择，导出聚合时只接受已发布的常量 ID。来源和目标 Actor 都从事件上下文解析，实体类型仍使用独立实体条件判断。运行时只查询对应 Actor 的活跃持续对象标签，不使用 Owner 级可写标签服务。

Trigger Authoring 的 `remove_buff` 使用标签定义 ID 选择 Buff 的授予事实，目录只展示已发布的 Active 战斗事实。旧 `remove_slow` 专用动作已退出编辑入口；已有资源仍按迁移工具检查后发布。

规则预览使用当前 Excel 经 Luban 导出的规则树，例如 `python LubanConfig/Moba/export_pipeline.py --preview-rule 2004 --preview-tags State.Stunned`。输出展开后的 `All/Any/Not/Tag` 树和命中结果；可与 `--check-published` 一起运行以核对 Excel 与发布产物。Battle Debug 的“活跃对象”面板显示持续对象授予的标签、来源、当前与最近一次规则判定以及阻断原因。

`MobaEffectiveTagQueryService.Metrics` 记录缓存重建、结果位读取、规则读取、重建时扫描的持续对象数、求值的结果映射数和重建耗时 tick。`GetEffectiveTags` 返回可修改的副本；只需判断标签时使用 `HasTag` 或 `HasAnyTag`，它们直接查询内部缓存，不创建副本。重复读取结果位只做缓存查询和位运算；携带标签的持续对象生命周期变化或显式调用 `RefreshTagRequirements` 会使对应 Actor 的缓存失效，下一次查询重建。指标用于定位热点与回归，耗时不是确定性战斗状态，也不作为跨进程协议字段。跨进程传输标签应使用定义 ID，并先完成 `BundleVersion` 握手，不能发送进程本地的 `GameplayTag.Value`。

导出器检查标签/组/节点/模板/映射 ID、命名空间、父节点归属、唯一根、节点孤岛、循环组引用、重复兄弟顺序、叶字段、Not 子节点数量、规则深度和展开后节点预算，以及稳定结果位。深度上限为 16 层，展开后不超过 64 个节点。

`python -m unittest discover -s LubanConfig/Moba -p "test_*.py"` 覆盖异常引用、组复用、规则环和结果位；`MobaLubanConfigPipelineTests` 检查二进制模板与 JSON 基线等价；`MobaTagResultTests` 覆盖层级条件、标记标签、结果位、多持续对象叠加、激活/暂停/恢复/结束的缓存刷新、表现 Cue 稳定键和初始化快照、AI 标签查询、外部容器修改隔离、无模板技能 Pipeline 桥接，以及沉默、硬控、霸体和免伤门禁。`GameplayTagManager` 对并行世界加载提供同步，目录并发回归由 `TagManagerTests` 覆盖。技能修改仍需运行六英雄冒烟和受影响英雄的 Unity 验收。

```powershell
python -m unittest discover -s LubanConfig/Moba -p "test_*.py"
python LubanConfig/Moba/export_pipeline.py --check-published
dotnet test src/AbilityKit.Demo.Moba.Tests/AbilityKit.Demo.Moba.Tests.csproj --filter "FullyQualifiedName~MobaTagResultTests|FullyQualifiedName~MobaLubanConfigPipelineTests|FullyQualifiedName~BattleScenarioRealSkillTests"
```
