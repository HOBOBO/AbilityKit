# HFSM 确定性内核与渐进迁移

> 文档类型：FrameworkCore canonical
> 事实基线：2026-08-21
> 当前阶段：P1 定义协议与 Legacy 语义对照已落地，旧消费者尚未迁移

## 一、裁定

`com.abilitykit.hfsm` 采用“自研运行时逐步接管、UnityHFSM 保持兼容”的演进路线，不继续把战斗确定性、配置协议和新编辑器能力直接堆入旧内核，也不进行一次性全量重写。

包内暂时存在两个明确边界：

| 边界 | 命名空间 | 定位 |
| --- | --- | --- |
| Legacy | `UnityHFSM` | 维持 Flow、Shooter、MOBA 和已有示例兼容；只接受缺陷修复和迁移所需适配 |
| Next | `AbilityKit.HFSM` | 新定义协议、确定性运行时、快照、观察契约和后续编辑器导出目标 |

“自研”指 AbilityKit 拥有数据协议、执行语义、确定性和版本演进，不要求重写可复用的 Unity 画布、布局、Inspector 控件。

## 二、P0 已实现架构

```text
Editor / Config Source                 Application package
        |                                      |
        v                                      v
HfsmDefinition + Validator ----> HfsmRuntimeBindings<TOwner>
        |                                      |
        +--------------> HfsmRuntime<TOwner> <-+
                               |
                  +------------+-------------+
                  |                          |
          HfsmRuntimeSnapshot        IHfsmRuntimeObserver
          rollback / replay          debugger / trace
```

### 2.1 Definition 是运行语义权威

`HfsmDefinition` 只保存运行所需数据：

- root machine 和分层 machine/state 关系；
- 稳定的 state/transition/trigger/binding ID；
- initial state、remember-last、exit approval；
- transition priority、from-any、force、定点最小时长；
- format version 和稳定 definition hash。

Definition 不保存 Unity 对象引用、CLR 类型名、节点坐标、缩放、分组和注释。编辑资产必须经过 Validate/Export 才能成为运行时输入。

运行时在构造时复制所有语义字段。之后修改 authoring 对象不会热修改正在执行的状态机；显式热更新需要未来独立的 migration 协议。

### 2.2 执行语义

P0 固定以下契约：

1. 父 machine 先判断转换，再驱动当前 active child machine。
2. 每个 machine 先检查 from-any，再检查当前 state 的局部转换。
3. 同组转换按 priority 降序、transition id ordinal 升序，配置列表顺序不参与结果。
4. 每个 machine 每次 Tick 最多执行一次转换；完成转换后执行新状态的 Tick。
5. Trigger 从 root 向 active leaf 传播，首个接受它的 machine 截止传播。
6. 普通转换受到 exit approval 约束；pending 期间只允许 force-immediate 转换抢占。
7. 层级退出按 leaf 到 root，进入按 root 到 leaf。
8. condition 必须无副作用且确定；业务回调异常使 runtime faulted，观察器异常被隔离。

延迟退出保留原版的帧内顺序：pending 状态先执行最后一次 Tick，再检查退出批准；若同帧批准，随后退出旧状态并进入目标状态，但目标状态要到下一帧才 Tick。force-immediate 仍在 Tick 前抢占，并 Tick 新状态。

这些规则是运行时协议。编辑器必须展示 priority、from-any、force 和 pending，不得用视觉连线顺序暗示另一套执行顺序。

### 2.3 时间与确定性

核心入口使用 `Tick(frame, Fixed64 time)`，不读取 `Time.time`、`Stopwatch` 或系统时钟。运行时计算 raw delta，并拒绝倒退时间和非递增帧号。

配置与快照中的时长使用 Q32.32 raw `long`。`Fixed64` 属性只是强类型视图，JSON/二进制协议不得经 float 往返。

定点时钟不是“整个战斗自动确定”。状态 behavior、condition 和 transition action 仍必须遵守：

- 稳定集合遍历顺序；
- 不使用未收敛的随机源和浮点累计；
- 不在 condition 中产生副作用；
- owner 中影响判断的数据必须进入战斗快照。

### 2.4 快照与恢复

`HfsmRuntimeSnapshot` 当前版本为 1，包含：

- definition hash、frame、time raw、initialized；
- 每个 machine 的 active/remembered/pending/active-since；
- 实现 `IHfsmStateSnapshotParticipant` 的状态载荷及载荷版本。

