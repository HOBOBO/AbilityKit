# Shooter 示例系统性设计

## 1. 设计目标

Shooter 示例的核心使命是**作为 AbilityKit 网络同步框架的能力演示载体**，需要：

1. **完整玩法闭环** — 创建实体、移动瞄准、发射子弹、碰撞扣血、实体销毁，形成可交互的游戏体验
2. **Editor 窗口管控** — 网络环境、同步模式、运行参数全部通过独立 Editor 窗口管理，不干扰 Game 窗口
3. **同步效果可观测** — 多 World 并排对比、漂移高亮、回放诊断，让同步行为一目了然
4. **纯 C# 可测** — 所有核心逻辑在 .NET 测试中可验证，Unity 侧仅负责渲染和输入

## 2. 现有能力盘点

### 2.1 已完成的层

| 层 | 包 | 状态 | 关键类型 |
|---|---|---|---|
| **协议** | `com.abilitykit.protocol.shooter` | ✅ 完成 | `ShooterPlayerCommand`, `ShooterStateSnapshotPayload`, `ShooterPlayerSnapshot`, `ShooterBulletSnapshot`, `ShooterEventSnapshot`, `ShooterStartGamePayload` |
| **玩法** | `com.abilitykit.demo.shooter.runtime` | ✅ 完成 | `ShooterBattleSimulation`（TickPlayers→移动/瞄准/开火, TickBullets→移动/碰撞/伤害/销毁） |
| **ECS** | 同上 | ✅ 完成 | `ShooterSveltoPlayerComponent`, `ShooterSveltoProjectileComponent`, `IShooterEntityManager` |
| **表现映射** | `com.abilitykit.demo.shooter.view.runtime` | ✅ 完成 | `ShooterSnapshotViewModelMapper` → `ShooterSnapshotViewBatch`（EntityChange/Transform/Health/Score/ProjectileLifetime/Events） |
| **同步控制** | 同上 | ✅ 完成 | `ShooterClientPredictRollbackSyncController`, `ShooterClientAuthoritativeInterpolationSyncController`, `ShooterClientSyncControllerFactory` |
| **验收抽象** | 同上 | ✅ 完成 | `ShooterAcceptanceLab`, `ShooterAcceptanceSession`, `ShooterAcceptanceCatalog` |
| **常量** | `com.abilitykit.demo.shooter.share` | ✅ 完成 | `ShooterGameplay`（RoomType, WorldType, GameplayId=2, TickRate=30, MaxPlayers=4, PlayerHp=3） |
| **调优** | 同 runtime | ✅ 完成 | `ShooterBattleTuning`（PlayerSpeed=5, BulletSpeed=12, BulletLifeFrames=60, HitRadius=0.45, HitDamage=1） |

### 2.2 待完善的层

| 层 | 说明 |
|---|---|
| **Editor 窗口** | 已有 `com.abilitykit.demo.shooter.editor` 包与 `ShooterDemoWindow`，可继续增强诊断与矩阵批量入口 |
| **Unity 渲染** | 已有 `ShooterEditorSceneViewSink` SceneView 绘制与 PlayMode `UnityShooterGameObjectViewSink`，仍缺少正式场景/Prefab 化展示 |
| **输入采集** | 已有 `ShooterEditorInputProvider` 与 PlayMode 输入源，鼠标瞄准、动作占位和输入诊断仍可增强 |
| **场景预制** | 无 Shooter 专用 Scene/Prefab，当前主要通过 EditorWindow/SceneView 与 PlayMode host 驱动 |

## 3. 整体架构

