# Tiny 规则与确定性

源码唯一位置：`Unity/Packages/com.abilitykit.demo.tiny.logic/Runtime/Logic/`（命名空间 `AbilityKit.Demo.Tiny`）。由 Unity 客户端、`src/AbilityKit.Demo.Tiny.Core`（服务端链）与测试**共编译同一批文件**。

## TinyBattle（142 行）

```csharp
public sealed class TinyBattle
{
    public const string AssetKey  = "tiny:arena";
    public const string RulesKey  = "tiny:rules.v1";
    public const int InputOpCode  = 1;
    public const int MaxHp = 100, AttackDamage = 10;
    public const int AttackRangeSquared = 9, AttackCooldownFrames = 30;

    public int Frame { get; }
    public IReadOnlyCollection<TinyActorState> Actors { get; }

    public void AddPlayer(uint playerId, int x, int y);
    public bool ContainsPlayer(uint playerId);
    public TinyBattleState CaptureState();
    public void RestoreState(TinyBattleState state);
    public void Submit(uint playerId, TinyInput input);
    public void Tick();
    public uint ComputeHash();
}
```

配套两结构：`TinyActorState`（`PlayerId/X/Y/Hp/CooldownFrames`）、`TinyBattleState`（`Frame` + `Actors[]`）。

### Tick 语义

一次 `Tick()` = 恰好一帧。按 `PlayerId` 排序遍历（`_actors.Keys.ToArray()`），跳过已死 Actor：

1. 应用 `input.MoveX/MoveY` 作为**整数**位移；
2. `CooldownFrames--`；
3. 若 `input.Attack` 且冷却已达 0，扫描**另一个** Actor，用 `long` 计算 `dx*dx+dy*dy` 与 `AttackRangeSquared`(9，即距离 ≤3) 比较，命中则扣 `AttackDamage`、自身冷却置 30，**命中第一个目标后 `break`**；
4. 清空输入，`Frame++`。

`Submit` 是**覆盖式**——同一玩家同一 tick 内多次提交，最后一次生效。

### 不变式与校验

- `AddPlayer`：`playerId == 0` 或加入**第三个**不同 id 时抛 `ArgumentOutOfRangeException`。
- `RestoreState`：`frame >= 0`、1–2 个 Actor、无 `PlayerId == 0`、`0 <= Hp <= 100`、`0 <= CooldownFrames <= 30`、PlayerId 互异；否则抛 `ArgumentException`。
- `Submit`：`!input.IsValid` 或未知玩家抛 `ArgumentException`。

## TinyInput（29 行）

```csharp
public readonly struct TinyInput
{
    public TinyInput(sbyte moveX, sbyte moveY, bool attack);
    public sbyte MoveX { get; }  public sbyte MoveY { get; }  public bool Attack { get; }
    public bool IsValid => MoveX is >= -1 and <= 1 && MoveY is >= -1 and <= 1;
    public static TinyInput Decode(ReadOnlySpan<byte> payload);  // 恰好 3 字节
    public byte[] Encode();
}
```

线格式 **3 字节**：`{moveX+1, moveY+1, attack}`（两个三元轴偏移到 0..2，一个布尔 0/1）。`Decode` 要求 `Length == 3 && payload[0] <= 2 && payload[1] <= 2 && payload[2] <= 1`。

## TinyBattleStateCodec（53 行）

`PayloadOpCode = 31001`。定长布局（小端）：头部 `int32 frame` + `1 字节 actor 数`，随后每 Actor 20 字节（`uint PlayerId, int X, int Y, int Hp, int CooldownFrames`）。

`Encode` 要求 1–2 Actor；`Decode` 要求 `Length >= 5`、count ∈ 1..2、且 `Length == 5 + count*20` **精确相等**。

> **已知实现特征**：`Decode` 末尾会 `new TinyBattle().RestoreState(state)` 造一个**丢弃实例**，纯粹为了复用 `RestoreState` 的范围校验——于是越界值（如 `PlayerId == 0`）以 `ArgumentException` 形式从 `Decode` 抛出。`TinyTurnStateCodec.Decode` 同样如此。这是**有意复用**（`CorruptSnapshotCannotBecomeRecoveryBaseline` 依赖它），代价是热路径上一次多余分配。

