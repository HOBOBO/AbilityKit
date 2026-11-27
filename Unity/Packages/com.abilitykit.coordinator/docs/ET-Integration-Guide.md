# ET Demo 最小化接入 Coordinator 框架方案

> 文档类型：历史接入记录 / 已取代。下列实现状态、编译通过与旧 API 仅保留原记录含义，未作为 2026-11-25 当前结果复验。当前包只含配置、契约、DTO 与 codec，使用见[现行指南](../Documentation~/Usage.md)；不要按下列代码创建已移除的协调器或 adapter。本次保留原路径，未核校 ET 主链。

## 实施状态

| 组件 | 状态 | 说明 |
|------|------|------|
| `ETCoordinatorHost.cs` | ✅ 已创建 | 实现 `ISessionCoordinatorHost` |
| `ETBattleDriverHost.cs` | ✅ 已创建 | 实现 `IBattleDriverHost` |
| `ETMobaBattleDriver.SyncAdapter` | ✅ 已添加 | 用于获取 actor states |
| Coordinator 包引用 | ✅ 已添加 | ET Demo 项目引用 Coordinator |
| 编译验证 | ✅ 通过 | 无编译错误 |

---

## 核心实现文件

### ETCoordinatorHost

```csharp
// 创建 HostRuntime + 配置服务
public sealed class ETCoordinatorHost : ISessionCoordinatorHost
{
    private readonly ITextAssetLoader _configLoader;

    public IWorldHost CreateWorldHost(SessionConfig config)
    {
        _hostRuntime = new HostRuntime(new EmptyWorldManager(), new HostRuntimeOptions());
        return _hostRuntime;
    }

    public void RegisterServices(IWorld world, SessionConfig config)
    {
        // Services registered by moba.core's bootstrap
    }

    public PlayerSpawnData[] CreatePlayerSpawnData(SessionConfig config)
    {
        return new[]
        {
            PlayerSpawnData.CreateLocalPlayer(1, 1001, 0f, 0f),
            PlayerSpawnData.CreateLocalPlayer(2, 1001, 20f, 20f),
        };
    }
}
```

### ETBattleDriverHost

```csharp
// 封装 ETMobaBattleDriver，实现 IBattleDriverHost
public sealed class ETBattleDriverHost : IBattleDriverHost
{
    private readonly ETMobaBattleDriver _driver;

    public void SubmitInputs(PlayerInput[] inputs)
    {
        foreach (var input in inputs)
        {
            switch (input.OpCode)
            {
                case InputOpCodes.Move:
                    HandleMoveInput(input);
                    break;
                case InputOpCodes.Skill:
                    HandleSkillInput(input);
                    break;
            }
        }
    }

    public EntityState[] GetAllEntityStates()
    {
        var adapter = _driver.SyncAdapter as IETBattleSyncAdapter;
        var actorStates = adapter.GetAllActorStates();
        return actorStates.Select(s => new EntityState
        {
            EntityId = s.ActorId,
            X = s.X, Y = s.Y, Z = s.Z,
            Rotation = s.Rotation,
            Hp = s.Hp, HpMax = s.HpMax,
            TeamId = s.TeamId,
            IsDead = s.Hp <= 0,
        }).ToArray();
    }
}
```

---

## 一、现状分析

### 1.1 当前 ET Demo 代码结构

```
ET Demo (1500+ 行战斗相关代码)
├── ETBattleComponent.cs (154 行)
├── ETBattleComponentSystem.cs (437 行)
├── ETBattleDriverBridge.cs
├── ETSyncAdapterFactory.cs (153 行)
├── ETFrameSyncAdapter.cs
├── ETStateSyncAdapter.cs
├── ETHybridSyncAdapter.cs
├── Session SubFeatures (8 个组件)
│   ├── ETSessionEventsSubFeature.cs
│   ├── ETSessionLifecycleSubFeature.cs
│   ├── ETSessionTickLoopSubFeature.cs
│   ├── ETSessionSnapshotRoutingSubFeature.cs
│   └── ...
└── Plus 大量 View/Snapshot/PlayerRegistry 组件
```

