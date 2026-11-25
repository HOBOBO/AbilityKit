# 战斗分析工作区与承载形态规划

> 状态：设计评审稿。本文只确定产品边界、运行承载和界面方向，不进入运行时代码、编辑器代码或 Web 实现。必须等整体方案确认后再编码。

## 1. 结论先行

战斗分析平台应采用“共享分析基础、三个能力域、多个专用窗口和承载端”的结构：

```text
统一 Battle Analysis Workbench
  ├─ Offline Evidence   离线证据分析：只读 Artifact / Trace / 采样 / 录像索引
  ├─ Isolated Replay    隔离重演：独立逻辑世界按输入推进，可投影到预览画面
  └─ Runtime Control    运行时控制：连接真实战斗世界，执行受权限约束的观察/回滚

承载端
  ├─ Unity Editor Window：主工作台、配置/参数、当前运行连接、隔离重演预览
  ├─ Unity Preview Scene：只承载隔离 Replay 或采样表现的可视投影
  └─ Web Analyzer：只读产物浏览、比较、趋势和协作分享
```

统一的是会话、帧号、实体引用、Trace/事件关联、构建指纹和分析视图；不统一三类能力的世界生命周期、可写权限、恢复语义和数据来源。

推荐顺序是 **Unity Editor 先形成闭环，Web 后消费版本化产物，真实 Runtime Control 最后作为受控扩展**。不建议一开始让 Web、Editor 和实时运行时共用一套可写控制接口。

不建议把三个能力域长期收敛成一个巨大的窗口。它们可以共用会话和分析基础，但在窗口层按生命周期、权限和运行依赖拆开。现有 `BattleDebugWindow` 更适合作为入口/导航壳和兼容入口，逐步把具体内容分流到专用窗口，而不是继续向其中堆叠所有控制。

## 1.1 共享基础与窗口边界

### 应共享的基础结构

共享层只承载跨能力域稳定、低副作用的契约：

| 共享基础 | 职责 | 不负责 |
| --- | --- | --- |
| `AnalysisSession` / `WorldRef` | 标识会话、世界类型、构建和能力 | 创建或恢复任何逻辑世界 |
| `FrameRef` / `TimeCursor` | 帧号、时间、范围和游标同步 | 决定游标移动是否会驱动运行时 |
| `EntityRef` / `EventRef` / `TraceRef` | 稳定关联实体、事件、Trace 和源记录 | 猜测不存在的关联 |
| Artifact/Replay/Runtime Adapter | 将来源转换成只读查询和明确命令能力 | 把不同来源强行转换成同一状态格式 |
| Capability / AccessPolicy | 声明可观察、可重演、可恢复、可写、可分支能力 | 绕过权限或隐式升级权限 |
| Index / Cache / AttachmentStore | 索引、分块加载、缓存和附件 URI | 修改原始证据文件 |
| Selection / Bookmark / CompareState | 选中实体、书签、筛选、比较对齐和深链状态 | 持有运行时对象引用 |
| Projection contract | 将某个世界投影到画面或轻量几何 | 把 GameObject 当作逻辑状态权威 |
| Command/Result envelope | 命令目标、请求 ID、能力检查、结果和错误 | 默认允许 Runtime rollback |

共享基础应尽量是低依赖、可序列化、可测试的 DTO 和服务。它可以被 Unity Editor、独立 Runner 和 Web 复用，但不应引用 Unity Scene、当前 BattleContext 或具体英雄/技能执行对象。

### 应拆开的专用窗口

| 窗口 | 主要职责 | 世界与权限 | 首要用户任务 |
| --- | --- | --- | --- |
| **Evidence Explorer** 离线证据窗口 | Artifact、Trace、事件、状态采样、差异和报告 | Offline Evidence / Read-only | 解释已经发生的事实 |
| **Replay Lab** 隔离重演窗口 | 输入重演、逐帧推进、seek、逻辑状态检查和分支 | Isolated Replay / Executable，默认不影响生产战斗 | 验证目标帧的逻辑状态 |
| **Runtime Monitor** 实时运行监控窗口 | 当前帧、预测/确认、网络、诊断、导出和回滚统计 | Live Runtime / 默认 Read-only | 观察当前真实战斗 |
| **Runtime Control** 受控命令窗口 | 暂停、继续、请求回滚、导出和审计 | Live Runtime / Writable by explicit permission | 执行有副作用的运行时操作 |
| **Comparison** 比较窗口 | 两个或多个会话的帧、Trace、状态和指标差异 | 多来源 / Read-only | 说明差异及其证据 |

