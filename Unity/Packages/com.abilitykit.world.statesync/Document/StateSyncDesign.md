# com.abilitykit.world.statesync：状态同步与客户端预测

> **阅读对象**：希望了解状态同步系统如何设计的开发者
>
> **文档目标**：让你理解"什么是状态同步与客户端预测"、"Rollback 机制如何工作"、"预测协调器的两种风格"

---

## 一、设计理念

### 1.1 状态同步的核心问题

状态同步中，客户端需要解决三个核心问题：

| 问题 | 表现 | 解决方案 |
|------|------|----------|
| **延迟感知** | 玩家操作后等待服务器确认才看到结果 | 客户端本地预测 |
| **预测错误** | 预测结果与服务器不一致 | 回滚（Rollback） |
| **带宽成本** | 每帧传输所有实体状态 | 增量同步 + 压缩 |

### 1.2 本包的职责

`com.abilitykit.world.statesync` 负责：

- **状态快照的序列化与反序列化**（`WorldStateSnapshot`）
- **Rollback 状态管理**（`IStateManager` / `StateManager`）
- **客户端预测**（两种风格：per-entity 和 global ECS-style）
- **状态差异检测**（`IStateDiffProvider`）
- **状态哈希校验**（`StateHashValidator`）
- **帧缓冲**（`SnapshotBuffer`）

### 1.3 与其他包的关系

```
world.networkfragments          world.snapshot
┌──────────────────┐     ┌──────────────────────┐
│ FramePacket      │     │ FrameSnapshotDispatcher│
│ ISnapshotEnvelope│────▶│ 快照路由与解码        │
└──────────────────┘     └──────────────────────┘
          │                        │
          ▼                        │
┌──────────────────┐               │
│ host.extension    │               │
│ Session/         │               │
│ FramePacketNetAdapter            │
└──────────────────┘               │
          │                        │
          ▼                        ▼
┌──────────────────────────────────────────┐
│          world.statesync                 │
│                                          │
│  ┌─────────────┐  ┌─────────────────┐  │
│  │ StateManager │  │PredictionCoordinator│  │
│  │ (Rollback)  │  │ /ClientPrediction │  │
│  └─────────────┘  └─────────────────┘  │
│                                          │
│  ┌─────────────┐  ┌─────────────────┐  │
│  │StateSnapshot│  │StateHashValidator│  │
│  │ (序列化)   │  │ (哈希校验)      │  │
│  └─────────────┘  └─────────────────┘  │
└──────────────────────────────────────────┘
          │
          ▼
┌──────────────────┐
│ game.battle.runtime│
│ IBattleLogicTransport│
└──────────────────┘
```

---

## 二、核心类型总览

```
StateSync/
├── Buffer/
│   ├── InputBuffer.cs         # 通用输入缓冲（按帧号存取）
│   └── SnapshotBuffer.cs       # 线程安全的快照存储
├── Client/
│   ├── ClientPredictionModule.cs   # Per-entity 预测管理
│   ├── EntityPredictionState.cs
│   ├── IClientPredictionModule.cs  # 主预测接口
│   ├── IPredictableEntity.cs      # 实体预测契约
│   └── Handlers/                   # 预测处理器
├── Compression/
│   └── DeltaCompressor.cs      # 增量压缩
├── Core/
│   ├── IStateManager.cs         # 状态管理器接口
│   ├── StateManager.cs          # Rollback 状态管理实现
│   ├── IRollbackable.cs        # 可回滚实体契约
│   ├── IRollbackState.cs       # 回滚状态接口
│   ├── RollbackState.cs        # 回滚状态实现
│   ├── IStateDiff.cs          # 状态差异接口
│   ├── IStateDiffProvider.cs   # 差异提供者接口
│   ├── StateDiff.cs           # 差异实现
│   └── ServerGameState.cs     # 服务器游戏状态
├── Diff/
│   ├── StateDiff.cs           # 增量状态
│   └── StateDiffProvider.cs
├── Events/
│   └── ClientEvents.cs        # 客户端事件（StateChange、Rollback）
├── Hash/
│   ├── StateHash.cs           # 状态哈希
│   └── StateHashValidator.cs  # 哈希校验
├── Network/
│   ├── ISnapshotPacker.cs    # 快照序列化接口
│   ├── SnapshotMessage.cs
│   └── MemoryPackSnapshotPacker.cs
├── Prediction/
│   ├── IPredictionCoordinator.cs  # 全局预测协调器（ECS 风格）
│   ├── KeyFrameStrategy.cs
│   └── Core/
│       ├── PredictionCoordinator.cs  # 全局协调器实现
│       ├── PredictionCore.cs
│       └── StateSlots.cs           # 通用键值状态存储
└── Snapshot/
    ├── IHashableState.cs     # 业务层哈希提供者
    ├── StateHashComputer.cs
    └── WorldStateSnapshot.cs  # MemoryPackable 快照（Vec3/Quat）
```