### 1.2 问题

| 问题 | 说明 |
|------|------|
| **代码重复** | ET 重新实现了一套 Session SubFeatures，与 Coordinator 重复 |
| **同步适配器重复** | ET 有自己的 ETFrameSyncAdapter 等，Coordinator 已有 LocalSyncAdapter |
| **接入代价高** | 需要实现大量 ET 特定代码才能接入战斗逻辑 |

## 二、目标：最小化接入

### 2.1 目标架构

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                      目标：最小化接入架构                                   │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ET Demo (少量适配代码)                                                     │
│  ┌─────────────────────────────────────────────────────────────────────┐ │
│  │ 只需要实现 3 个接口：                                                 │ │
│  │ • ISessionCoordinatorHost ──── 创建 HostRuntime、注册服务、加载配置   │ │
│  │ • IBattleDriverHost ──────── 封装 ETMobaBattleDriver                  │ │
│  │ • IViewEventSink ─────────── 桥接到 ET 事件系统                       │ │
│  │                                                                       │ │
│  │ 总计：约 200-300 行适配代码                                           │ │
│  └─────────────────────────────────────────────────────────────────────┘ │
│                                    │                                        │
│                                    ▼                                        │
│  ┌─────────────────────────────────────────────────────────────────────┐ │
│  │                AbilityKit.Coordinator                                 │ │
│  │                                                                       │ │
│  │  SessionCoordinator                                                  │ │
│  │  ├── LocalSyncAdapter ── 帧同步 (复用)                               │ │
│  │  ├── RemoteSyncAdapter ── 状态同步 (复用)                            │ │
│  │  ├── HybridSyncAdapter ── 混合模式 (复用)                            │ │
│  │  └── Session SubFeatures ── 内置                                      │ │
│  └─────────────────────────────────────────────────────────────────────┘ │
│                                    │                                        │
│                                    ▼                                        │
│  ┌─────────────────────────────────────────────────────────────────────┐ │
│  │                AbilityKit.Host                                        │ │
│  │                                                                       │ │
│  │  HostRuntime ── 宿主运行时 (moba.runtime 在此运行)                     │ │
│  └─────────────────────────────────────────────────────────────────────┘ │
│                                    │                                        │
│                                    ▼                                        │
│  ┌─────────────────────────────────────────────────────────────────────┐ │
│  │                AbilityKit.Demo.Moba (战斗逻辑层)                       │ │
│  │                                                                       │ │
│  │  技能系统 / 伤害计算 / Buff / 移动                                    │ │
│  │  (无改动，可独立开发测试)                                             │ │
│  └─────────────────────────────────────────────────────────────────────┘ │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 2.2 接入前后对比

| 维度 | 接入前 | 接入后 | 减少 |
|------|--------|--------|------|
| 战斗相关代码 | 1500+ 行 | ~300 行 | **80%** |
| SyncAdapter 实现 | 3 个 (ETFrameSyncAdapter 等) | 0 个 (复用框架) | **100%** |
| Session SubFeatures | 8 个组件 | 0 个 (复用框架) | **100%** |
| 维护成本 | 高 (多套实现) | 低 (统一框架) | **显著降低** |

## 三、最小化接入实现

### 3.1 ISessionCoordinatorHost 实现

