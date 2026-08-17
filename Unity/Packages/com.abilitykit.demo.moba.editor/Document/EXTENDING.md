# MOBA 战斗诊断扩展开发指南

> 适用范围：Diagnostics Core、MOBA Runtime 诊断服务、MOBA Editor Battle Debug
>
> 最后更新：2026-07-19

## 架构链路

新增诊断能力应遵循以下单向链路：

```text
Runtime fact
  -> Producer / Sampler
  -> narrow write port
  -> bounded Store
  -> narrow read port
  -> IBattleDiagnosticReadOnlySession
  -> Editor ViewModel
  -> IBattleDebugPanel
```

依赖方向：

```text
Diagnostics Core <- MOBA Runtime
Diagnostics Core <- MOBA Editor
MOBA Runtime     <- MOBA Editor
```

Diagnostics Core 必须保持纯 C# 和 `noEngineReferences`。Runtime 不得引用 Editor。Editor 不得把活动 Runtime 对象作为诊断事实来源。

## 选择扩展类型

### 新增事件

适用于已经发生的离散事实，例如命中、结束、失败、回滚或异常。

优先复用：

- `BattleDiagnosticEvent`
- `MobaBattleDiagnosticEventDraft`
- `IMobaBattleDiagnosticEventSink`
- Event Ring Store
- `QueryEvents`

只有事件需要稳定结构化字段且 Summary 无法可靠承载机器消费时，才扩展强类型 `BattleDiagnosticEventPayload`。Payload 必须有固定 Kind、显式 schema version、纯值类型字段和向后兼容规则。

### 新增 Actor 当前状态

适用于每次采样可替换的当前快照，例如 Cooldown、资源、装备或技能运行时摘要。

标准组成：

1. 平台无关不可变 DTO。
2. 窄只读 Store 接口。
3. 窄写入 Store 接口或明确的快照提交端口。
4. latest-only Core Store。
5. 独立 Store revision。
6. Runtime 采样投影。
7. Local Session 动态 capability 和查询路由。
8. Editor ViewModel 与 Panel。

如果需求需要查询历史帧，不应直接复制 latest-only Store；应先设计保留范围、容量、索引、淘汰和查询一致性。

### 新增 Trace 或图查询

必须从真实 Registry 或稳定图 Store 导出。禁止从事件 Summary 文本重建父子关系。节点身份、Root、Parent、创建帧、结束状态和淘汰语义必须显式建模。

## Core DTO 规则

DTO 应满足：

- `readonly struct` 或不可变类型。
- 仅使用 BCL 和 Diagnostics Core 自身类型。
- 不引用 Unity、Entitas、GameplayTags、Ability Runtime 或 MOBA Runtime 类型。
- 使用稳定 ID，不持有活动对象引用。
- 在构造时校验非法 ID、跨 Scope/Frame、负数和非有限数值。
- 明确表达字段是否适用，不把 Runtime 哨兵值泄漏到 Core。
- 实现值相等性和稳定哈希。

名称等易变展示数据应在采样时固化；名称缺失时 UI 回退稳定 ID，不在 Editor 重新查询 Runtime 注册表。

## Store 规则

### 写入不变量

快照替换应先完整校验，再一次提交：

- Store Scope 与 DTO Scope 一致。
- DTO Frame 与快照 Frame 一致。
- Actor 项必须对应已捕获 Actor。
- 稳定实例键不得重复。
- 输入集合防御性复制。
- 失败提交不改变旧快照和 revision。
- Freeze 时拒绝写入。

不要在遍历过程中边校验边修改 Store。单 Actor Runtime 投影也应先完整构造临时结果，再追加到批次输出。

### 查询语义

latest-only Actor Store 使用统一语义：

- `frame == 0`：查询最新快照。
- Store 无快照：`NotProduced`。
- 非当前帧：`NotCaptured`。
- Actor 未捕获：`NotCaptured`。
- Actor 已捕获但集合为空：`Empty`。
- 能力不存在：`Unsupported`。

Event 和 Trace 等历史 Store 应显式定义 `Evicted`、`Truncated`、分页和 revision 行为。

### 修订号

每个独立数据面使用独立单调 revision。只有可观察数据变化时才推进。不要用 Event revision 代替 Actor State revision，也不要为方便 UI 刷新而合并无关 revision。

新增只读查询时，应同步扩展：

- Store 接口的 `Revision`。
- `IBattleDiagnosticReadOnlySession` 对应 revision 属性。
- Local Session 路由。
- ViewModel 缓存键。
- revision 独立性测试。

## Runtime Producer 与 Sampler

Producer 应挂接在唯一、真实且稳定的业务事实发生点：

- 成功事件只在业务提交成功后采集。
- 结束事件在上下文被清理前捕获必要字段。
- 不从空报告、缺失回调或 UI 状态推断事实。
- 诊断失败不得中断战斗主流程。
- 可选 Sink 缺失时静默跳过。
- 关闭通道时不写 Store，也不消耗 Sequence。

映射逻辑应尽量抽为纯静态函数，以稳定原语或只读上下文为输入，便于不构造完整 World 的聚焦测试。

Sampler 应：

