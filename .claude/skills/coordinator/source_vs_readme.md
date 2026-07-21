# README 与源码偏差对照

`com.abilitykit.coordinator/README.md` 与 `docs/ET-Integration-Guide.md` 严重过时。**skill 内容一律以源码为准**。

## 9 处主要偏差

| # | README 写的 | 源码实际 |
|---|------------|---------|
| 1 | `IBattleDriverHost`（含 `SetDriverHost`、`GetAllEntityStates()` 返回 `EntityState[]`） | **`ILogicWorldDriverBridge`**（含 `SetLogicWorldDriver`、返回 `SnapshotEntityState[]`、多了 `AdvanceFrame/Start/Stop`） |
| 2 | `ISessionCoordinatorHost` 4 个方法 | 实际 **5 个**，多了 `ConfigureWorldCreateOptions(in SessionConfig, WorldCreateOptions)` |
| 3 | 未提配置策略接口 | 实际有 `ISessionCoordinatorConfigPolicy.ConfigureSession(ref SessionConfig)` |
| 4 | `SyncMode { Lockstep/StateSync/Hybrid }`（3 个） | 实际 **4 个**：`Lockstep=0/SnapshotAuthority=1/StateSync=2/Hybrid=3` |
| 5 | `SessionConfig` 是 sealed class（属性形式） | 实际是 **struct**，全是字段（`public SessionId SessionId;` 等） |
| 6 | 工厂方法 `CreateServer/CreateClient/CreateForMode` | 实际是 `CreateLocal/CreateStateSyncClient/CreateHybrid/CreateHost` |
| 7 | `PlayerInput` 用 `InputType` 枚举 + `InputPayload` 字典 | 实际是 `int OpCode + byte[] Payload`（MemoryPack 序列化），含 `CreateMove/CreateSkill/CreateStop` 工厂 + 内置 `MoveInputPayload/SkillInputPayload` + `InputOpCodes` 常量（Move=1001/Skill=1002/Stop=1003/UseItem=1004/Ping=1005） |
| 8 | `SessionHooks` 字段列表不全 | 实际还有 `OnViewBinderReady/OnViewsRebound/OnViewFrameAligned` 三个视图钩子，以及 `Clear()` |
| 9 | 文件结构列了 `IBattleDriverHost.cs`/`SessionState.cs` | 实际是 `ILogicWorldDriverBridge.cs`/`SessionEnums.cs`（`SessionState` 是 `SessionEnums.cs` 里的枚举） |

## README 漏掉的目录与文件

- `Transport/`（实际有 2 个文件：`IRemoteBattleSyncTransport.cs` + `CoordinatorInputSubmitBridge.cs`）
- `SubFeatures/ISessionHost.cs`
- `Core/ILogicWorldDriveGate.cs`
- `Core/ILogicWorldDriverBridge.cs`
- `Core/ISpawnService.cs`
- `Core/ExistingWorldSessionCoordinatorHost.cs`
- `Data/CoordinatorPayloadCodec.cs`

## README 示例代码的过时调用

- `_coordinator.SetDriverHost(...)` → 实际 `SetLogicWorldDriver(...)`
- README 第三节"coordinator 调 driver.GetAllEntityStates" → 实际驱动方向反过来：adapter 通过 driver 推进帧

## 处理建议

- 看源码，不看 README
- 若 README/ET-Integration-Guide 与本 skill 冲突，以本 skill 为准
- 若代码与本 skill 冲突，以代码为准（skill 也可能滞后）
