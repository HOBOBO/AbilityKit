# Examples & troubleshooting

## 常见拆分点示例（来自 BattleSessionFeature 真实结构）

- **按生命周期阶段拆**：`BattleSessionFeature.Lifecycle.cs`（`OnAttach/OnDetach/Tick`）/ `.SessionStart.cs` / `.World.cs`
- **按 Sim 变体拆**：`Sim/BattleSessionFeature.SimTick.RemoteDriven.cs`（`TickRemoteDrivenLocalSim`）/ `SimTick.Confirmed.cs`（`TickConfirmedAuthorityWorldSim`）
- **dispose helpers 按领域拆**：`Core/BattleSessionFeature.DispatcherDispose.cs` / `Sim/BattleSessionFeature.SimDispose.cs`
- **accessors 按职责拆**：`.Accessors.cs` / `.PhaseAccessors.cs` / `.StateAccessors.cs` / `.SnapshotAccessors.cs` / `.NetworkAccessors.cs`
- **host 契约拆**：`.HostBridges.cs`（显式接口实现）/ `.HostInterfaces.cs`（接口定义）/ `.OrchestratorHost.cs` / `.NetAdapterContextHost.cs`
- **领域子目录**：`Gateway/`（含 `GatewayConnection / GatewayPreparation / GatewayTimeSync / GatewayTimeSyncStats / GatewayFrameTiming / GatewayRoom`）/ `Net/` / `Snapshot/` / `Editor/`
- **`#if UNITY_EDITOR` 的 debug/seek 拆到** `Editor/*.Debug.cs`，与主逻辑 controller 分离

## Controller 调用 Feature 的标准范式

```csharp
// Controller 构造
public sealed class TickLoopController
{
    private readonly BattleSessionState _state;
    private readonly BattleSessionHandles _handles;
    private readonly ITickLoopHost _host;  // ← Feature 自己（窄接口）

    public TickLoopController(BattleSessionState state, BattleSessionHandles handles, ITickLoopHost host)
    {
        _state = state; _handles = handles; _host = host;
    }

    public void MainTick(float dt)
    {
        // 读 state
        _state.Tick.TickAcc += dt;
        // 读 handles
        var session = _handles.Session;
        // 经 host 回调 Feature
        _host.TickRemoteDrivenLocalSim(dt);
    }
}

// Feature 显式接口实现（HostBridges.cs）
void ITickLoopHost.TickRemoteDrivenLocalSim(float dt) => TickRemoteDrivenLocalSim(dt);
```

## Entitas ECS 反注册的标准范式

```csharp
public class MobaPassiveSkillTriggerRegisterSystem : ReactiveWorldSystemBase<ActorEntity>
{
    protected override void OnTearDown()
    {
        try {
            var entities = _group.GetEntities();
            for (int i = 0; i < entities.Length; i++)
                _passives.UnregisterActor(entities[i], frame);
            _passives?.ReleaseAllCachedOwnerKeys();
        } finally {
            base.OnTearDown();   // 取消事件订阅、清 _pending/_handlers
        }
    }
}
```

## 常见问题

### 编译错误：找不到方法/类

- 检查是否忘了把类声明改为 `partial`
- 检查新文件 namespace 与外层嵌套类型是否一致
- 检查是否漏在 asmdef `references` 里声明依赖（asmdef 引用不传递）

### 行为回归：Start/Stop 顺序改变

- 检查是否在迁移时调整了 finally 清理顺序
- Handles.Reset 是否遗漏了新引入的资源（按领域 partial 拆后容易漏）

### SubFeature 越权访问 Feature

- 不应直接 `feature.Xxx`，应通过 `FeatureModuleContext<BattleSessionFeature>` + `BattleSessionFeatureRuntimeAccess.TryGet<IXxxRuntime>(...)`
- 检查 Feature 是否正确显式实现了对应 `ISession*Runtime` 接口

### Controller 变成有状态的

- Controller 应该无状态（所有状态从 `_state.*` 读写）
- 若发现需要跨 tick 缓存，应该把字段移到 State 的对应嵌套 POCO

### 反注册放在 OnCleanup 里

- `OnCleanup` 是每帧清理，不是销毁阶段
- 反注册放 `OnTearDown`（`ReactiveWorldSystemBase` 注释明说"资源释放在 TearDown 中进行"）
- `OnTearDown` 始终执行，不受 `Enabled` 影响

### 注释噪音过大

- 只对新增业务文件要求文件头注释
- 机械迁移不强制到处补注释
- 非公共/显而易见代码可不写（参考 `EffectService` 几乎无注释也被接受）

## 旧 skill 引用过但已失效的内容清单

- 所有 `Runtime/Game/Flow/Battle/Features/Session/Core/` 路径 → 改为 `Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Client/Session/Features/`
- `BattleSession*Controller.cs` → 实际是 `Session*Controller.cs`（不带 Battle 前缀）
- 旧 skill 把 Handles 当单一类 → 实际是 8 个 partial 文件
- 旧 skill 没提 Host 接口反向桥接（`HostBridges.cs` + `HostInterfaces.cs`）
- 旧 skill 没提 Runtime 契约（`Runtime.cs` + SubFeature 经 `FeatureModuleContext<T>` 访问）
- 旧 skill 没提 Entitas ECS 层与 Session 层是并存关系