- 在确定的系统阶段运行。
- 使用 Registry 或正式 Resolver 枚举活动对象。
- 将 Runtime 类型转换为 Core DTO。
- 对单 Actor 投影失败进行隔离，同时正确报告整批提交结果。
- 规范化 Runtime 的 NaN、Infinity、负时间和哨兵值。
- 不在缺少求值上下文时执行可能改变语义的动态计算。

## 窄端口与 World DI

按所有权拆分接口：

- Producer 只获得写入 Sink。
- Session 只获得只读 Store。
- Sampler 只获得状态写端口。
- 管理工具才获得 Freeze/Clear 等控制端口。

如果一个 scoped 实现暴露多个接口，确认 World DI 不会按接口类型创建多个实例。使用共享适配端口将各窄接口转发到同一个 scoped 实例，并测试跨端口 revision、Sequence、Freeze 和 Clear 的共享性。

新增 Store 后检查：

1. Runtime 服务注册。
2. 窄读写端口是否指向同一实例。
3. Local Session 最长可解析构造器是否收到新端口。
4. Store Scope 是否与 Session Scope 一致。
5. Capture Control 是否应覆盖新 Store。

## Session 与 Capability

Capability 表示当前 Session 的真实可用表面，不表示未来规划或内部存在某个类。

声明 capability 前必须同时满足：

- 对应查询契约已稳定。
- Session 能路由到真实同 Scope 数据源。
- 不支持、未生产和空结果语义明确。
- 至少有聚焦契约测试。

可选 Store 缺失时，Session 不声明对应 capability，查询返回 `Unsupported`。不要捕获 `NotImplementedException` 来判断能力。

`AllLocal` 只是枚举组合便利项，不应作为 Local Session 默认能力声明。

## 编辑器 ViewModel

ViewModel 负责：

- 调用只读 Session。
- 保存不可变查询结果。
- 把 Query Status 转换为清晰状态消息。
- 管理过滤、选择和缓存。
- 提供 Panel 所需的展示投影或复制文本。

ViewModel 不应依赖 `UnityEditor`，以便在 EditMode 测试中直接验证。

### 缓存键

缓存键应覆盖所有影响查询结果的输入：

- Session Scope。
- 对应 Store revision。
- ActorId、RootContextId 或其他稳定选择 ID。
- Frame。
- 所有过滤、分页和搜索条件。

聚合 ViewModel 应包含所有相关 revision，不包含无关 revision。缓存也必须保存 `Unavailable` 和 `Empty`，避免每次 Repaint 重复查询。

`InvalidateCache()` 只清除 ViewModel 缓存，不修改 Store；Panel 需要同时请求窗口 Repaint。

## 编辑器面板

新 Panel 实现 `IBattleDebugPanel`：

- `Name` 使用稳定、简短的标签。
- `Order` 与现有面板保持有意排序。
- `IsVisible` 只处理展示条件，不执行查询。
- `Draw` 通过 `BattleDebugDiagnosticSessionResolver` 获取 Session。
- 查询前检查 capability。
- 使用稳定选择 ID，不持有活动 `IUnitFacade`。
- `Unavailable`、`Empty` 和 `Error` 使用不同空态。
- Panel 只绘制，不承担 Store、采样或业务映射。

Panel Registry 通过反射自动发现面板，无需手工注册。新增类型后应验证程序集可加载、构造器可创建且 Order 没有非预期冲突。

## 测试要求

### Core DTO 与 Store

至少覆盖：

- 构造不变量和值相等性。
- 正常替换和防御性复制。
- 跨 Scope/Frame 拒绝。
- 重复稳定键拒绝。
- 失败提交原子性。
- `NotProduced`、`NotCaptured`、`Empty`。
- Freeze、Clear 和 revision。

### 运行时

至少覆盖：

- DTO/Draft 字段映射。
- 缺失可选上下文时的回退。
- 非有限和哨兵值规范化。
- Sink/Store 流转。
- 关闭通道不消耗 Sequence。
- World DI 生命周期和窄端口共享。

### Session 与 Editor

至少覆盖：

- 动态 capability。
- 无端口时 `Unsupported`。
- 跨 Scope Store 拒绝。
- 查询路由和 revision 独立性。
- ViewModel 完整缓存键。
- `Empty` 与不可用状态投影。

测试文件应进入 `AbilityKit.Demo.Moba.Diagnostics.Core.Tests` 程序集，并同步 Unity `.meta` 和生成项目验证。

## 文档同步

完成扩展时同步更新：

- `CURRENT-CAPABILITIES.md`：当前能力、Producer、面板和限制。
- `TROUBLESHOOTING.md`：新增状态、故障模式和定位方法。
- `TESTING.md`：新增测试入口或验证命令。
- `IMPLEMENTATION-HISTORY.md`：记录本批目的、边界和实际验证结果。
- 架构设计：仅当长期契约、边界或决策变化时修改，不追加重复操作说明。

## 变更完成清单

- [ ] 真实数据源和所有权已明确。
- [ ] Core DTO 无引擎和 Runtime 引用。
- [ ] Store 不变量、可用性和 revision 已定义。
- [ ] Runtime 映射不影响战斗主流程。
- [ ] Session 只声明真实 capability。
- [ ] ViewModel 缓存键覆盖全部输入。
- [ ] Panel 不读取活动 Runtime 对象。
- [ ] 聚焦测试与范围化构建已执行。
- [ ] 新文件 `.meta` GUID 唯一。
- [ ] 当前能力和实施历史已更新。