恢复先验证版本、定义哈希、完整 machine 集合、层级活跃关系、pending 合法性和全部状态载荷，再写入运行结构。恢复不调用 enter/exit，不回放副作用。

状态载荷参与者的 `ValidateSnapshot` 必须无副作用。任一参与者在 Restore 中抛出异常会使 runtime faulted；跨多个业务对象的事务回滚仍由上层 snapshot pipeline 负责。

## 三、扩展边界

| 扩展点 | 责任 | 约束 |
| --- | --- | --- |
| `IHfsmState<TOwner>` | enter/tick/exit/exit approval | 实例按 state 独占，不保存无法快照的确定性数据 |
| `IHfsmTransitionCondition<TOwner>` | 判断是否转换 | 纯函数语义，不修改 owner/blackboard |
| `IHfsmTransitionAction<TOwner>` | 转换前后业务动作 | 异常会 fault runtime；不可承担编辑器观察 |
| `IHfsmStateSnapshotParticipant` | 状态私有回滚数据 | raw 存储、显式 payload version、先验证后恢复 |
| `IHfsmRuntimeObserver` | debugger/trace/metrics | 只读；异常隔离；不得参与模拟决策 |

`HfsmRuntimeBindings<TOwner>` 以稳定 key 注册 factory，Definition 不出现程序集限定类型名。项目包拥有业务状态和条件，HFSM 核心不引用 MOBA、Shooter、Entity、动画或网络服务。

## 四、UnityHFSM 核心概念对照

`HfsmLegacyParityTests` 同时执行 Legacy 与 Next，不以文档推测代替行为证据。当前基线如下：

| 概念 | Legacy UnityHFSM | Next `AbilityKit.HFSM` | 裁定 |
| --- | --- | --- | --- |
| enter / transition-before / exit / enter / transition-after | 支持 | 支持 | 必须等价，差分测试覆盖 |
| 转换后 Tick 新状态 | 支持 | 支持 | 必须等价，差分测试覆盖 |
| from-any 先于 local | 支持 | 支持 | 必须等价，差分测试覆盖 |
| 父 machine 先于 active child 处理 Trigger/Tick | 支持 | 支持 | 必须等价，差分测试覆盖 |
| 延迟退出帧内顺序 | 旧状态 Tick 后批准 | 同顺序 | 必须等价，差分测试覆盖 |
| remember-last | 子 machine 重新进入时恢复 | 支持并进入快照 | 核心概念保留，Next 强化可恢复性 |
| force-immediate | 绕过 exit time | 绕过 approval，可抢占 pending | 核心概念保留 |
| 同组转换顺序 | 注册/列表顺序 | priority 降序 + ID ordinal | 有意增强；配置顺序不再是隐藏语义 |
| pending 被后续普通请求覆盖 | 后请求覆盖前请求 | 禁止覆盖，仅 force 可抢占 | 有意增强；避免同帧调用顺序改变结果 |
| ghost state 连锁 | 支持 | 暂不支持 | importer 明确报错，不静默展开 |
| vertical exit transition | 支持 | 当前 IR 无对应概念 | importer 明确报错，需单独设计层级出口协议 |
| restore | 仅结构指针、清 pending | 结构、pending、定点时间、状态载荷 | Next 强化；恢复不回放生命周期 |
| Unity 方法名/多态条件 JSON | 直接执行旧载荷 | 稳定 binding key | 必须显式映射，禁止从 CLR 名自动推导 |

差分测试的目标不是让 Next 永远复制 Legacy 的所有偶然行为。只要出现差异，必须归入“有意增强”或“暂不支持并诊断”，不得处于无人知晓的灰区。

## 五、Definition JSON 协议

`HfsmDefinitionJson` 是当前 Definition 文本协议权威：

- 固定 camelCase 字段和写出顺序，machine/state/transition 使用确定性排序；
- `minimumActiveDurationRaw` 直接写 Q32.32 raw `long`，不经过 float；
- 不启用 `TypeNameHandling`，协议中没有 CLR 类型元数据；
- 未知字段、重复字段、缺失字段和类型不匹配立即失败；
- Load 完成后强制执行 `HfsmDefinitionValidator`；
- format version 不匹配不会容错读取，未来升级必须先进入显式 migration。