`Runtime Monitor` 和 `Runtime Control` 可以在同一 Unity Editor Window 中以两个明确的标签页出现，但仍要使用不同的命令模型和危险操作区；`Evidence Explorer`、`Replay Lab` 和 `Comparison` 则应保持可独立打开，避免离线分析窗口因为实时连接失败而不可用。

### 窗口之间如何协作

窗口之间传递不可变的 `AnalysisNavigation`：`sessionId`、`worldKind`、`frame`、`entityRef`、`traceRef`、筛选和比较上下文。典型跳转如下：

```text
Evidence Explorer 选择 Trace 节点
  -> 打开/激活 Replay Lab，带入 session + frame + entity + trace
  -> Replay Lab 精确重演并返回 StateEvidence / ReplayResult
  -> Comparison 对齐原始 Artifact 与 Replay 结果

Runtime Monitor 导出当前证据
  -> 生成新的 Artifact/FrameRecord manifest
  -> Evidence Explorer 以只读会话打开
```

跳转传递的是导航和数据引用，不是跨窗口共享可变的逻辑世界实例。Replay Lab 可持有自己的 Replay runtime；Runtime Monitor 可持有 Live adapter；两者不能互相替换全局 facade 或共享 `BattleContext`。

## 2. 三类能力的应用方式

### 2.1 Offline Evidence：离线证据分析

输入是 Artifact、Trace、FrameRecord、逐帧采样、录像索引和运行 manifest。它的目标是回答“发生了什么、证据在哪里、哪些字段发生了变化、两个运行有什么差异”。

适合：

- 技能 Trace 因果树、事件泳道、状态字段差异；
- 运行结果、诊断警告、性能指标和基线比较；
- 不同构建/配置的离线比较；
- 从异常事件定位到录像帧或 Replay 起点；
- 导出报告、书签和 Scenario 候选包。

不适合：

- 将 Artifact 快照恢复为可运行逻辑世界；
- 在浏览器或离线窗口中直接改变生产战斗；
- 用空白字段推断“没有发生变化”；
- 依赖 Unity 运行时对象才能读取基本证据。

Editor 和 Web 都应支持这一能力。Editor 适合开发者边看边跳转到本地 Replay；Web 适合团队共享、历史趋势、跨运行筛选和链接协作。

### 2.2 Isolated Replay：隔离重演

输入是完整或部分可验证的初始状态、按帧输入、Replay manifest、构建/配置指纹和可选逻辑检查点。它在独立的逻辑会话中推进，不修改当前生产战斗。

适合：

- 从 Trace/事件定位到目标帧并读取真实逻辑状态；
- 按帧播放、暂停、逐帧前进/后退和重演进度显示；
- 检查实体、Buff、Tag、Effect、HFSM/技能阶段和事件；
- 将 Replay 世界投影到一个专用可视场景；
- 从确定性输入分支导出新的 Scenario 候选。

隔离 Replay 的逻辑世界应由 Replay runtime 持有。Unity Scene 只负责投影实体、摄像机、调试标记和表现资源，不是逻辑状态的权威存储。

### 2.3 Runtime Control：真实运行时控制

输入是当前 PlayMode/客户端/测试运行时的连接句柄、能力声明、权限和诊断协议。它的目标是观察当前真实战斗，必要时执行明确的运行时动作，例如暂停、继续、请求回滚或导出当前证据。

适合：

- 查看当前真实战斗世界和实时诊断；
- 观察网络同步、预测、确认帧和回滚统计；
- 请求运行时生成 Artifact、Trace 或 FrameRecord；
- 在明确支持且获得权限时执行回滚并观察恢复结果。

它必须与离线分析和隔离 Replay 使用不同的状态标签。Runtime Control 具有可写性和副作用，不能被普通的时间轴拖动或 Web 查看动作隐式触发。

## 3. Unity Editor 的推荐工作区

### 3.1 共享入口加专用窗口

建议保留 `BattleDebugWindow` 作为现有入口的演进方向，但将它降为共享入口和导航壳。具体能力通过菜单、标签页或独立可停靠窗口打开：

