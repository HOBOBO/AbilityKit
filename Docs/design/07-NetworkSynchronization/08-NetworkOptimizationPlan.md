# 08 · 多人联网模块演进计划

> **文档类型：演进计划**
>
> **事实基线：2026-08-16**
>
> **维护规则：** 只保留当前架构裁决、未完成事项、验收条件和压缩后的历史摘要；过程日志不在本文持续追加。

## 1. 当前架构裁决

### 1.1 框架提供稳定装配，不统一玩法算法

网络同步的公共价值位于协议、连接、房间阶段、Profile、能力协商、数据面生命周期、可靠事件和诊断。预测、插值、回滚粒度、表现对账与玩法状态 schema 具有明显项目差异，继续由游戏注册 controller 与 codec。

`NetworkSyncSessionBuilder<TController,TContext>` 是当前同步装配主干：解析稳定 Profile，冻结 Catalog/options，校验本地能力，按 `Ignore`、`NegotiateWhenAvailable`、`Require` 处理远端声明，再解析项目 controller 并返回不可变 descriptor。它降低接入成本，但不把 MOBA 或 Shooter 的应用层抽象提升为框架默认算法。

### 1.2 Room 声明能力，客户端拒绝不兼容组合

服务端 `RoomNetworkSyncCapabilityResolver` 根据最终 template/profile 生成 metadata version 1 的 `SyncCapabilities`，并随 room commit、persistent state 与 wire snapshot 保存。客户端 binding 拒绝未知 metadata version、未知策略位与 Profile 不匹配；旧服务端空/0 metadata 进入显式 legacy fallback，严格策略则返回 `MissingRequired`。

能力协商是会话建立的一部分，不是运行中自动切换同步模型的机制。运行中更换 Profile 需要重新建立可验证的会话边界。

### 1.3 高层 facade 与阶段化 Flow 并存

`GatewayMultiplayerSession` 适合线性/最小 Room 流程，已被 `GatewayBattleClientHost.EnterAsync` 和 Console 路径采用。MOBA 的 hero-pick、loading、恢复与事件驱动交互继续使用 `RoomGatewaySessionFlow` 原子阶段；不以迁入 facade 作为统一性目标。

facade 是一次性 host：`Dispose` 不代替 `LeaveRoom`，重连应新建或由项目驱动 staged restore。两连接拓扑只允许 battle 数据面拥有 push 订阅，Room 控制连接必须关闭对应订阅。所有网络 session 的 `Tick` 使用真实墙钟 delta。

### 1.4 三套预测实现只收敛稳定接口

FrameSync rollback、Host prediction driver 与 Shooter 专用状态同步控制器解决的问题不同。当前只收敛以下公共面：

- Profile/能力描述和 session 创建失败语义；
- confirmed/predicted frame、回滚次数、baseline 状态等诊断指标；
- 输入历史、checkpoint 和 full-state baseline 的所有权；
- 失败后进入恢复、降级或拒绝入场的明确协议。

不强制统一具体算法，也不把示例专用应用层搬入框架包。

## 2. 当前优先级

| 优先级 | 事项 | 当前缺口 | 完成条件 |
|--------|------|----------|----------|
| P0 | 同步能力协商与版本兼容 | builder、Room 声明和 binding 已实现，仍需覆盖服务端声明到客户端 controller 的完整链路 | legacy、remote-declared、missing-required、未知版本/策略位、Profile 不匹配均有 E2E；descriptor 可进入诊断 artifact |
| P0 | 可靠事件 checkpoint/baseline/reconnect 所有权 | builder、store、flush/retry/circuit 已有；项目持久化、生命周期触发和重连 baseline 仍需按客户端闭环 | pause/quit/dispose、store 故障、circuit open、timeline 变化、baseline watermark 与双连接唯一订阅均有故障测试 |
| 已关闭 | `PredictionCoordinator` 时间线残留、同帧重演和浅复制 | 2026-08-17 已增加 store `Clear`、按帧批次 replay、Record/Get 双侧隔离和 `IStateSlotValueCloner`；移除分裂式旧预测接口 | StateSync `20/20` 覆盖 Reset、同帧/空帧、帧级事件、克隆策略与事务覆盖；后续只按新回归修正 |
| P1 | FrameTime 定点范围与完整确定性 | Q32.32 帧时钟和 rollback payload 已完成，但业务状态并未自动定点化 | 建立跨运行时输入回放与状态 hash 矩阵，逐项审计随机、容器顺序、物理和 codec；不得只测时钟 |
| P1 | 非 TCP 服务端闭环 | WebSocket canonical 注册存在但默认关闭；LiteNet/UDP server listener 未完成 | WebSocket 真实 Gateway/TLS/反代/断线 E2E；LiteNet listener、配置、协议和 Smoke |
| P2 | `DemoHarnessRunner` 迁出 runtime | 测试基础设施仍位于运行时包边界 | 独立 test-infra 包、消费者迁移、UPM 依赖审计与相关 gate 全部通过 |
| P2 | 预测接口收敛 | controller/driver 指标和恢复结果仍不完全一致 | 只抽稳定诊断与失败契约；不要求 FrameSync/StateSync 共用算法实现 |

