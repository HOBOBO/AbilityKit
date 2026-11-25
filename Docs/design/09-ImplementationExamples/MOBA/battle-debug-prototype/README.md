# MOBA Battle Debug Prototype

用于快速验证战斗调试工具信息架构的独立 Vue + TypeScript 原型。

原型覆盖战斗调试工作台的主要工作区：

- 角色检查：Actor 浏览、角色状态、属性、Buff、效果和标签入口
- 属性与来源：基础值/最终值/差值、逐属性修改器解释、最近采样曲线、采样间隔与预期值对照
- 施法与对象：施法运行时、运行时对象目录、活跃对象监视
- 事件调查：事件筛选/详情与 Trace 树、时间线、关联对象跳转
- 帧同步：总览、预测、回滚、对账、时间同步和网络缓冲
- 数据与回放：数据能力/完整度、诊断文件和逐帧回放
- Actor、Buff、事件、Trace、运行时对象和回放帧之间的上下文跳转
- 本地 Mock 数据，不依赖 Unity、Gateway 或后端服务；字段是界面验证用的假设模型

规划依据 Unity 当前 `BattleDebugWindow`、`BattleDebugAnalysisModule`、面板目录与现有工作区规划。原型只验证信息层级、筛选和导航，不代表每个展示字段都已进入 Live/Offline 诊断 DTO，也不改 Unity 运行时代码。

属性曲线和修改器详情依据当前 `BattleDebugAttributesPanel` 已有展示能力规划。曲线点、修改器数值及来源关系都是静态 Mock；原型中的记录开关和采样间隔仅演示交互，不会采集战斗数据。

Trigger 事件页按“事件匹配 → 触发预算 → 条件评估 → Plan 判定 → Action 执行”呈现单次触发链，并提供条件通过、条件失败短路两种 Mock 场景。Unity 当前结构化 Trigger 诊断载荷可表达 Budget、Conditions、Plan、Execution 阶段结果，以及 Trigger ID、上下文/来源类型、失败键/原因和预算计数；它没有逐条件表达式/实际值，也没有逐 Action 的开始、目标和结果明细。因此，原型中的条件行和 Action 行用于确认目标界面与后续采集需求，不能视为当前 Live/Offline DTO 已提供的数据。

高频事件通过 Trigger 聚合组查看，分组字段对应现有聚合载荷的 Trigger、上下文/来源、阶段、结果、明细码和失败键；次数、首末帧与样例原因也可由当前聚合结构表达。界面默认突出 `Failed` / `Blocked`，把 `Passed` 组收在“全部聚合”，避免按发生次数直接判严重性。代表事件跳转、组级失败键标题及平均每帧次数是原型交互/展示假设，当前聚合载荷没有显式 Sample Event ID 或频率指标。

当前运行时的 `Valuable` Trigger 过滤规则突出 Budget 阻断和 Plan/Execution 结果；Conditions 阶段即使是 `Failed` 也不会仅凭该规则被保留。接入实际数据时需要明确查询模式，避免把“问题优先”错误绑定到现有 `Valuable` 过滤后漏掉条件失败。

## 技能设计与流程解释

「技能流程工作台」是 `com.abilitykit.ability.explain` 解释能力与 MOBA 配置编辑工作流的网页原型入口。Ability Explain 当前是 Unity Editor 的解释框架：以实体 Provider 查询实体、由 Resolver 生成 Explain Forest，再通过 Issues、Actions、Discovery、Diff、Relation、上下文编辑器和详情扩展完成解释与导航。它提供模型和 Editor 扩展协议，不负责 MOBA 业务配置的真实解析、技能运行时执行、资产持久化或完整依赖图生成；包内 Mock Integration / Timeline Sample 也不等于已接入生产配置的可编辑时间轴。

原型以「能力总览」作为入口，其他视图按需深入，而非把技能压缩成单条施法管线：

- **能力总览**：技能实体向下连接 CastFlow、Trigger、Projectile / Area / Summon、Buff / Shield 和最终战斗影响；分支以稳定配置引用和触发事件连接，能快速看出某个效果来自哪个对象回调。
- **对象生命周期**：每种派生对象各自展示创建、运行、碰撞/行为、结束与诊断引用。Projectile、Summon、Area、Buff 等按对象类型展开不同阶段；未配置对象明确显示为空，不根据技能名推断。
- **强化与替换**：单独区分数值 Modifier、追加投射物、追加 Action、替换现有效果和新增 Summon 分支，展示变更来源、作用条件、基线差异及受影响子图。当前五种变体卡片是界面结构示例，不绑定到 Lux W。
- **施法流程**：只展示主动输入、资源、RulePlan、Commit 与 SkillFlow Phase。
- **时间轴**：呈现逻辑阶段持续时间与事件位置；表现轨道、VFX 与运行时对象生命周期分开查看。
- **Trigger Plan**：目标结构为预算、条件、计划决策和 Action 执行；当前 Trigger Authoring 源尚未由本页结构化读取，因此只展示已核对的 ID、名称、入口和用途，不臆造逐条条件或 Action 参数。
- **配置引用**：展示技能定义、SkillFlow、逻辑 Timeline、Trigger、派生对象和效果之间的来源链，支持后续通过 Resolver 投影可定位的来源引用。

当前示例选取真实配置 Lux W / `30070301`：SkillFlow 包含资源预留、RulePlan `900101011`、`cast` Commit 点与 5042 ms Timeline；逻辑 Timeline 在 250 ms 执行 Effect `30070301`。该 Trigger 会先给自身加盾并发射 `Projectile #32070301`；Projectile 配置包含速度 24、最大距离 11.5、5000 ms 生命周期、返程速度 24、命中回调 `30070311` 和退出回调 `30070312`。友方命中和成功返程分别产生护盾 `30070390`。护盾持续 2500 ms，技能等级基础值为 40/55/70/85/100，并引用次级属性系数。Lux W 没有召唤物或技能变体配置，因此页面只显示空分支；额外投射物、追加效果、替换效果和 Summon 的图示明确是通用设计样例。

网页中可修改的字段只用于试验信息组织和编辑器布局，保存/放弃仅作用于当前页面草稿，不写入 Unity Asset、Luban 表或 Trigger Authoring 文件。后续正式接入应由只读配置投影 / Explain Resolver 提供实体、阶段、节点稳定 ID、Issue、SourceRef 和导航目标；解释图以对象关系为主干，时间轴作为其中一个阶段视图，强化/替换通过差异子图投影。若需资产编辑，应另接经过校验的配置编辑/保存服务，并让解释树在保存后重新解析。运行时调试则复用实体、Trigger 与 Trace 的跨页跳转，不应把配置解释结果伪装为某一局战斗的运行时证据。

## 运行

在本目录执行：

```bash
npm install
npm run dev
```

浏览器打开 Vite 输出的本地地址，默认是 `http://127.0.0.1:5174`。

可以通过 `?module=attributes` 直接打开属性与来源模块，例如 `http://127.0.0.1:5174/?module=attributes`。
可以通过 `?workspace=authoring` 直接打开技能流程工作台。

```bash
npm run build
```

构建只验证原型工程，不写入 Unity 或 Gateway 的运行目录。