```
┌─────────────────────────────────────────────────────────────────────┐
│                    Unity Editor / PlayMode                          │
│                                                                     │
│  ┌──────────────────────┐    ┌──────────────────────────────────┐  │
│  │  ShooterDemoWindow   │    │       Game View (SceneView)       │  │
│  │  (EditorWindow)      │    │                                    │  │
│  │                      │    │  ┌─────────────┐ ┌─────────────┐  │  │
│  │  ┌────────────────┐  │    │  │ Client World │ │ Auth World  │  │  │
│  │  │ Sync Mode      │  │    │  │ (Predicted)  │ │ (Optional)  │  │  │
│  │  │ Network Env    │  │    │  │              │ │              │  │  │
│  │  │ Player Config  │  │    │  │  Player ●    │  │  Player ○   │  │  │
│  │  │ Auth World ☐   │  │    │  │  Bullet →    │  │  Bullet →   │  │  │
│  │  │ Runtime Stats  │  │    │  │              │ │              │  │  │
│  │  └────────────────┘  │    │  └─────────────┘ └─────────────┘  │  │
│  │                      │    │                                    │  │
│  │  ┌────────────────┐  │    │  ┌──────────────────────────────┐  │  │
│  │  │ ▶ Start        │  │    │  │  ShooterMonoViewSink         │  │  │
│  │  │ ■ Stop         │  │    │  │  (IShooterSnapshotViewSink)  │  │  │
│  │  │ ‖ Pause        │  │    │  │  - Create/Remove GameObjects │  │  │
│  │  │ ▶ Step         │  │    │  │  - Update Transform          │  │  │
│  │  └────────────────┘  │    │  │  - HP Bar / Score Label      │  │  │
│  │                      │    │  │  - Bullet Trail               │  │  │
│  │  ┌────────────────┐  │    │  │  - Hit Flash VFX             │  │  │
│  │  │ Diagnostics    │  │    │  └──────────────────────────────┘  │  │
│  │  │ Frame: 1234    │  │    │                                    │  │
│  │  │ Divergence: 0  │  │    │  ┌──────────────────────────────┐  │  │
│  │  │ Rollbacks: 3   │  │    │  │  ShooterMonoInputProvider    │  │  │
│  │  └────────────────┘  │    │  │  (Keyboard → Command)        │  │  │
│  └──────────────────────┘    │  └──────────────────────────────┘  │  │
│                               └──────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
                    │                    │
                    ▼                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│                     Pure C# Layer (已存在)                           │
│                                                                     │
│  ShooterAcceptanceSession                                           │
│  ├── ShooterBattleRuntimePort (Client World)                        │
│  ├── ShooterPresentationFacade (Client Presentation)                │
│  ├── IShooterClientSyncController (PredictRollback / AuthInterp)    │
│  ├── ShooterBattleRuntimePort (Authoritative World, Optional)       │
│  └── ShooterPresentationFacade (Auth Presentation, Optional)        │
│                                                                     │
│  数据流:                                                             │
│  Input → ShooterPlayerCommand → SyncController.Tick()               │
│       → RuntimePort.SubmitInput() → ShooterBattleSimulation.Tick()  │
│       → RuntimePort.GetSnapshot() → ShooterStateSnapshotPayload     │
│       → PresentationFacade.ApplySnapshot() → ShooterSnapshotViewBatch│
│       → IShooterSnapshotViewSink.ApplySnapshot()                    │
└─────────────────────────────────────────────────────────────────────┘
```

## 4. 新增包结构

### 4.1 `com.abilitykit.demo.shooter.editor`（新建）

```
com.abilitykit.demo.shooter.editor/
├── package.json
└── Editor/
    ├── com.abilitykit.demo.shooter.editor.asmdef
    ├── Windows/
    │   ├── ShooterDemoWindow.cs              ← 主 Editor 窗口
    │   ├── ShooterDemoWindow.Session.cs      ← 会话管理面板
    │   ├── ShooterDemoWindow.Network.cs      ← 网络环境面板
    │   ├── ShooterDemoWindow.Diagnostics.cs  ← 诊断信息面板
    │   └── ShooterDemoWindow.SceneView.cs    ← SceneView Gizmo 绘制
    ├── Sink/
    │   └── ShooterEditorSceneViewSink.cs     ← Editor 下的 SceneView 渲染
    ├── Input/
    │   └── ShooterEditorInputProvider.cs     ← Editor 窗口键盘输入采集
    └── Gizmo/
        ├── ShooterPlayerGizmoDrawer.cs       ← Player 圆形 + 朝向 + HP
        ├── ShooterBulletGizmoDrawer.cs       ← Bullet 线段 + 速度方向
        └── ShooterDivergenceGizmoDrawer.cs   ← 漂移连线（Client↔Auth）
```