```text
BattleDebugWindow（入口 / 最近会话 / 状态摘要）
  ├─ Evidence Explorer（Artifact / Trace / State / Compare）
  ├─ Replay Lab（Replay runtime / Preview Scene / 输入重演）
  ├─ Runtime Monitor（当前战斗 / 帧同步 / 诊断）
  └─ Runtime Control（暂停 / 导出 / 回滚，显式权限）
```

所有窗口共享顶部的会话身份条和统一导航格式，但每个窗口只显示与自身能力相关的控制。窗口可以打开多个分析会话，每个会话必须显示自己的 World 类型：`Offline Evidence`、`Isolated Replay` 或 `Live Runtime`。不要用单个全局静态“当前世界”让三个会话互相覆盖。

| 窗口 | 推荐布局 | 不应放入的内容 |
| --- | --- | --- |
| Evidence Explorer | 来源树 + Trace/事件/状态详情 + 只读时间轴 | 播放器内部逻辑、实时回滚按钮 |
| Replay Lab | 输入/回放控制 + 预览画布 + 实体状态 + 重演进度 | 生产战斗写入、任意配置编辑 |
| Runtime Monitor | 实时指标 + 当前实体 + 帧同步/回滚历史 + 导出 | 未确认的时间轴 seek、离线文件编辑 |
| Runtime Control | 命令队列 + 权限/确认 + 结果/审计 | 普通筛选、Trace 展开等低风险操作 |
| Comparison | 双会话时间轴 + 字段/Trace/指标差异 | 修改任一来源世界 |

### 3.2 参数与控制面板

参数面板应是分析会话的控制面，不是生产配置编辑器。建议分为四组：

| 参数组 | 内容 | 写入位置 |
| --- | --- | --- |
| 来源选择 | Artifact、Trace、FrameRecord、录像路径、运行连接 | 分析会话元数据 |
| 重演参数 | 世界预设、输入范围、目标帧、播放速度、检查点策略、随机/确定性校验 | Replay 会话配置或临时覆盖 |
| 观察参数 | 实体筛选、字段白名单、Trace 深度、采样显示、比较对齐规则 | 分析视图状态 |
| 运行时控制 | 暂停/继续、导出诊断、回滚请求、权限和确认信息 | Runtime Control 命令记录 |

英雄、技能、Buff、Effect、HFSM 和生产表仍由各自正式 Authoring 工具维护。分析窗口只显示引用、跳转和必要的临时 Scenario 参数，不复制生产定义，也不在共享分析器里按英雄加入执行特例。

### 3.3 是否需要额外运行一个场景

需要，但用途应严格限定为“表现投影宿主”，而不是第二个逻辑世界：

- `Offline Evidence`：默认不启动额外场景；如果有实体位置采样，可在分析画布中绘制轻量几何或静态预览。
- `Isolated Replay`：可创建独立 Preview Scene 或专用 Preview 层，加载摄像机、灯光、模型、动画、特效和调试标记，将 Replay runtime 的世界状态投影进去。
- `Live Runtime`：默认连接当前运行时的现有游戏视图；如需并排观察，可创建只读投影视图，但不得把实时世界复制成一个可写副本。

额外场景的生命周期应跟随分析会话：启动、切换、关闭和异常清理必须独立；不能清理 Replay 时顺带停止用户当前的真实战斗。预览场景中生成的 GameObject 只是视图资源，实体 ID、帧号和状态值来自对应世界适配器。

### 3.4 Editor 内三种窗口状态

| 状态 | 默认权限 | 时间轴行为 | 画面来源 |
| --- | --- | --- | --- |
| 离线证据 | Read-only | 移动观察游标 | Artifact/采样/录像或轻量绘制 |
| 隔离重演 | 可播放、可暂停、可分支 | seek 会重建/检查点恢复并按输入推进 | Preview Scene 投影 Replay 世界 |
| 当前运行时 | Read-only；控制动作单独授权 | 默认观察；回滚是独立命令 | 当前游戏视图或只读投影 |

## 4. 当前真实战斗的连接方向

### 4.1 连接方式

Editor 连接当前战斗应使用显式的 `RuntimeSessionAdapter` 概念，读取既有诊断 facade、FrameSync 观察、Replay 控制和导出接口。连接页面应展示：