```csharp
// ETCoordinatorHost.cs (约 80 行)

namespace ET.Logic
{
    /// <summary>
    /// ET Demo 的 Coordinator Host 实现
    ///
    /// 职责：
    /// - 创建 HostRuntime
    /// - 注册 moba.runtime 服务
    /// - 加载配置文件
    /// - 创建玩家生成数据
    /// </summary>
    public sealed class ETCoordinatorHost : ISessionCoordinatorHost
    {
        private ITextAssetLoader _configLoader;

        public ETCoordinatorHost(ITextAssetLoader configLoader)
        {
            _configLoader = configLoader;
        }

        public IWorldHost CreateWorldHost(SessionConfig config)
        {
            var options = new HostRuntimeOptions
            {
                TickRate = config.TickRate,
            };
            return new HostRuntime(options);
        }

        public void RegisterServices(IWorld world, SessionConfig config)
        {
            var resolver = world.Services;

            // 注册配置加载器
            resolver.Register<ITextAssetLoader>(_configLoader);

            // 注册 moba.runtime 服务
            resolver.Register<SkillExecutor>(new SkillExecutor());
            resolver.Register<DamagePipelineService>(new DamagePipelineService());
            resolver.Register<MobaActorIdLookupService>(new MobaActorIdLookupService());
            // ... 其他服务
        }

        public void LoadConfig(IWorld world, SessionConfig config)
        {
            var loader = world.Services.Resolve<ITextAssetLoader>();

            // 加载角色配置
            var charactersJson = loader.LoadText("configs/characters.json");
            var characters = JsonUtility.FromJson<CharacterConfig[]>(charactersJson);

            // 加载技能配置
            var skillsJson = loader.LoadText("configs/skills.json");
            var skills = JsonUtility.FromJson<SkillConfig[]>(skillsJson);

            // ... 其他配置
        }

        public PlayerSpawnData[] CreatePlayerSpawnData(SessionConfig config)
        {
            // 创建测试玩家数据
            return new[]
            {
                PlayerSpawnData.CreateLocalPlayer(1, 10001, 0, 0),
                PlayerSpawnData.CreateLocalPlayer(2, 10001, 10, 0),
            };
        }
    }
}
```

### 3.2 IBattleDriverHost 实现

```csharp
// ETBattleDriverHost.cs (约 100 行)

namespace ET.Logic
{
    /// <summary>
    /// ET Demo 的 Battle Driver Host 实现
    ///
    /// 职责：
    /// - 封装 ETMobaBattleDriver
    /// - 提供帧号、逻辑时间
    /// - 提交输入到战斗逻辑
    /// - 查询实体状态
    /// </summary>
    public sealed class ETBattleDriverHost : IBattleDriverHost
    {
        private readonly ETMobaBattleDriver _driver;

        public ETBattleDriverHost(ETMobaBattleDriver driver)
        {
            _driver = driver;
        }

        public int CurrentFrame => _driver.CurrentFrame;

        public double LogicTimeSeconds => _driver.LogicTimeSeconds;

        public bool IsRunning => _driver.IsRunning;

        public void SubmitInputs(PlayerInput[] inputs)
        {
            foreach (var input in inputs)
            {
                switch (input.Type)
                {
                    case InputType.Move:
                        HandleMoveInput(input);
                        break;
                    case InputType.Skill:
                        HandleSkillInput(input);
                        break;
                }
            }
        }

        public EntityState[] GetAllEntityStates()
        {
            // 从 SnapshotDispatcher 获取实体状态
            var dispatcher = _driver.SnapshotDispatcher;
            if (dispatcher == null)
                return Array.Empty<EntityState>();

            return dispatcher.GetAllEntityStates()
                .Select(e => new EntityState
                {
                    EntityId = e.EntityId,
                    X = e.X,
                    Y = e.Y,
                    Z = e.Z,
                    Rotation = e.Rotation,
                    CurrentHp = e.CurrentHp,
                    MaxHp = e.MaxHp,
                    IsDead = e.IsDead,
                })
                .ToArray();
        }

        private void HandleMoveInput(PlayerInput input)
        {
            // 转换为 ET 内部的移动命令
            var cmd = new MoveCommand
            {
                ActorId = (int)input.PlayerId,
                TargetX = input.Payload.GetFloat("x"),
                TargetZ = input.Payload.GetFloat("z"),
            };
            // 提交到 ETMobaBattleDriver
            ETBattleDriverBridge.SubmitMoveInput(_driver, cmd);
        }

        private void HandleSkillInput(PlayerInput input)
        {
            var cmd = new SkillCommand
            {
                ActorId = (int)input.PlayerId,
                SkillSlot = input.Payload.GetInt("slot"),
                TargetX = input.Payload.GetFloat("x"),
                TargetZ = input.Payload.GetFloat("z"),
            };
            ETBattleDriverBridge.SubmitSkillInput(_driver, cmd);
        }
    }
}
```