**package.json 依赖：**
```json
{
  "name": "com.abilitykit.demo.shooter.editor",
  "version": "0.0.1",
  "displayName": "AbilityKit Demo Shooter Editor",
  "unity": "2022.3",
  "description": "Shooter demo editor tools for network sync demonstration.",
  "author": { "name": "AbilityKit" },
  "dependencies": {
    "com.abilitykit.demo.shooter.runtime": "0.0.1",
    "com.abilitykit.demo.shooter.view.runtime": "0.0.1",
    "com.abilitykit.demo.shooter.share": "0.0.1",
    "com.abilitykit.network.runtime": "0.0.1"
  }
}
```

**asmdef 引用：**
```json
{
  "name": "AbilityKit.Demo.Shooter.Editor",
  "references": [
    "AbilityKit.Demo.Shooter.Runtime",
    "AbilityKit.Demo.Shooter.View.Runtime",
    "AbilityKit.Demo.Shooter.Share",
    "AbilityKit.Network.Runtime",
    "AbilityKit.Core",
    "AbilityKit.Core.Editor"
  ],
  "includePlatforms": ["Editor"]
}
```

## 5. Editor 窗口设计

### 5.1 ShooterDemoWindow — 主窗口

参考 [`EditorGameFlowPumpWindow`](../Unity/Packages/com.abilitykit.demo.moba.editor/Editor/Preview/EditorGameFlowPumpWindow.cs) 的 Editor 驱动模式和 [`FrameSyncTestWindow`](../Unity/Packages/com.abilitykit.demo.moba.editor/Editor/FrameSync/FrameSyncTestWindow.cs) 的分面板布局。

```
┌─────────────────────────────────────────────────────────────────┐
│ Shooter Demo                                          [□][×]   │
├─────────────────────────────────────────────────────────────────┤
│ [▶ 启动] [■ 停止] [‖ 暂停] [▶ 单步]    倍速: [===●===] 1.0x   │
├──────────────────────┬──────────────────────────────────────────┤
│ ⚙ 配置               │ 📊 诊断                                  │
│                      │                                          │
│ 同步模式:            │ Frame: 1234                              │
│ [PredictRollback ▼]  │ Tick: 33.3ms                             │
│                      │ Players: 2  Bullets: 3                   │
│ 网络环境:            │ Rollbacks: 5                             │
│ [Ideal ▼]            │ MaxDivergence: 0.12                      │
│                      │                                          │
│ 延迟: [===●===] 0ms  │ ┌────────────────────────────────────┐  │
│ 抖动: [===●===] 0ms  │ │ Player #1  HP:3  Score:2  (1.2,3.4)│  │
│ 丢包: [===●===] 0%   │ │ Player #2  HP:1  Score:0  (5.6,7.8)│  │
│ 乱序: [===●===] 0%   │ │ Bullet #1  Owner:1  →(0.8,0.6)     │  │
│                      │ │ Bullet #2  Owner:2  →(-0.3,0.9)     │  │
│ ☑ 显示权威世界       │ │ Bullet #3  Owner:1  →(0.1,0.5)     │  │
│ ☑ 显示漂移连线       │ └────────────────────────────────────┘  │
│                      │                                          │
│ 玩家配置:            │ 📋 事件日志                               │
│ 玩家数: [2▼]         │                                          │
│ 初始HP: [3]          │ [Hit] P1→P2 Bullet#5 at (3.2,4.1)     │
│ 随机种子: [3901]     │ [Fire] P1 Bullet#5 at (1.0,2.0)       │
│                      │ [Hit] P2→P1 Bullet#4 at (1.5,3.0)     │
│ ☑ 键盘输入(WASD+Space)│                                         │
│ 控制玩家: [1▼]       │                                          │
├──────────────────────┴──────────────────────────────────────────┤
│ 💡 使用 WASD 移动 / 鼠标瞄准 / Space 开火                       │
│    Editor 窗口需获得焦点才能接收键盘输入                           │
└─────────────────────────────────────────────────────────────────┘
```

