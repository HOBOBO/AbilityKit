# Moba View Runtime 架构评估与优化优先级

## 范围与结论

基于当前工作区的 `com.abilitykit.demo.moba.view.runtime`、`com.abilitykit.network.*` 源码及 asmdef。工作区已有大量并行改动，下列判断以当前文件状态为准。

| 领域 | 当前状态 | 判断 |
| --- | --- | --- |
| 特效 | `BattleVfxManager` 汇集创建、跟随、寿命和清理；按 `vfxId` 分桶复用 GameObject；Cue 有独立的纯逻辑 registry | 常用能力具备，但资源归属、异常回收和池契约仍需收口 |
| 实体视图 | `BattleEntityFeature` 持有 ECS lookup/factory/query，`BattleViewHandleStore` 管理视图索引，`BattleViewLifecycleController` 负责清理 | 基本生命周期存在，逻辑实体、视图状态与 Unity 对象尚未形成完整的可替换边界 |
| 网络复用 | `network.runtime` 提供 transport/connection/dispatcher 抽象，SDK builder 组装，Room/Battle 分包，多个 transport 可选 | 通用连接层已可复用；MOBA 的网关编解码、房间业务和复制策略属于项目适配层，不应并入通用 SDK |
| 纯逻辑与平台 | Flow Core、Presentation Core/Adapter、Network Runtime asmdef 均禁用引擎引用 | 关键纯逻辑已拆分；View Runtime 主程序集仍包含 Unity、网络、会话、战斗和表现代码，拆分尚未完成 |

## P0：先保证生命周期和资源正确性

1. **特效池归还契约**：`BattleVfxGameObjectPool.Return(vfxId, instance)` 原来只检查目标桶是否存在，可能把实例归入错误的 `vfxId` 桶。现已统一四类池的租用实例跟踪与跨桶、重复归还校验；`Clear` 会处理仍在使用的实例。
2. **特效创建失败与半初始化清理**：`BattleVfxEntityFactory.TryCreateEntity` 在创建 GameObject 后才创建 ECS 子实体；`ViewVfxSubFeature.OnAttach` 分步创建 manager、node 和绑定。失败路径现会回滚已取得的资源，并覆盖 ECS 创建抛错、attach 中途失败等测试；`resources == null` 时的空数据库降级分支已修复。
3. **会话退出的可观测清理**：本地与确认视图独立注销统计 provider；旁观、重连、资源租约和会话诊断的重复清理由 Unity EditMode 测试覆盖。池指标提供活动、缓存和累计销毁计数，可用于实际场景退出时核对。

## P1：收紧依赖边界

1. **把纯视图状态移出主程序集**：`BattleViewHandle` 同时含 `GameObject`、`MonoViewHandle`、`Vector3` 和插值状态；先抽出实体键、actor 映射、位置采样、可见性/生命周期决策为纯 C# 状态，Unity handle 留在平台适配层。以无 Unity 引用的程序集可独立编译和单元测试为完成标准。
2. **表现资源端口**：`BattleVfxPrefabCache` 依赖 `ResourcesAssetProvider.Shared`；为特效 prefab 的获取/释放提供会话级端口，Unity Resources 实现保留为默认适配。先确定租约和缓存清理时机，再接 Addressables 或其他项目资源系统。
3. **网络复用边界**：保持 `ITransport`、`NetworkSdkBuilder`、请求/推送、重连和诊断在通用包；把 `GatewayRoomWireProtocolClient`、MOBA op code、snapshot 解码及业务重试留在 MOBA 包。提供一个非 MOBA 的最小接入示例，并以 SDK/transport/Room 的独立测试证明不需要 View Runtime 依赖。
   `NetworkTrafficProtocolDecoderModuleTests` 原有的 9 个 MOBA frame-sync decoder 缺口已补齐，并通过网络 SDK 测试。
4. **主程序集瘦身**：当前 `AbilityKit.Demo.Moba.View.Runtime` asmdef 同时引用约四十个能力包且 `noEngineReferences=false`。按依赖方向逐步迁出 Client Session、Networking Replication、纯 Presentation 决策和 Unity View；每次迁移保持现有 facade 与 asmdef 可编译，避免一次性移动整个目录。

## P2：性能与工程化