### 3.3 IViewEventSink 实现

```csharp
// ETViewEventSink.cs (约 80 行)

namespace ET.Logic
{
    /// <summary>
    /// ET Demo 的 View Event Sink 实现
    ///
    /// 职责：
    /// - 接收战斗事件
    /// - 桥接到 ET 事件系统
    /// - 触发 View 层更新
    /// </summary>
    public sealed class ETViewEventSink : IViewEventSink
    {
        private readonly Scene _scene;

        public ETViewEventSink(Scene scene)
        {
            _scene = scene;
        }

        public void OnEntitySpawn(EntityState state)
        {
            var evt = new ActorSpawnEvent
            {
                ActorId = state.EntityId,
                X = state.X,
                Y = state.Y,
                Z = state.Z,
                CurrentHp = state.CurrentHp,
                MaxHp = state.MaxHp,
            };
            EventSystem.Instance.Publish(_scene, evt);
        }

        public void OnEntityTransform(int entityId, float x, float y, float z, float rotation)
        {
            var evt = new ActorMoveEvent
            {
                ActorId = entityId,
                X = x,
                Y = y,
                Z = z,
            };
            EventSystem.Instance.Publish(_scene, evt);
        }

        public void OnEntityDamage(int entityId, float damage, float currentHp)
        {
            var evt = new ActorDamageEvent
            {
                ActorId = entityId,
                Damage = damage,
                CurrentHp = currentHp,
            };
            EventSystem.Instance.Publish(_scene, evt);
        }

        public void OnEntityDead(int entityId, int killerId)
        {
            var evt = new ActorDeadEvent
            {
                ActorId = entityId,
                KillerId = killerId,
            };
            EventSystem.Instance.Publish(_scene, evt);
        }

        public void OnFrameSyncComplete(int frame)
        {
            // 可选：发布帧同步完成事件
        }

        public void OnBattleStart(int frame)
        {
            var evt = new BattleStartEvent();
            EventSystem.Instance.Publish(_scene, evt);
        }

        public void OnBattleEnd(int frame, int winTeamId)
        {
            var evt = new BattleEndEvent
            {
                WinTeamId = winTeamId,
            };
            EventSystem.Instance.Publish(_scene, evt);
        }
    }
}
```

### 3.4 简化后的 ETBattleComponent

```csharp
// ETBattleComponent.cs (简化版，约 80 行)

namespace ET.Logic
{
    /// <summary>
    /// ET Battle Component - 简化版
    ///
    /// 使用 Coordinator 框架后：
    /// - 不再需要管理 SyncAdapter (框架处理)
    /// - 不再需要 Session SubFeatures (框架处理)
    /// - 不再需要 DriverBridge (IBattleDriverHost 处理)
    ///
    /// 只负责：
    /// - ET 生命周期管理
    /// - 视图事件转发
    /// </summary>
    [ComponentOf(typeof(Scene))]
    public class ETBattleComponent : Entity, IAwake, IDestroy
    {
        // ============== Identity ==============

        public long BattleId { get; set; }
        public long PlayerId { get; set; }

        // ============== Coordinator (框架) ==============

        public ISessionCoordinator Coordinator { get; set; }
        public IBattleDriverHost BattleDriverHost { get; set; }

        // ============== ET-Specific (少量) ==============

        public IETViewEventSink ViewSink { get; set; }

        // ============== Lifecycle ==============

        public void Awake() { }

        public void OnDestroy(ETBattleComponent self)
        {
            Coordinator?.Dispose();
        }
    }
}
```

### 3.5 简化后的 ETBattleComponentSystem