## 确定性口径

- **纯整数**：全部 `int`/`uint`/`sbyte`，**零浮点**。移动是整数格 + 三元轴。
- **不依赖 `com.abilitykit.deterministic`**：不引入 `Fixed64`，Logic 装配 `references: []`。整个 Tiny 家族 grep 不到 `Fixed64`/`DeterministicMath`。导航类需求才需要定点，Tiny 的整数域天然确定。
- **零 `UnityEngine`**：Logic 与 FrameSync 两个装配都是 `noEngineReferences: true`，这是服务端能共编译的前提。**新增字段/方法时不得破坏它**。
- **顺序稳定**：`SortedDictionary<uint, TinyActorState>` 决定 Actor 遍历序；`_inputs` 是 `Dictionary` 但只按键索引、从不枚举。溢出风险点 `dx*dx+dy*dy` 已用 `long` 兜住。
- **哈希 FNV-1a 32 位**（offset `2166136261u`、prime `16777619u`），按 `Frame` → 排序后每 Actor 的 `PlayerId,X,Y,Hp,CooldownFrames` 顺序累积。
- **帧时钟确定 ≠ 全战斗确定**：`TinyBattle` 只管帧，`TinySyncSettings.TickRate` 只是外部会话用的常量；`TinyBattle` 自身没有 tick-rate 概念。

## 常量的消费方（改前先查）

| 常量 | 被谁读 |
|---|---|
| `TinySyncSettings.TickRate=30` / `InputDelayFrames=2` / `SubmissionLeadFrames=8` | `TinyFrameReplication`、`TinySyncMode`、`TinyViewModules`、`TinyBattleSessionTests`、`TinyFrameSmoke`、三个 `.Sample/Program.cs`、服务端 `TinyGameplay` |
| `TinySyncTemplates.{WorldType,State,Frame,Hybrid}` | `TinySyncMode`（客户端）、`TinyGameplay`（服务端）、`TinySyncModeProfileTests`、`TinyFrameSmoke`、`Record`/`LiveRecord` 样例 |
| `TinyBattle.{AssetKey,RulesKey}` | 客户端 `TinyAssetPreparation.Validate`、服务端 `TinyRoomGameplayAdapter` 构造 |

`AssetKey`/`RulesKey` 原先在服务端是写死字面量，**在途改动**（未提交）已抽成 `TinyBattle` 常量并让服务端引用之——值完全一致，无行为变化。见 [change_checklist.md](change_checklist.md)。

## 回合制规则 TinyTurnBattle（141 行）

唯一源码 `com.abilitykit.demo.tiny.turn/Runtime/Logic/TinyTurnBattle.cs`，命名空间 `AbilityKit.Demo.Tiny.Turn`。

| 常量 | 值 |
|---|---|
| `AssetKey` / `RulesKey` | `tiny:turn` / `tiny:turn.rules.v1` |
| `RoomType` / `WorldType` | `tiny-turn` / `tiny-turn-battle` |
| `StateTemplate` | `tiny-turn-state-authority` |
| `TickRate` / `InputOpCode` / `SnapshotOpCode` | 10 / 2 / 31002 |
| `MaxHp` | **2**（每次命中扣 1，两下分胜负） |

规则：`CanSubmit` 要求无胜者、`playerId == CurrentPlayerId`、无待处理输入；`Submit` 只接受 `payload.Length == 1 && payload[0] == 1`（**只有攻击，没有移动**）。`Tick` 无条件 `Frame++`，但**只有** `_pendingPlayerId != 0` 时才推进 `Turn`——所以网络空帧不推进回合。

`RestoreState` 不变式比实时版更强：`Frame >= 0`、`Turn >= 0`、**`Turn <= Frame`**、双 HP ∈ 0..2、`WinnerId <= 2`，且一致性——`WinnerId == 0` 时 `CurrentPlayerId` 必须为 1 或 2，否则必须为 0。

`TinyTurnStateCodec` 定长 **24 字节**：`int32 Frame, int32 Turn, uint32 CurrentPlayerId, int32 PlayerOneHp, int32 PlayerTwoHp, uint32 WinnerId`。哈希同样是 FNV-1a 同参数，覆盖六个字段。