- 会话 ID、World ID、客户端/服务端标识；
- 构建、配置、协议和采集器指纹；
- 当前帧、确认帧、预测帧和回滚窗口；
- 可读能力、可导出能力、可回滚能力和权限状态；
- 连接断开、数据延迟、采样缺口和最近一次命令结果。

连接默认是只读。暂停、导出、回滚等操作应经过单独的命令面板，显示命令目标、预期副作用、超时、结果和审计记录。

### 4.2 时间轴与真实运行时

对 Live Runtime，时间轴默认是观察游标，不是可任意拖动的控制器。只有当运行时明确声明了回滚范围、检查点、输入窗口和恢复结果验证时，才显示“请求回滚”操作；超出窗口时应禁用并解释原因。

运行时回滚成功后，平台应区分：

- 回滚前采集的证据；
- 回滚恢复后的状态；
- 回滚后重新推进产生的新分支；
- 与离线 Replay 的结果是否通过哈希/输入/构建指纹证明等价。

这四类数据不能覆盖写成一条时间线记录。

## 5. Unity Editor、独立工具与 Web 的职责

### 5.1 Unity Editor：开发者工作台

Unity Editor 是第一阶段的主承载端，适合需要代码、资源、运行时对象和场景投影同时存在的排查：

- 读本地 Artifact/Replay，直接定位源码、配置和资源引用；
- 启动隔离 Replay 和 Preview Scene；
- 连接当前 PlayMode 运行时；
- 查看真实 Game View 或并排调试投影；
- 跳转 BattleScenario、技能流程、状态机和配置 Authoring 工具。

Editor 界面可以密集、可停靠、保留调试按钮和开发者专用字段。它可以拥有高权限，但必须通过 World/Capability 标识防止误操作。

### 5.2 独立桌面/命令行：批处理与重型任务

独立进程适合长时间重演、批量比较、CI、生成 Artifact 和大量索引。它不应复制 Unity Editor 的所有交互，也不应默认负责渲染完整游戏画面。

建议把以下任务优先放入独立 Runner：

- 大范围输入重演和状态哈希校验；
- 批量 Scenario 执行和比较报告；
- Artifact 索引、Trace 聚合和录像分段生成；
- 为 Web 提供已完成的分析产物。

Unity Editor 通过 manifest、run ID 和附件 URI 打开这些结果，而不是等待一个长任务占用编辑器主线程。

### 5.3 Web：证据浏览与协作分析

Web 首期定位为只读分析器，不在浏览器里运行完整 MOBA 逻辑世界。它消费版本化的 `RunManifest`、`CaseSummary`、`TraceRecord`、Artifact 和录像/Replay 附件索引。

Web 适合：

- 会话列表、失败聚类、趋势和跨构建比较；
- Trace/事件/状态字段下钻；
- 通过 URL 分享帧、实体、Trace 节点和筛选条件；
- 团队书签、评论、报告和权限控制。

Web 不应首期承担：

- 直接加载 Unity 场景或项目程序集；
- 任意执行用户上传的输入流；
- 直接向真实战斗发送回滚/继续命令；
- 用前端近似计算替代服务端已验证的运行结果。

如果未来需要 Web 发起 Replay，应由后端 allow-list workflow 接收 `runId/caseId/targetFrame`，在隔离 Runner 中执行，完成后返回新的 manifest 和证据，不让浏览器直接成为战斗执行器。

## 6. Editor 与 Web 的界面样式分工

### 6.1 Editor 风格

- 深色、密集、可停靠，强调状态、能力和调试信息；
- 中央区域可以放 Replay 画面或 Preview Scene；
- 右侧 Inspector 展示运行时类型、对象引用、配置来源和原始 ID；
- 底部时间轴支持逐帧操作、输入注入进度和错误日志；
- Live Runtime 使用黄色/红色高风险操作区，并始终显示当前 World。

### 6.2 Web 风格

- 以会话列表、筛选栏、摘要卡、时间轴和详情抽屉为主；
- 默认先给出结论与证据覆盖率，再逐层展开 Trace/状态细节；
- 支持宽屏双会话比较和窄屏单列下钻；
- 大文件按需加载，时间轴和 Trace 分段虚拟化；
- 所有链接都带会话版本、帧号、实体 ID 和筛选条件；
- 不显示 Editor 专用对象引用，显示可理解的资源/构建/配置名称和稳定 ID。