---

## 三、Rollback 机制（IStateManager）

### 3.1 核心接口

```csharp
namespace AbilityKit.Ability.StateSync
{
    /// <summary>
    /// 状态管理器
    /// 负责管理所有可回滚实体的状态快照
    /// </summary>
    public interface IStateManager
    {
        /// <summary>注册可回滚实体</summary>
        void RegisterRollbackable(IRollbackable entity);

        /// <summary>注销实体</summary>
        void UnregisterRollbackable(long entityId);

        /// <summary>在指定帧号捕获所有实体状态</summary>
        void CaptureState(int frame);

        /// <summary>尝试恢复到指定帧</summary>
        bool TryRestore(int frame);

        /// <summary>计算两帧之间的差异</summary>
        IStateDiff ComputeDiff(int fromFrame, int toFrame);

        /// <summary>获取指定帧的完整状态</summary>
        byte[] GetFullState(int frame);

        /// <summary>获取所有已捕获的帧号</summary>
        IReadOnlyList<int> GetCapturedFrames();

        /// <summary>清空历史记录</summary>
        void ClearHistory();
    }
}
```

### 3.2 可回滚实体契约

```csharp
namespace AbilityKit.Ability.StateSync
{
    /// <summary>
    /// 可回滚实体接口
    /// 需要参与回滚的实体实现此接口
    /// </summary>
    public interface IRollbackable
    {
        /// <summary>实体唯一标识</summary>
        long EntityId { get; }

        /// <summary>快照键值（用于 StateManager 内部索引）</summary>
        int SnapshotKey { get; }

        /// <summary>创建当前状态的回滚快照</summary>
        IRollbackState CreateRollbackState();

        /// <summary>从回滚快照恢复状态</summary>
        void RestoreFromRollbackState(IRollbackState state);
    }
}
```

### 3.3 Rollback 流程

```
服务器发送帧100状态
  → 客户端应用服务器状态（确认帧100）
  → 服务器发送帧105状态
  → 比较帧100和帧105的预测结果
  → 不匹配 → 触发回滚
  → TryRestore(100)
  → 从帧100重新执行到帧105
```

---

## 四、客户端预测：两种风格

本包提供了两套客户端预测方案，适用于不同的游戏类型。

### 4.1 风格一：Per-Entity 预测（IClientPredictionModule）

适用于 **MOBA、ARPG** 等实体数量多、每个实体需要独立预测逻辑的游戏。

```
┌─────────────────────────────────────────────────────────────┐
│                   IClientPredictionModule                     │
│                                                             │
│  每个实体独立的预测状态：                                     │
│  ┌─────────────────────────────────────────────────────┐  │
│  │ Entity: Hero_001                                     │  │
│  │   ├── PredictionState                                │  │
│  │   │     └── StateSlots (key-value)                   │  │
│  │   ├── Handlers                                       │  │
│  │   │     ├── MovementHandler → Predict/Validate       │  │
│  │   │     └── SkillHandler → Predict/Validate         │  │
│  │   └── 独立的预测/验证生命周期                         │  │
│  └─────────────────────────────────────────────────────┘  │
│  ┌─────────────────────────────────────────────────────┐  │
│  │ Entity: Hero_002 ...                                 │  │
│  └─────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

**接口**：`IClientPredictionModule` 提供 `RegisterEntity`、`UnregisterEntity`、`SubmitInput`、`ApplyServerSnapshot`、事件 `OnPredictionRejected` 等。

**实体实现**：`IPredictableEntity` 提供 `GetPredictionHandlers()` 返回该实体专属的 handler 列表，`GetStateSlots()` 返回该实体的状态槽。

### 4.2 风格二：全局 ECS 预测（IPredictionCoordinator）

适用于 **FPS、动作游戏** 等使用 ECS 风格、按系统划分 Handler 的游戏。

```
┌─────────────────────────────────────────────────────────────┐
│                   IPredictionCoordinator                     │
│                                                             │
│  全局共享的 StateSlots：                                    │
│  ┌─────────────────────────────────────────────────────┐  │
│  │ StateSlots (共享 key-value store)                    │  │
│  │   "Hero_001/Position" → Vec3                       │  │
│  │   "Hero_001/Health"    → float                   │  │
│  │   "Hero_002/Position" → Vec3                       │  │
│  └─────────────────────────────────────────────────────┘  │
│                                                             │
│  全局 Handler（按系统划分）：                               │
│  ┌─────────────────────────────────────────────────────┐  │
│  │ MovementHandler → Predict / Validate / ApplyServer  │  │
│  │ CooldownHandler → Predict / Validate / ApplyServer  │  │
│  └─────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

