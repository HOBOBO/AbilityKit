# MOBA 战斗诊断当前能力与限制

> 状态日期：2026-07-20
>
> 本文是当前实现状态的唯一事实入口。设计目标请查阅架构设计，历史批次请查阅实施历史。

## 状态标记

| 标记 | 含义 |
| --- | --- |
| 可用 | 本地 Play Mode 已有 Producer/Store/Session Query，且存在可消费入口 |
| 条件可用 | 代码已接入，但依赖当前 World 服务、真实事件来源或运行模式 |
| 仅契约 | Core 已定义能力位或 DTO，但尚无完整用户工作流 |
| 未实现 | 设计中存在，当前没有可用实现 |

## Session 与查询

当前统一只读入口是 `IBattleDiagnosticReadOnlySession`。本地实现按实际注入的 Store 动态声明 capability，不应使用 `BattleDiagnosticCapabilities.AllLocal` 推断当前会话支持全部能力。

| Capability | 查询 | Revision | 状态 | Editor 消费入口 |
| --- | --- | --- | --- | --- |
| `WorldState` | `QueryWorld` | `StateStoreRevision` | 可用 | 诊断状态 |
| `ActorState` | `QueryActors` | `StateStoreRevision` | 可用 | 总览、诊断状态 |
| `Events` | `QueryEvents` | `EventStoreRevision` | 可用 | 诊断事件 |
| `Trace` | `QueryTrace` | `TraceStoreRevision` | 条件可用 | Trace |
| `ActorAttributes` | `QueryActorAttributes`、`QueryActorAttributeModifiers` | `ActorAttributeStoreRevision` | 可用 | 属性 |
| `ActorBuffs` | `QueryActorBuffs` | `ActorBuffStoreRevision` | 可用 | Buff |
| `ActorTags` | `QueryActorTags` | `ActorTagStoreRevision` | 可用 | 标签、总览 |
| `ActorEffects` | `QueryActorEffects` | `ActorEffectStoreRevision` | 可用 | 效果、总览 |
| `SkillRuntime` | 无正式查询 | 无 | 仅能力位 | 无 |
| `FreezeCapture` | 不属于只读 Session 查询 | 无统一公开查询 | 仅内部控制能力 | 无正式 UI |
| `Clear` | 不属于只读 Session 查询 | 各 Store 独立推进 | 仅内部控制能力 | 无正式 UI |
| `PinTrace` | 无正式控制工作流 | 无 | 未实现 | 无 |
| `Export` | 无正式诊断导出工作流 | 无 | 未实现 | 无 |
| `SelfMetrics` | 无正式查询 | 无 | 未实现 | 无 |

`StoreRevision` 是 `EventStoreRevision` 的兼容别名。新代码应使用各数据面的独立 revision，避免无关 Store 更新导致重复查询。

## 状态快照语义

World、Actor、Attributes、Buffs、Tags 和 Effects 当前使用 latest-only 快照：

- 查询帧 `0` 表示最新快照。
- 指定帧仅在等于 Store 当前快照帧时可用。
- Store 尚未提交过快照时返回 `NotProduced`。
- Actor 不在当前快照，或请求的帧不是当前帧时返回 `NotCaptured`。
- Actor 存在但对应集合为空时返回正常 `Empty`，不等同于未采样。
- 各 Store revision 独立推进，不提供跨 Store 原子事务。

Overview 同时依赖 `ActorState | ActorTags | ActorEffects`，并以 Session Scope、三类 revision、ActorId 和 Frame 作为联合缓存键。某一项 capability 缺失时，Overview 不回退读取 Runtime 对象。

## Event Store

事件 Ring Store 当前具备：

- 固定容量和严格递增 Sequence。
- Scope 校验与有界淘汰。
- 按 Actor、Channel、结果和文本组合过滤。
- 基于 Store revision 的一致分页。
- 已丢弃 revision 的 `Evicted` 语义。
- Freeze、Clear 和采集通道开关的内部控制端口。
- 不可变查询结果和版本化强类型 Payload。

事件面板当前显示最多 200 条结果，支持选择事件后查看信封字段、Summary 和已知结构化 Payload，并可从事件一键打开对应 Trace。完整 Timeline、Bookmark 和导出尚未落地。

## Trace Store

Trace 面板按 Root Context ID 查询真实 Trace Registry 导出的树预序快照；既可手工输入根，也可由事件详情自动打开根并定位事件 Context。面板提供：

- 节点层级、状态、Actor、Config、起止帧和结束原因。
- 选中节点从根到当前节点的父链路径。
- 按 Kind、状态、结束原因、Context、Actor 和 Config 的不区分大小写搜索；结果保留命中节点的祖先路径。
- 在直接搜索命中之间循环前后导航，不把上下文祖先当作命中；程序化选择会将目标滚动回树视口。
- 分支折叠、保留当前选中父链的批量折叠和全部展开；搜索期间临时展开命中路径，清除搜索后恢复既有折叠状态。
- 当前树内的临时节点 Pin、返回 Pin 和 Pin 节点淘汰提示。
- 缺失 Parent 的孤儿节点标记，以及异常父链的环路防护。
- `Unsupported`、`NotProduced`、`Evicted` 和截断结果的显式状态提示。
- 以 Session Scope、`TraceStoreRevision` 和 Root Context ID 组成的查询缓存键。

