# MOBA 逻辑层与表现层稳定化清单

本清单关注铺量前的架构契约，而非具体英雄、技能或副本玩法。判断是否完成时，以启动失败、重连、回滚、多世界并存和退出重入的实际行为为准；类拆分和参数数量只是辅助信号。

## 本轮已收紧

| 范围 | 问题 | 当前处理 |
| --- | --- | --- |
| 表现层会话启动 | `DefaultBattleLogicSessionRegistry.Start` 在会话构造成功前发布调试 facade，失败后留下无会话的全局入口 | 构造成功后再发布；远端传输参数在分配帧流前校验 |
| 逻辑世界装配 | `MobaGeneratedWorldSystemManifest.Install` 在生成清单为空时静默返回 | 改为启动失败，避免无系统的战斗世界继续运行 |

## 原始优先级

### P0：状态正确性

1. **按世界类型校验回滚覆盖。** `MobaRollbackRegistryBuilder.Create` 根据可解析服务有条件地注册 Provider，甚至在无世界服务时返回空注册表。先区分大厅与战斗世界，声明战斗世界必需的 Provider key、依赖及版本；在首次捕获前验证实际集合，缺失时阻止预测或恢复启动。验收需覆盖缺失单个核心服务、缺失生成 Provider、完整对局回滚及恢复后重放一致性。
2. **会话构造与销毁成为完整事务。** `BattleLogicSession` 同时建立运行时、帧流、传输和客户端，构造中后段失败没有统一的反向释放；`Dispose` 也未声明重复调用契约。将这些资源交给单一 owner，明确外部传入 transport 的所有权，按逆序回收并聚合清理异常。覆盖客户端创建、自动连接、建世界及事件订阅失败后的资源状态。
3. **恢复导入避免半提交。** `BattleAuthoritativeWorldRecoveryPort` 在导入 Actor 后才检查失败数，随后还执行 HFSM 与预测基线重设。明确 importer 的预检与提交边界；任何失败都应保持旧世界或进入明确的不可继续状态。验收需注入第 N 个 Actor/HFSM 导入失败并验证世界与帧号一致。

### P1：所有权与组合边界

4. **结束 `BattleSessionFeature` 的半迁移状态。** `BattleSessionRuntime` 声称持有会话状态，但 `SessionModuleRuntime` 仍通过 `_owner` 转发计划、上下文、网络适配器和生命周期操作。逐项把可变状态移入 runtime，由模块只接收其所需的窄端口；feature 保留游戏 Flow 适配职责。迁移一个端口就删除对应转发，避免新增第二套入口。验收重点是重复入局、并行世界、启动失败及退出重入。
5. **生产路径消除 `Current` 单实例入口。** `BattleLogicSessionHost`、`BattleDebugFacadeProvider`、回放控制和输入诊断仍存在静态当前实例。正式消费者应按 battle scope/world id 查询，并让发布 lease 负责撤销；无 scope 的入口仅留给明确的开发调试兼容层。验证两个世界交替启动和退出时不会读到另一局状态。
6. **让运行模式对应明确实现。** `MobaBattleLogicRuntimeFactory.CreateRuntime` 用 `null` 表示 Remote，而 `BattleLogicSession` 在一个构造器中分支管理本地与远端。拆为本地运行时和远端连接的组合策略，公共会话只持有明确的 client/streams 契约。先固定 transport 所有权和停止顺序，再移动类型。

### P2：扩展接口与演进成本

7. **统一生成清单失效策略。** 世界系统已要求非空，但 Bootstrap Stage 的反射回退会记录单个构造异常后继续扫描；生成与反射路径应有相同的完整性验证和可诊断失败。可选扩展需声明是否允许缺席，不让异常等同于未配置。
8. **对回滚数据做显式版本迁移。** 持续效果回滚目前由 `BinaryWriter` 按字段顺序写入，并兼容旧版本；大构造器本身不是迁移动机。先将快照字段组织为命名条目、保留旧读路径，再用黄金字节和跨版本导入测试切换编码格式。不得直接替换为 MemoryPack 后改变现有快照协议。
9. **区分观测错误和确定性失败。** `MobaWorldSystemExecution.HandleException` 默认按 Recoverable 交给策略记录并继续。为会影响帧状态的系统定义可恢复条件与故障后行为；同一输入和故障下，客户端与服务端必须得到相同的停止或回滚结论。

顺序上先完成 P0 的数据与资源契约，再迁移 P1 的 owner，最后处理 P2 的编码和兼容入口。每批同时验收 .NET 场景与 Unity EditMode；Unity 编译或测试运行器故障不能算作通过。

## 本批落地记录

- P0：`battle` 与 `MobaBattle` 世界建回滚表时校验核心 Provider 集合；从 `IWorldRandom` 注册随机数状态，缺项直接阻止启动。会话构造失败和重复销毁按资源逆序释放，并明确外部 transport 在传入会话后由会话持有。权威快照先预检 Actor/HFSM，提交失败即销毁该代预测世界、阻断输入并要求完整基线。
- P1：会话模块的状态、网络适配器和 tick loop 直接由 `BattleSessionRuntime` 提供；feature 保留 Flow 回调。远端模式不再向本地 runtime factory 请求 `null` 哨兵。静态 `Current` 的消费者核对后仅剩编辑器和测试兼容路径，正式会话仍按 battle scope 查询。
- P2：生成 Stage 和反射回退均在构造失败时中止，并回退本轮已注册 Stage。持续效果回滚升级为带命名段的 v6 格式，保留 v2–v5 读取路径。帧状态系统异常默认停止模拟，不再记录后继续下一 Actor。
- 验收：相关 .NET 测试 98 项、回滚重放场景 26 项通过；Unity EditMode 会话资源 7/7、registry 2/2、权威恢复 12/12、启动清理 26/26、模块组合 4/4 通过。`moba-complete-battle-journey` 门禁通过（Console 13/13、Unity 1/1）。覆盖全场景的逐帧回滚重放一致性及全部 Unity 套件仍需独立验收。