```csharp
// ETBattleComponentSystem.cs (简化版，约 150 行)

namespace ET.Logic
{
    [EntitySystemOf(typeof(ETBattleComponent))]
    [FriendOf(typeof(ETBattleComponent))]
    public static partial class ETBattleComponentSystem
    {
        [EntitySystem]
        private static void Awake(this ETBattleComponent self)
        {
            Log.Info("[ETBattle] ETBattleComponent awake");
        }

        [EntitySystem]
        private static void Destroy(this ETBattleComponent self)
        {
            self.Coordinator?.Dispose();
            self.Coordinator = null;
            self.BattleDriverHost = null;
            self.ViewSink = null;
        }

        // ============== Initialize (使用 Coordinator) ==============

        public static void InitializeBattle(this ETBattleComponent self, BattleStartPlan plan)
        {
            self.BattleId = IdGenerater.Instance.GenerateId();
            self.PlayerId = plan.PlayerId;

            var scene = self.Scene();

            // 1. 创建配置
            var config = SessionConfig.CreateForMode(
                mode: (SyncMode)plan.SyncMode,
                localPlayerId: (int)plan.PlayerId,
                tickRate: plan.TickRate > 0 ? plan.TickRate : 30
            );

            // 2. 创建 Coordinator Host
            var configLoader = new ETTextAssetLoader();
            var host = new ETCoordinatorHost(configLoader);

            // 3. 创建 Coordinator
            self.Coordinator = new SessionCoordinator();
            self.Coordinator.Initialize(config, host);

            // 4. 创建 Battle Driver Host
            var driverComponent = scene.AddComponent<ETMobaBattleDriver>();
            driverComponent.Initialize(plan, new ETBattleViewEventSink(self), configLoader);
            self.BattleDriverHost = new ETBattleDriverHost(driverComponent);

            // 5. 设置到 Coordinator
            self.Coordinator.SetDriverHost(self.BattleDriverHost);

            // 6. 设置 View Sink
            self.Coordinator.SetViewEventSink(new ETViewEventSink(scene));

            // 7. 订阅 Hooks
            self.Coordinator.Hooks.OnSessionStarted += (cfg) =>
            {
                Log.Info($"[ETBattle] Session started!");
                EventSystem.Instance.Publish<Scene, BattleSceneInitFinish>(scene, new BattleSceneInitFinish());
            };

            self.Coordinator.Hooks.OnSessionFailed += (ex) =>
            {
                Log.Error($"[ETBattle] Session failed: {ex.Message}");
            };

            Log.Info($"[ETBattle] Battle initialized (Coordinator Mode)");
        }

        // ============== Battle Lifecycle ==============

        public static void StartBattle(this ETBattleComponent self)
        {
            self.Coordinator?.Start();
        }

        public static void EndBattle(this ETBattleComponent self, bool isVictory)
        {
            self.Coordinator?.Stop();
        }

        // ============== Frame ==============

        public static void AdvanceFrame(this ETBattleComponent self)
        {
            if (self.Coordinator?.State == SessionState.Running)
            {
                self.Coordinator.Tick(1f / 30f);
            }
        }

        // ============== Input ==============

        public static void SubmitMoveInput(this ETBattleComponent self, long actorId, float x, float z)
        {
            self.Coordinator?.SubmitLocalInput(new PlayerInput
            {
                Type = InputType.Move,
                PlayerId = actorId,
                Payload = new InputPayload { { "x", x }, { "z", z } }
            });
        }

        public static void SubmitSkillInput(this ETBattleComponent self, long actorId, int slot, float x, float z)
        {
            self.Coordinator?.SubmitLocalInput(new PlayerInput
            {
                Type = InputType.Skill,
                PlayerId = actorId,
                Payload = new InputPayload { { "slot", slot }, { "x", x }, { "z", z } }
            });
        }
    }
}
```

## 四、架构对比