**接口**：

```csharp
public interface IPredictionCoordinator
{
    int LocalPlayerId { get; }
    int CurrentPredictedFrame { get; }
    int ServerConfirmedFrame { get; }
    bool NeedsRollback { get; }

    void ProcessInput(IInputCommand input);
    void ProcessInputs(IReadOnlyList<IInputCommand> inputs);
    void ApplyServerSnapshot(int serverFrame, int objectId, StateSlots serverSlots);
    void Reset();
}
```

### 4.3 两种风格对比

| 维度 | IClientPredictionModule (Per-Entity) | IPredictionCoordinator (Global ECS) |
|------|----------------------------------------|----------------------------------|
| 适用游戏 | MOBA、ARPG | FPS、动作游戏 |
| Handler 作用域 | 每个实体独立 | 全局共享 |
| 状态存储 | 每实体独立 StateSlots | 全局共享 StateSlots |
| 灵活性 | 高（实体可定制） | 中（系统可定制） |
| 性能 | 实体多时可能有开销 | 批量处理更高效 |

---

## 五、快照序列化

### 5.1 WorldStateSnapshot

```csharp
public sealed class WorldStateSnapshot : IMessagePackFormattable, IBufferable
{
    public WorldId WorldId { get; }
    public int Frame { get; }
    public long Timestamp { get; }
    public bool IsFullSnapshot { get; }
    public Vec3[] Positions { get; }     // 位置（MemoryPack 序列化）
    public Quaternion[] Rotations { get; } // 旋转（MemoryPack 序列化）
}
```

### 5.2 ISnapshotPacker

```csharp
public interface ISnapshotPacker
{
    SnapshotMessage Pack(WorldStateSnapshot snapshot);
    WorldStateSnapshot Unpack(SnapshotMessage message);
    SnapshotMessage Compress(WorldStateSnapshot snapshot);
    WorldStateSnapshot Decompress(SnapshotMessage message);
}
```

### 5.3 IHashableState

业务层实现此接口提供自定义哈希：

```csharp
public interface IHashableState
{
    ulong ComputeHash();
}
```

---

## 六、状态哈希校验

`StateHashValidator` 定期校验客户端预测结果与服务器广播的哈希是否一致。不一致时触发回滚。

```
服务器广播 Frame(100) Hash = 0xABCD1234
  → 客户端比较本地哈希
  → 匹配 → 继续预测
  → 不匹配 → 调用 ApplyServerSnapshot(...)
  → 协调器自动恢复到帧100 → 按原帧批重演未确认输入
```

---

## 七、传输层适配

本包通过 `IBattleLogicTransport` 与具体网络实现解耦：

| 类型 | 位置 | 说明 |
|------|------|------|
| `IBattleLogicTransport` | `game.battle.runtime` | 通用战斗逻辑传输接口 |
| `CreateWorldRequest` | `game.battle.runtime` | 创建世界请求 |
| `SubmitInputRequest` | `game.battle.runtime` | 输入提交请求 |
| `NetworkTransport` | `game.battle.transport.runtime` | 基于 `INetworkClient` 的实现 |
| `INetworkClient` | `game.battle.transport.runtime` | 底层网络：请求响应 + 服务器推送 |

---

## 八、设计原则

| 原则 | 说明 |
|------|------|
| **确定性优先** | 所有参与回滚的系统必须实现 `IRollbackable` |
| **分层解耦** | Rollback 基础设施与具体游戏逻辑分离 |
| **零 GC** | 使用结构体和环缓冲避免 GC |
| **双预测风格** | 支持 MOBA（per-entity）和 FPS（global ECS）两种模式 |