`HfsmLegacyGraphImporter` 位于 Unity 层，Legacy 类型不会反向进入 Next Core。第一阶段仅转换可证明等价的层级、initial、remember-last、普通/from-any transition、priority 和 force。旧 state 方法/behavior、condition JSON 必须通过 `HfsmLegacyImportBindings` 映射为稳定 key；ghost state 与 vertical exit transition 返回带 code/source path 的 error，存在 error 时不返回 Definition。

## 六、后续阶段

### P1：数据协议与编辑器出口（进行中）

- 已增加不含 CLR 类型名的 JSON codec 和 golden fixture；下一格式版本出现时再增加 version migration。
- 已增加 `HfsmBindingAttribute`、metadata-only `HfsmBindingCatalog` 和 Editor 自动扫描目录；编辑器不实例化业务状态即可展示字段与校验错误。重复 key 会记录 `HFSMBIND001`，并阻断 Next 导出而不让编辑器崩溃。
- 已增加可版本控制的 `HfsmBindingCatalogAsset`。项目可从 Assets 菜单配置该清单；配置后 Inspector 和 Next exporter 优先使用资产，未配置时回退到程序集扫描。资产只保存稳定 key/展示元数据，不保存可执行对象，格式版本和重复/空 key 均有显式诊断。
- 已建立旧 `HfsmGraphAsset` 到 Definition 的保守 importer，并接入编辑器导出 UI；后续补 binding 诊断面板聚合视图。
- 已接入 Graph Inspector 的 Next binding/trigger/raw duration 字段，并将 Export 菜单区分 Next Runtime Definition 与 Legacy Archive。
- Definition -> JSON -> Definition、Graph -> Definition -> JSON round-trip 和未知 binding 阻断均有 EditMode/.NET 测试覆盖。

### P2：Legacy 适配与首个战斗消费者

- Graph importer 第一版已实现；Profile importer 仍待实现，新 Runtime 未反向依赖 `UnityHFSM`。
- characterization/differential tests 已锁定第一批共有语义，后续迁移每遇到旧概念必须补测试或诊断。
- 优先迁移 MOBA actor 状态机和 rollback provider，验证真实帧同步回滚。
- 对无法等价迁移的语义给出显式诊断，不做静默降级。

### P3：运行时调试和规模验证

- 建立弱引用 runtime registry、拉取式 observation snapshot 和历史事件环形缓冲。
- 编辑器显示 active path、pending、最近 transition、frame/time raw 和 definition hash。
- 增加 1k 状态/10k 转换的构建、Tick、快照性能与分配基线。
- 验证多 runtime、多 world、domain reload 和销毁后的注册中心清理。

### P4：扩大迁移并退役 Legacy

- 按风险依次迁移 Shooter Bot、Flow adapter、示例和 Unity Graph runtime。
- 删除公共消费者中的 `using UnityHFSM`，保留一个版本周期的兼容 facade。
- 完成许可证/NOTICE 审计和迁移发布说明后，才删除 vendored Legacy 源码。

## 七、当前证据与限制

| 能力 | 证据 | 当前结论 |
| --- | --- | --- |
| Definition/validator/hash/JSON/binding catalog | `.NET` 聚焦测试 | E3，覆盖非法引用、稳定哈希、golden、严格 Load、round-trip、metadata scan 和重复 key |
| Legacy 核心概念对照 | 双运行时 `.NET` 差分测试 | E3，首批等价语义与有意差异已锁定 |
| 层级执行/trigger/pending/force | `.NET` 聚焦测试 | E3，含原版延迟退出帧内顺序 |
| Fixed64 时间门槛 | raw 值测试 | E3，不依赖墙钟 |
| snapshot/restore/fault/observer | `.NET` 聚焦测试 | E3，状态载荷与失败边界已覆盖 |
| Unity Graph 导出新 IR | Unity EditMode 最近一次 42/42 | E3，等价子集导入、binding 校验、canonical JSON、版本化 catalog asset 和不支持语义诊断已覆盖；编辑器导出 UI 已接入 |
| 真实 MOBA/Shooter 消费 | 尚未迁移 | Legacy 仍是生产消费者 |
| 大规模性能/Unity 平台 | 尚未执行 | 不能宣称 production-ready |

当前目标仍是建立可演进且可验证的替换路径，不是宣布 Legacy 已退役或新编辑器已经完成。