### 6.3 不应强行统一的 UI

Editor 的 SceneView/Inspector/PlayMode 控件和 Web 的筛选/分享/报告控件不应共用一套像素级布局。两端应共用信息架构、术语、能力徽标、颜色语义、时间轴协议和详情字段，而不是共用相同控件。

## 7. 推荐的实现分期

### 阶段 A：冻结边界与契约

定义 `WorldKind`、`AccessMode`、`CapabilitySet`、`AnalysisSession`、`FrameRef`、`EntityRef` 和 `RunManifest`。明确 Artifact、FrameRecord、Replay 和 Runtime Adapter 的输入输出，不写 UI 执行逻辑。

### 阶段 B：Editor 离线工作台

把现有 `BattleDebugWindow` 组织成 Offline Evidence 工作区：导入 Artifact/Trace/Replay manifest、统一时间轴、Trace/状态/比较窗口、书签和数据完整性提示。此阶段不创建额外场景，不连接可写运行时。

### 阶段 C：隔离 Replay 与 Preview Scene

接入已有独立 Replay runtime，在分析会话内创建/销毁专用 Preview Scene。先支持单个 Replay 世界的投影、暂停、逐帧和精确 seek，再评估检查点与表现采样优化。

### 阶段 D：Live Runtime 只读连接

通过 RuntimeSessionAdapter 连接当前 PlayMode/测试运行时，显示实时帧、诊断、网络/回滚统计和导出按钮。所有连接断开、资源清理和并发会话必须经过隔离测试。

### 阶段 E：受控 Runtime Control

在运行时协议明确支持后，增加暂停/继续/回滚等命令。命令面与离线时间轴分离，加入权限、超时、审计、结果哈希和失败恢复。

### 阶段 F：Web 只读分析器

消费版本化运行产物，实现会话列表、趋势、Trace/Artifact 下钻、双会话比较和分享链接。Web 不依赖 Unity Editor，也不执行任意逻辑。

### 阶段 G：后端隔离 Replay（可选）

若团队需要浏览器发起重演，后端使用 allow-list Scenario/Workflow 启动隔离 Runner，产出新的 AnalysisSession。该能力必须和 Runtime Control 分开部署和授权。

## 8. 需要整体确认的方向

| 决策 | 推荐方案 | 需要确认的影响 |
| --- | --- | --- |
| 首期主入口 | Unity Editor 单一工作台，先离线分析 | 复用 `BattleDebugWindow`，不先做 Web 执行 |
| 额外场景 | 只为 Isolated Replay/采样表现创建 Preview Scene | 逻辑世界仍由 Replay runtime 持有 |
| 当前战斗连接 | 首期只读；Runtime Control 后置 | 避免误操作和共享上下文污染 |
| Web 定位 | 只读产物分析与协作 | 不加载 Unity，不直接控制战斗 |
| 长任务执行 | 独立 Runner/CI | Editor 不承担批量重演和大文件索引 |
| 时间轴语义 | Offline 观察、Replay seek、Runtime rollback 三种显式操作 | 不提供一个无歧义的全局 Seek |
| 参数修改 | 分析会话临时参数与正式 Authoring 工具分离 | 不在分析器里复制生产配置 |
| Web 发起 Replay | 后端 allow-list，可选后续阶段 | 需要任务队列、权限、资源隔离和产物回链 |

## 9. 设计验收标准

- 用户能明确知道当前查看的是离线证据、隔离 Replay 还是实时运行时。
- 额外 Preview Scene 被销毁时不会停止或改变当前真实战斗。
- 离线时间轴移动不会向任何运行时发送命令。
- Replay seek 失败、数据缺失或状态哈希不一致时，界面显示已确认帧和失败原因。
- Runtime Control 的每个可写动作都有能力声明、权限、确认、结果和审计记录。
- Editor、独立 Runner 和 Web 通过版本化 manifest、稳定 ID 和附件 URI 关联，不依赖共享静态对象。
- Web 关闭或不可用时，Editor 仍可完成本地离线分析；Editor 不可用时，Web 仍可浏览已完成产物。
- 没有为某个英雄、技能或资源 ID 在共享分析器、回放器或运行时控制链中增加特例行为。

配套窗口分层原型：[共享基础与专用窗口](diagrams/battle-analysis-window-split.svg)。