### 4.1 接入前 vs 接入后

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           接入前：ET 全家桶                                 │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ETBattleComponent                                                         │
│  ├── SyncAdapter (ETFrameSyncAdapter)                                      │
│  ├── RemoteSyncAdapter (ETStateSyncAdapter)                                │
│  ├── PredictionSyncAdapter (ETHybridSyncAdapter)                          │
│  ├── ClientPredictionRunner                                                │
│  ├── ClientPredictionReconciler                                            │
│  ├── SessionHooks                                                          │
│  └── 8 个 Session SubFeatures                                              │
│                                                                             │
│  总计：20+ 个组件/类                                                       │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                           接入后：框架驱动                                  │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ETBattleComponent                                                         │
│  └── Coordinator (框架) ───────────────────────────────────────────────┐   │
│                                                                       │   │
│  框架内部 (无 ET 代码)：                                            │   │
│  ├── LocalSyncAdapter / RemoteSyncAdapter / HybridSyncAdapter       │   │
│  ├── Session SubFeatures (内置)                                     │   │
│  ├── SessionHooks                                                    │   │
│  └── IBattleDriverHost (ET 实现，约 100 行)                        │   │
│                                                                       │   │
│  ET 适配代码 (总计 ~300 行)：                                        │   │
│  ├── ETCoordinatorHost ──── ~80 行                                │   │
│  ├── ETBattleDriverHost ─── ~100 行                               │   │
│  └── ETViewEventSink ──── ~80 行                                   │   │
│                                                                       │   │
└───────────────────────────────────────────────────────────────────────────────┘
```

### 4.2 切换同步模式

```csharp
// 切换只需修改配置
public static void SetSyncMode(ETBattleComponent self, SyncMode mode)
{
    // 重新初始化（实际项目可用更优雅的切换方式）
    self.Coordinator?.Dispose();

    var config = SessionConfig.CreateForMode(mode, (int)self.PlayerId);
    var host = new ETCoordinatorHost(new ETTextAssetLoader());

    self.Coordinator = new SessionCoordinator();
    self.Coordinator.Initialize(config, host);
    self.Coordinator.SetDriverHost(new ETBattleDriverHost(self.GetDriver()));
    self.Coordinator.SetViewEventSink(new ETViewEventSink(self.Scene()));
}
```

## 五、文件清单

### 5.1 需要创建的文件

| 文件 | 行数 | 职责 |
|------|------|------|
| `ETCoordinatorHost.cs` | ~80 | 实现 ISessionCoordinatorHost |
| `ETBattleDriverHost.cs` | ~100 | 实现 IBattleDriverHost |
| `ETViewEventSink.cs` | ~80 | 实现 IViewEventSink |

### 5.2 需要修改的文件

| 文件 | 改动 |
|------|------|
| `ETBattleComponent.cs` | 简化，移除 SyncAdapter/SubFeatures |
| `ETBattleComponentSystem.cs` | 使用 Coordinator 初始化 |
| 删除文件 | |
| `ETSyncAdapterFactory.cs` | 删除 (框架提供) |
| `ETFrameSyncAdapter.cs` | 删除 (框架提供) |
| `ETStateSyncAdapter.cs` | 删除 (框架提供) |
| `ETHybridSyncAdapter.cs` | 删除 (框架提供) |
| `ETSession*.cs` (8 个) | 删除 (框架提供) |
| `ETClientPrediction*.cs` | 删除或简化 |

### 5.3 保留的文件

| 文件 | 原因 |
|------|------|
| `ETMobaBattleDriver.cs` | 封装 moba.runtime 战斗逻辑 |
| `ETBattleDriverBridge.cs` | 辅助方法桥接 |
| `ETTextAssetLoader.cs` | ET 特定的资源加载 |
| `ETUnitComponent.cs` | ET 单位管理 |
| View 层组件 | ET 特定视图实现 |

## 六、优势总结

| 优势 | 说明 |
|------|------|
| **代码量减少 80%** | 从 1500+ 行减少到 ~300 行适配代码 |
| **复用框架能力** | SyncAdapter、SubFeatures、Hooks 等由框架提供 |
| **切换成本低** | 同步模式切换只需修改配置 |
| **维护成本低** | 框架更新无需修改 ET 适配代码 |
| **独立开发** | moba.runtime 可独立开发测试 |
| **标准化** | 接入其他项目只需实现相同 3 个接口 |