### 5.2 窗口分区职责

| 分区 | 文件 | 职责 |
|------|------|------|
| **工具栏** | `ShooterDemoWindow.cs` | 启动/停止/暂停/单步/倍速，参考 `EditorGameFlowPumpWindow` 的 `EditorApplication.update` 驱动 |
| **配置面板** | `ShooterDemoWindow.Session.cs` | 同步模式下拉、网络环境下拉+滑条、玩家配置、权威世界开关 |
| **网络面板** | `ShooterDemoWindow.Network.cs` | 5 个预设快速切换 + 自定义滑条（延迟/抖动/丢包/乱序/重复），运行时可调 |
| **诊断面板** | `ShooterDemoWindow.Diagnostics.cs` | Frame/实体数/回滚次数/漂移距离/事件日志 |
| **SceneView** | `ShooterDemoWindow.SceneView.cs` | 注册 `SceneView.duringSceneGui`，委托 Gizmo 绘制 |

### 5.3 Editor 驱动模式

参考 [`EditorGameFlowPumpWindow`](../Unity/Packages/com.abilitykit.demo.moba.editor/Editor/Preview/EditorGameFlowPumpWindow.cs#L136) 的模式：

```csharp
// 核心驱动循环
private void OnEditorUpdate()
{
    if (!_running || _paused) return;

    var now = EditorApplication.timeSinceStartup;
    var delta = (float)((now - _lastTime) * _timeScale);
    _lastTime = now;

    // 1. 采集输入
    var command = _inputProvider.PollInput(_controlledPlayerId);
    if (command.HasValue)
    {
        _session.Controller.SubmitLocalInput(in command.Value);
    }

    // 2. 驱动同步控制器 Tick
    var tickResult = _session.Controller.Tick(delta);

    // 3. 如果启用权威世界，同步驱动
    if (_session.HasAuthoritativeWorld)
    {
        _session.TickAuthoritativeWorld(delta);
    }

    // 4. 采集诊断数据
    _diagnostics.Capture(tickResult, _session);

    // 5. 请求 SceneView 重绘
    SceneView.RepaintAll();
    Repaint();
}
```

**关键设计决策：**
- **非 PlayMode 也可运行** — 使用 `EditorApplication.update` 驱动，不依赖 `MonoBehaviour.Update`
- **SceneView 渲染** — 使用 `SceneView.duringSceneGui` + `Handles`/`Gizmos` 绘制，不污染 Game 窗口
- **PlayMode 兼容** — 如果在 PlayMode 下，也可以通过 `MonoBehaviour` 驱动（可选）

## 6. SceneView 渲染设计

### 6.1 ShooterEditorSceneViewSink

实现 [`IShooterSnapshotViewSink`](../Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Presentation/View/IShooterSnapshotViewSink.cs) 接口，将 `ShooterSnapshotViewBatch` 转化为 SceneView Gizmo 绘制数据：

```csharp
public sealed class ShooterEditorSceneViewSink : IShooterSnapshotViewSink
{
    // 缓存最新快照用于 SceneView.duringSceneGui 回调绘制
    private ShooterSnapshotViewBatch _clientBatch;
    private ShooterSnapshotViewBatch? _authorityBatch;
    private bool _showDivergence;

    public void ApplySnapshot(in ShooterSnapshotViewBatch batch)
    {
        _clientBatch = batch;
    }

    public void ApplyAuthoritySnapshot(in ShooterSnapshotViewBatch batch)
    {
        _authorityBatch = batch;
    }

    public void Clear() { /* 重置缓存 */ }

    // 由 ShooterDemoWindow.SceneView 在 duringSceneGui 中调用
    public void DrawSceneView(SceneView sceneView)
    {
        DrawGrid();
        DrawEntities(in _clientBatch, isClient: true);

        if (_authorityBatch.HasValue && _showDivergence)
        {
            DrawEntities(in _authorityBatch.Value, isClient: false);
            DrawDivergenceLines(in _clientBatch, in _authorityBatch.Value);
        }
    }
}
```

### 6.2 Gizmo 绘制规范

| 实体类型 | Client World | Authority World | 漂移指示 |
|---------|-------------|-----------------|---------|
| **Player** | 绿色圆盘 + 白色朝向箭头 + HP数字 | 蓝色虚线圆盘（半透明） | 红色虚线连接对应 Player |
| **Bullet** | 黄色线段（速度方向） | 蓝色虚线线段（半透明） | 红色虚线连接 |
| **命中事件** | 红色闪烁圆圈（1帧） | — | — |
| **开火事件** | 橙色小圆点（发射点） | — | — |

### 6.3 坐标系

- 战斗逻辑使用 2D 坐标 (X, Y)
- SceneView 映射：X → X 轴, Y → Z 轴（俯视图），Y 轴固定为 0
- 战场范围：默认 -10 到 10 的正方形区域，用灰色网格绘制

## 7. 输入采集设计

### 7.1 ShooterEditorInputProvider

```csharp
public sealed class ShooterEditorInputProvider
{
    public int ControlledPlayerId { get; set; } = 1;
    public bool EnableKeyboardInput { get; set; } = true;

    // 在 Editor 窗口 OnGUI 中采集当前按键状态
    // 使用 EventType.KeyDown/KeyUp 追踪持续按键
    private readonly HashSet<KeyCode> _keysDown = new HashSet<KeyCode>();

    public ShooterPlayerCommand? PollInput(int playerId)
    {
        if (!EnableKeyboardInput || _keysDown.Count == 0)
            return null;

        var moveX = 0f; var moveY = 0f;
        if (_keysDown.Contains(KeyCode.W)) moveY += 1f;
        if (_keysDown.Contains(KeyCode.S)) moveY -= 1f;
        if (_keysDown.Contains(KeyCode.A)) moveX -= 1f;
        if (_keysDown.Contains(KeyCode.D)) moveX += 1f;

        // 鼠标位置 → 瞄准方向（SceneView 世界坐标）
        var aimX = 0f; var aimY = 1f; // 默认朝上
        // 通过 SceneView.camera.ScreenToWorldPoint 计算鼠标相对玩家方向

        var fire = _keysDown.Contains(KeyCode.Space);

        if (moveX == 0f && moveY == 0f && !fire)
            return null;

        return ShooterClientInputBuilder.CreateCommand(
            playerId, moveX, moveY, aimX, aimY, fire);
    }
}
```

**输入映射：**

| 按键 | 功能 |
|-----|------|
| W/A/S/D | 移动（上/左/下/右） |
| 鼠标位置 | 瞄准方向（相对于玩家位置） |
| Space | 开火 |
| Q | 切换控制的玩家 |

## 8. 网络环境运行时管理

### 8.1 Editor 窗口中的网络参数

网络环境参数通过 Editor 窗口的滑条实时修改，调用 [`ShooterAcceptanceSession.ApplyNetwork()`](../Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Client/Synchronization/ShooterAcceptanceLab.cs) 方法：

```
┌─ 网络环境 ─────────────────────────────────┐
│ 预设: [Ideal ▼] [Lan] [4G] [Cross] [Poor]  │
│                                              │
│ 延迟 (ms):    [========●========] 0          │
│ 抖动 (ms):    [========●========] 0          │
│ 丢包率:       [========●========] 0.000      │
│ 乱序率:       [========●========] 0.000      │
│ 重复率:       [========●========] 0.000      │
│                                              │
│ [应用修改]  [重置为预设]                       │
└──────────────────────────────────────────────┘
```

**交互逻辑：**
1. 选择预设 → 填充滑条值 → 自动调用 `ApplyNetwork()`
2. 手动拖动滑条 → 实时调用 `ApplyNetwork()` 构建新 `NetworkConditionProfile`
3. "重置为预设" → 恢复当前预设的默认值

### 8.2 与 AcceptanceLab 的集成

```csharp
// Editor 窗口持有 ShooterAcceptanceSession
private ShooterAcceptanceSession _session;

// 网络参数变更时
private void ApplyNetworkParameters()
{
    var profile = new NetworkConditionProfile(
        _latencyMs, _jitterMs, _packetLoss, _outOfOrder, _duplicateRate);
    _session.ApplyNetwork(in profile, _networkDisplayName);
}
```

## 9. 多 World 对比设计

### 9.1 启用流程

```
配置面板:
  ☑ 显示权威世界（CompareMode）
  ☑ 显示漂移连线

启动时:
  var session = ShooterAcceptanceLab.Create(
      syncModel: selectedSyncMode,
      networkProfile: selectedNetwork,
      enableAuthoritativeWorld: compareModeEnabled);
```

### 9.2 SceneView 双 World 渲染

```
┌─────────────────────────────────────────────┐
│              SceneView (俯视)                │
│                                             │
│     ●P1─────────────────○P1'                │
│     (Client)            (Auth)              │
│          ↘ 0.12 units ↗                    │
│     ●P2                 ○P2'                │
│                                             │
│   →B1  →B2            →B1'                 │
│                                             │
│   ─ ─ ─ ─ 战场边界 ─ ─ ─ ─                 │
└─────────────────────────────────────────────┘

● = Client World 实体（实心）
○ = Authority World 实体（虚线半透明）
- - = 漂移连线（红色虚线，标注距离）
```

### 9.3 漂移数据来源

使用 [`ShooterAcceptanceSession.CompareWorlds()`](../Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Client/Synchronization/ShooterAcceptanceLab.cs) 获取 `ShooterWorldComparison`：

```csharp
if (_session.HasAuthoritativeWorld)
{
    var comparison = _session.CompareWorlds();
    _maxDivergence = comparison.MaxDistance;

    foreach (var div in comparison.Divergences)
    {
        // 在诊断面板显示每个 Player 的漂移
        // 在 SceneView 绘制漂移连线
    }
}
```

## 10. 诊断面板设计

### 10.1 实时统计

| 指标 | 来源 |
|------|------|
| 当前帧号 | `ShooterSnapshotViewModel.Frame` |
| 实体数 | `ShooterSnapshotViewBatch.EntityChangeCount` |
| Player 数/列表 | `ShooterSnapshotViewBatch` 中 Kind==Player 的 EntityChange |
| Bullet 数 | `ShooterSnapshotViewBatch` 中 Kind==Bullet 的 EntityChange |
| 回滚次数 | `SyncReconciliationReport` from `IClientSyncStrategy.GetReconciliationReport()` |
| 最大漂移 | `ShooterWorldComparison.MaxDistance` |
| 网络统计 | `NetworkConditioningStats` from `ShooterDemoHarnessCarrier` |

### 10.2 事件日志

`ShooterSnapshotViewBatch.Events` 包含 [`ShooterEventSnapshot`](../Unity/Packages/com.abilitykit.protocol.shooter/Runtime/StateSync/ShooterStateSnapshotCodec.cs#L56)：

| EventType | 含义 | 显示 |
|-----------|------|------|
| 1 | 命中 | `[Hit] P{source}→P{target} Bullet#{bullet} at ({x},{y}) dmg={value}` |
| 2 | 开火 | `[Fire] P{source} Bullet#{bullet} at ({x},{y})` |

## 11. 数据流总览

```
┌───────────────────────────────────────────────────────────────────┐
│                        Editor Window                              │
│                                                                   │
│  Keyboard/Mouse                                                   │
│       │                                                           │
│       ▼                                                           │
│  ShooterEditorInputProvider.PollInput()                           │
│       │                                                           │
│       ▼                                                           │
│  ShooterPlayerCommand ──→ IShooterClientSyncController            │
│                              .SubmitLocalInput()                  │
│                              .Tick(delta)                         │
│       │                                                          │
│       │  内部调用 ShooterBattleRuntimePort                         │
│       │  .SubmitInput() → ShooterBattleSimulation.Tick()          │
│       │  .GetSnapshot() → ShooterStateSnapshotPayload             │
│       ▼                                                          │
│  ShooterPresentationFacade.ApplyLocalPredictionSnapshot()         │
│       │                                                          │
│       ▼                                                          │
│  ShooterSnapshotViewModelMapper.Map()                             │
│       │                                                          │
│       ▼                                                          │
│  ShooterSnapshotViewBatch                                         │
│       │                                                          │
│       ├─→ ShooterEditorSceneViewSink.ApplySnapshot()              │
│       │       │                                                  │
│       │       ▼                                                  │
│       │   SceneView.duringSceneGui → Gizmo 绘制                  │
│       │                                                          │
│       └─→ ShooterDemoWindow.Diagnostics → 诊断面板 Repaint        │
│                                                                   │
│  (可选) Authoritative World:                                      │
│  ShooterAcceptanceSession.TickAuthoritativeWorld()                │
│       │                                                          │
│       ▼                                                          │
│  ShooterAcceptanceSession.CompareWorlds()                         │
│       │                                                          │
│       ▼                                                          │
│  ShooterWorldComparison → 漂移连线 + 诊断数据                     │
└───────────────────────────────────────────────────────────────────┘
```

## 12. 实现优先级与里程碑

### Phase 1: Editor 窗口骨架 + SceneView 渲染（最小可交互）

**目标：** 能在 Editor 中启动 Shooter 战斗，看到实体移动和子弹飞行。

| 任务 | 文件 | 说明 |
|------|------|------|
| 创建 editor 包 | `package.json`, `asmdef` | 依赖 shooter.runtime + view.runtime + share + network.runtime |
| 主窗口 | `ShooterDemoWindow.cs` | 工具栏 + `EditorApplication.update` 驱动 |
| SceneView Sink | `ShooterEditorSceneViewSink.cs` | 实现 `IShooterSnapshotViewSink`，缓存 batch |
| Player Gizmo | `ShooterPlayerGizmoDrawer.cs` | 圆盘 + 朝向 + HP |
| Bullet Gizmo | `ShooterBulletGizmoDrawer.cs` | 线段 + 速度方向 |
| 输入采集 | `ShooterEditorInputProvider.cs` | WASD + Space |
| 集成 | `ShooterDemoWindow.Session.cs` | 调用 `ShooterAcceptanceLab.Create()` + 驱动循环 |

**验收标准：** 打开 `Tools/AbilityKit/Shooter Demo` 窗口 → 点击启动 → SceneView 中看到 2 个 Player 圆盘 → WASD 控制移动 → Space 发射子弹 → 子弹命中后 HP 减少。

### Phase 2: 网络环境面板 + 同步模式切换

**目标：** 能实时切换网络环境和同步模式，观察不同同步效果。

| 任务 | 文件 | 说明 |
|------|------|------|
| 网络面板 | `ShooterDemoWindow.Network.cs` | 预设下拉 + 5 个滑条 + 运行时 `ApplyNetwork()` |
| 同步模式 | `ShooterDemoWindow.Session.cs` | 下拉选择 → 重建 Session |
| 诊断面板 | `ShooterDemoWindow.Diagnostics.cs` | Frame/实体数/回滚/事件日志 |

**验收标准：** 切换到 PoorWifi → 看到 Player 位置抖动/延迟 → 切换到 AuthoritativeInterpolation → 看到平滑插值效果。

### Phase 3: 多 World 对比 + 漂移可视化

**目标：** 能同时看到 Client 预测世界和权威世界，直观对比漂移。

| 任务 | 文件 | 说明 |
|------|------|------|
| 权威世界开关 | `ShooterDemoWindow.Session.cs` | `enableAuthoritativeWorld` checkbox |
| 漂移 Gizmo | `ShooterDivergenceGizmoDrawer.cs` | 红色虚线连接 + 距离标注 |
| 双 World Sink | `ShooterEditorSceneViewSink.cs` | 增加 `ApplyAuthoritySnapshot()` |
| 诊断增强 | `ShooterDemoWindow.Diagnostics.cs` | MaxDivergence + Per-Player 漂移表 |

**验收标准：** 勾选"显示权威世界" → 看到 Client（绿色实心）和 Auth（蓝色虚线）→ 在 PoorWifi 下看到红色漂移连线 → 切换 Ideal 后漂移消失。

### Phase 4: PlayMode 支持（可选增强）

**目标：** 在 PlayMode 下也能运行，使用 MonoBehaviour 驱动 + 真实 GameObject 渲染。

| 任务 | 说明 |
|------|------|
| `ShooterMonoViewSink` | MonoBehaviour 实现 `IShooterSnapshotViewSink`，创建/销毁 GameObject |
| `ShooterMonoInputProvider` | MonoBehaviour 采集 Input.GetAxis/GetButton |
| `ShooterDemoRunner` | MonoBehaviour 持有 `ShooterAcceptanceSession`，在 `Update()` 中驱动 |
| Shooter Demo Scene | 包含 Camera + ShooterDemoRunner + Canvas(HUD) |

## 13. 与现有代码的边界

### 13.1 不修改的已有代码

| 包 | 原因 |
|---|---|
| `com.abilitykit.protocol.shooter` | 协议层稳定，不需要改动 |
| `com.abilitykit.demo.shooter.runtime` | 玩法逻辑完整，不需要改动 |
| `com.abilitykit.demo.shooter.share` | 常量定义稳定 |
| `com.abilitykit.network.runtime` | 框架层，不因示例改动 |

### 13.2 可能需要小幅扩展的已有代码

| 文件 | 扩展内容 | 影响 |
|------|---------|------|
| `ShooterAcceptanceLab.cs` | 可能需要增加 `Tick()` 便捷方法封装输入+Tick+快照的完整循环 | 向后兼容，纯新增 |
| `ShooterAcceptanceSession.cs` | 可能需要暴露更多诊断数据（如 `GetReconciliationReport()`） | 向后兼容，纯新增 |
| `IShooterSnapshotViewSink.cs` | 已有接口足够，不需要改动 | — |

### 13.3 纯新增代码

所有 Editor 窗口、Gizmo 绘制、输入采集代码都在新的 `com.abilitykit.demo.shooter.editor` 包中，不触碰任何已有包。

## 14. 关键设计决策

| 决策 | 选择 | 理由 |
|------|------|------|
| **渲染方式** | SceneView Gizmo（非 GameObject） | 不污染场景、不需要 Prefab、Editor 下性能好、参考 `EditorGameFlowPumpWindow` 成功模式 |
| **驱动方式** | `EditorApplication.update` | 非 PlayMode 可运行、参考 `EditorGameFlowPumpWindow` 模式 |
| **输入方式** | Editor 窗口键盘事件 | 不依赖 Input System 包、Editor 原生支持 |
| **网络管理** | Editor 窗口滑条 | 不影响 Game 窗口、运行时可调、直观 |
| **包结构** | 新建 `com.abilitykit.demo.shooter.editor` | 遵循 `com.abilitykit.demo.moba.editor` 的包分离模式 |
| **同步控制** | 复用 `ShooterAcceptanceLab` | 已有完整的 Session 管理、网络切换、多 World 对比能力 |
| **坐标系** | 2D 俯视 (X→X, Y→Z) | Shooter 是 2D 顶视角射击，SceneView 俯视最直观 |
