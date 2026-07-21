# Invariants (must hold)

## 1. State 必须保持纯数据

- 不持有可释放资源 / Unity 对象 / 线程调度器 / IDisposable / Task / CTS
- 内部用嵌套 POCO 子状态组织（参考 `BattleSessionState` 的 `TickState / RemoteDrivenSimState / ConfirmedSimState / FlagsState / GatewayRoomTimeSyncState / EditorHooksState`）
- 每个子状态各自提供 `Reset()`，便于整体复位

## 2. Handles 必须可兜底释放（且自身也常是 partial）

- 异常路径下也能 `Reset()` 清干净，内部记录异常
- 按领域拆为 partial 文件（参考 `BattleSessionHandles`：主 + `.Phase / .Net / .Confirmed / .RemoteDriven / .Snapshot / .GatewayRoom / .Dispatchers` 共 8 个）
- 持有外部对象：`BattleLogicSession Session` + 各领域 handles

## 3. 控制权单向

- `Controller / SubFeature` 可以读写 `State / Handles`，但 State 不依赖行为
- Controller 是无状态的（不持有跨 tick 的私有字段），所有状态都从 `state` 读写
- Controller 签名一致：`(BattleSessionState state, BattleSessionHandles handles, ISessionXxxHost host)` — `host` 是 Feature 本身

## 4. Host 接口反向桥接

- Feature 用**显式接口实现**把私有方法暴露给 Controller（参考 `BattleSessionFeature.HostBridges.cs`）
- Host 接口定义在 `BattleSessionFeature.HostInterfaces.cs`（如 `ITickLoopHost / ISessionOrchestratorHost / INetAdapterContextHost`）
- 这样 Controller 不直接持有 Feature 引用，只持有窄接口

## 5. Runtime 契约（SubFeature 访问 Feature）

- Feature 显式实现一组 `ISession*Runtime` 接口（参考 `BattleSessionFeature.Runtime.cs`）
- SubFeature 通过 `FeatureModuleContext<BattleSessionFeature>` + `BattleSessionFeatureRuntimeAccess.TryGet<...>` 反向取这些契约
- 不要让 SubFeature 直接 `feature.Xxx`，要走 Runtime 契约

## 6. 生命周期统一

- Start/Stop 的资源创建/销毁顺序必须稳定且可追踪
- dispose helpers 按领域拆 partial（参考 `BattleSessionFeature.DispatcherDispose.cs` / `.SimDispose.cs`）

## 7. WorldSystemBase（Entitas ECS 层）的反注册时机

- 继承 `WorldSystemBase` 或 `ReactiveWorldSystemBase<T>` 的 system，反注册放在 `OnTearDown()`
- **不要**放在 `OnCleanup()`（`OnCleanup` 是每帧清理，`ReactiveWorldSystemBase` 注释明说"空实现，资源释放在 TearDown 中进行"）
- `OnTearDown` 始终执行，不受 `Enabled` 影响（基类 `TearDown()` 无 `_enabled` 判断）
- 参考真实例子：`MobaPassiveSkillTriggerRegisterSystem.OnTearDown`（遍历 group 反注册被动技能 + 取消订阅）

## 8. 注释语言

- 新增注释使用中文
- 新增业务文件补文件头中文说明
- 非公共、显而易见的代码可以不写注释（参考 `MobaBuffService` 有中文 summary，而 `EffectService` 几乎无注释——两者都接受）