1. 为特效、角色 shell、投射物和区域池建立统一指标：活动数、缓存数、峰值、创建/销毁数、失败数；现有 `BattleViewPoolStatsOverlay` 可作为观察入口。用压力场景确定每类池上限和回收策略。
2. 为已拆出的纯模块添加程序集依赖检查，阻止 `UnityEngine` 或项目网关协议回流。当前 `noEngineReferences` 已覆盖 Flow Core、Presentation Core/Adapter 和 Network Runtime；继续覆盖新迁移的模块。
3. 将资源和网络适配层的线程契约写入 API：Unity 对象只在主线程使用；网络回调经 dispatcher 进入消费线程；跨异步边界保存推送 payload 时复制所需数据。

## 验收顺序

先完成 P0 的池与失败路径回归，再迁出一条完整的纯视图状态链路，随后用第二个项目或独立 sample 验证网络 SDK 的装配。每一步以可执行测试、程序集依赖和实际场景清理结果验收；不以文件夹或类型数量减少作为完成标准。

## 实施记录（2026-09-24）

| 优先级 | 已落地内容 | 验收依据 |
| --- | --- | --- |
| P0 | 四类池按实际租用实例校验归还，拒绝跨桶、重复和外来对象；`Clear` 销毁未归还实例。VFX 创建和子模块 attach 的失败路径回滚；预测/确认视图各自注销统计 provider，避免共享根节点互相清空。 | 池优化 Unity EditMode 92/92；层级、旁观和诊断复制 41/41；资源租约与会话生命周期 81/81。旁观订阅按正确的响应类型解码。 |
| P1 | actor 索引与位置采样迁入无引擎的 Presentation Core，Unity 侧只保留 `Vector3` 适配；VFX prefab 使用会话资产租约；MOBA/Room 诊断解码器覆盖新增消息。Shooter `ShooterClientNetworkLauncher` 是独立 SDK 接入实例。 | Presentation Core 桌面测试 48/48；网络 SDK 142/142；Shooter 启动器 5/5；View Runtime 187/187。 |
| P2 | 四类池统一暴露活动、缓存、峰值、创建、销毁、失败指标，并接入层级浮层；突发归还测试验证每桶最多保留配置上限，超量销毁。纯程序集依赖门禁禁止 Unity/MOBA 网关引用回流；资源主线程、dispatcher 消费线程和异步 payload 所有权写入 API，网络会话跨异步回调复制 payload。 | `PureAssemblyBoundaryTests` 在 Presentation Core 的 48/48 桌面测试中通过；`NetworkSessionBufferOwnershipTests` 在 41/41 Unity 测试组中通过。 |

统计浮层格式为 `类别:活动/缓存^峰值+创建-销毁!失败`，类别 `S/V/P/A` 分别表示角色 shell、特效、投射物、区域。默认每桶缓存上限：角色 shell 16，特效 32，投射物 16，区域 16；可通过现有构造参数调整。突发验收用较小的每桶上限 2 验证溢出销毁行为，默认上限仍需用实际战斗峰值持续校准。

主 View Runtime 程序集目前仍承载 Client Session 和 Networking Replication；P1 采用渐进拆分，已完成 actor 映射与位置采样这条纯状态链路。继续迁移时应沿现有 Flow Core、Presentation Core/Adapter 和 ProtocolAdapter 的依赖方向扩展，不让平台程序集成为纯模块的反向依赖。

## 主要依据

- 表现：`Runtime/Game/Battle/Presentation/Vfx/BattleVfxManager.cs`、`BattleVfxGameObjectPool.cs`、`BattleVfxEntityFactory.cs`、`View/BattleViewHandleStore.cs`、`Core/PresentationCueViewRegistry.cs`。
- 生命周期：`Runtime/Game/Battle/Presentation/Features/View/Shared/ViewVfxSubFeatures.cs`、`Entities/Features/BattleEntityFeature.cs`、`View/BattleViewLifecycleController.cs`。
- 边界：`Runtime/com.abilitykit.demo.moba.view.runtime.asmdef`、`Presentation/Core/*.asmdef`、`Presentation/Adapter/*.asmdef`、`App/Flow/Core/*.asmdef`。
- 网络：`com.abilitykit.network.runtime/Runtime/Network/Abstractions/ITransport.cs`、`com.abilitykit.network.sdk/Runtime/NetworkSdkBuilder.cs`、`Runtime/Game/Battle/Client/Gateway/GatewayRoomWireProtocolClient.cs`。