## 3. 设计风险与禁止项

| 风险 | 约束 |
|------|------|
| 把协商写成算法 | Profile 只声明能力与策略；controller 仍由项目注册 |
| 静默兼容未知 metadata | 未知版本/策略位必须拒绝；只有明确 legacy policy 才允许无声明服务器 |
| 两连接重复订阅 | battle 数据面是唯一 push 订阅者，避免 observerKey last-writer-wins 抢绑 |
| checkpoint 与 baseline 分属不同 owner | 同一 session owner 负责游标恢复、持久化、flush、baseline 与重连切换 |
| 把 `Dispose` 当业务退出 | 明确 LeaveRoom、断开、store flush、handler 解绑和对象释放的调用顺序 |
| 把 Q32.32 时钟外推成全局确定性 | 只有通过状态 hash/回放矩阵的业务状态才能声明确定性范围 |
| 强制迁移 MOBA 到线性 facade | 复杂阶段保留项目状态机；复用原子 Flow、Host primitives 与配置装配即可 |
| 用局部测试替代 Smoke/gate | E3、E4、E5 分开记录，且 gate 频率以 workflow 为准 |

## 4. 验证矩阵

| 影响面 | 最低 E3 | E4/E5 入口 | 说明 |
|--------|---------|-------------|------|
| FrameSync/回滚/时钟 | `AbilityKit.World.FrameSync.Tests` | `core-stability` 与指定 FrameSync Smoke | 本轮 18/18；MOBA 默认 StateSync gate 不能证明 FrameSync 模板 |
| StateSync/快照 | `AbilityKit.World.StateSync.Tests`、`AbilityKit.World.Snapshot.Tests` | `core-stability`、项目 Smoke | 本轮 12/12、7/7 |
| Record/codec | `AbilityKit.Record.Tests` | Shooter replay artifact 与相关 gate | 本轮 23/23；artifact、codec、workflow 是不同证据层 |
| Profile/可靠事件 | `AbilityKit.Network.Sdk.Tests` | `network-sdk`、真实重连/持久化 Smoke | 本轮 96/96 |
| Room/facade/能力绑定 | `AbilityKit.Network.Room.Tests` | `network-sdk`、MOBA/Shooter/Console Smoke | 本轮 36/36 |
| Shooter 双连接 | Shooter focused tests | `shooter-fast`、integration、multiprocess 分层 | PR、Push、Schedule、Manual 触发频率必须分别核对 |

本轮未运行真实 Gateway、WebSocket、LiteNet、Shooter multiprocess 或 Unity PlayMode Smoke，因此不新增 E4/E5 通过声明。

## 5. 历史完成摘要

| 日期 | 已完成裁决或改动 | 保留意义 |
|------|------------------|----------|
| 2026-08-09 | 通用 battle 数据面迁入 `com.abilitykit.network.battle`，Shooter 采用两连接拓扑 | 控制面与数据面所有权分离 |
| 2026-08-10 | Console 收敛到统一 `NetworkTransport`；Gateway facade 获得真实消费者 | 线性流程具备参考入口，但不替代复杂状态机 |
| 2026-08-10 | 回滚捕获/ring buffer/restore 临时数组降低热路径分配 | 保留防御性 payload 拷贝和 detached snapshot 契约 |
| 2026-08-10 | 修复恢复路径重复订阅导致 push 绑定被 Room 连接抢占 | 确立 battle 数据面唯一订阅原则 |
| 2026-08-16 | 增加同步 Profile builder、Room 远端能力声明、可靠事件 session 与 Q32.32 FrameTime | 当前 P0/P1 转向全链兼容、恢复所有权和确定性范围验证 |

历史条目只说明设计演进，不自动代表当前分支的 E4/E5 状态；发布判断始终以当次 artifact 和 workflow 结果为准。

---

*文档版本：v3.0 | 最后更新：2026-08-16 | 文档类型：演进计划*