Trace 数据仍受 Runtime Store 保留范围约束。Editor 临时 Pin 只是当前面板内的导航状态，不是 Session `PinTrace` 控制能力；面板仍不提供持久化 Pin、导出或修改 Trace 的能力。

## Producer 覆盖

| 事件来源 | 状态 | 说明 |
| --- | --- | --- |
| 技能生命周期 | 可用 | 开始、完成、失败、打断 |
| Damage | 可用 | 管线最终伤害与直接伤害 |
| Heal | 可用 | 直接治疗 |
| Buff 生命周期 | 可用 | 添加与移除 |
| Projectile 生命周期 | 可用 | 生成、命中、结束 |
| Summon 生命周期 | 可用 | 生成与结束 |
| TraceNode 生命周期 | 可用 | 创建与结束 |
| Area 生命周期 | 可用 | 生成与结束 |
| Effect 执行生命周期 | 可用 | 开始与结束 |
| Warning/Exception | 可用 | 受现有限流策略约束 |
| 同步状态哈希快照 | 条件可用 | 仅采集真实收到的权威状态哈希 |
| Snapshot Gap | 未实现 | 同步层尚未暴露可靠事实来源 |
| Rollback/Replay 完成 | 未实现 | 不从空对账报告推断事件 |
| Full Snapshot 请求/应用 | 未实现 | 等待同步控制器正式事件 |

## Editor 工作区与面板边界

主窗口使用稳定 Actor ID 保存选择，实体列表重建、排序或过滤不会把选择静默切换到其他 Actor。实体工具栏显示可见/总实体数，支持清除过滤；左栏支持按 ID 跳转、在当前可见实体间循环前后选择和清除选择。面板分为 `Actor` 和 `Diagnostics` 两个工作区，二级面板通过下拉框选择；拥有大型列表或树的面板自行管理滚动，避免窗口外层滚动嵌套。实体栏支持拖拽调整宽度，栏宽、工作区和各工作区面板索引通过 EditorPrefs 持久化；Actor 选择属于会话状态，不跨窗口持久化。周期刷新仅在过滤后的 Actor ID 序列变化时替换实体列表快照，并可通过窗口“自动刷新”开关暂停轮询；该开关不等同于 Diagnostics Freeze，不停止采集，也不修改 Store。

以下详情面板已经通过只读 Diagnostics Session 消费不可变 DTO：

- 总览
- 属性
- 标签
- 效果
- Buff
- 诊断事件
- Trace
- 诊断状态

当前主窗口仍有活动对象依赖：

- 通过 `IBattleDebugFacade` 枚举实体。
- 通过活动 Facade 解析当前选择。
- 左侧实体列表通过 `IUnitFacade` 显示 Tag/Effect 简要计数。
- 帧同步系列面板使用既有帧同步调试接口，不属于上述 Actor Store DTO 化链路。

因此，当前准确表述是“主要诊断详情面板已 DTO 化”，不是“整个 Battle Debug 窗口完全与活动 Runtime 对象解耦”。

## 环境支持

| 环境 | 状态 | 限制 |
| --- | --- | --- |
| 本地 Unity Play Mode | 可用 | 必须存在活动 `BattleLogicSession` 和对应 World Services |
| Unity Edit Mode 非运行状态 | 不可用 | 窗口仅显示进入 Play Mode 提示 |
| 远端权威服 | 未实现 | 没有连接、鉴权、协议和远端 Session Adapter 工作流 |
| 离线诊断文件 | 未实现 | 没有稳定文件 schema、加载器和离线 Session Adapter |
| Web/CLI 客户端 | 仅契约基础 | Core 可复用，但没有产品化客户端 |

## 已知限制

- Local Session 的 Scope 默认仍可能使用临时本地身份，稳定外部 Session/World/Epoch 配置入口尚未完整产品化。
- Actor 状态类 Store 不保留历史帧，无法查询任意过去帧。
- 强类型 Event Payload 当前只正式覆盖同步状态哈希；其他事件主要依赖稳定信封字段和 Summary。
- Freeze、Clear 和通道控制存在内部端口，但没有完整面板操作与权限模型。
- Session/Runtime 层的持久化 Pin Trace、诊断导出、自监控指标、远端和离线能力尚不可用；Editor 当前树内的临时导航 Pin 不改变此限制。
- 当前工具不会修改战斗状态，也不应通过 Editor 建立可变 Runtime 旁路。

## 更新规则

当能力发生变化时，应在同一变更中更新本文：

1. 修改 capability、查询、Store 或 Session 路由时，更新“Session 与查询”。
2. 新增或移除 Producer 时，更新“Producer 覆盖”。
3. 新增面板或迁移数据边界时，更新“Editor 面板边界”。
4. 接入远端、离线、导出或历史状态时，更新“环境支持”和“已知限制”。
5. 将状态日期更新为验证变更的日期。
