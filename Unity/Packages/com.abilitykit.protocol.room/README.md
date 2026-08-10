# com.abilitykit.protocol.room

> Room-gateway 线协议包。定义房间会话的全部 wire 类型 + opcodes + 编解码。shooter 与 moba 共用。

- **版本**：0.0.1
- **依赖**：`com.abilitykit.core`、`com.abilitykit.protocol`

## 内容

### Opcodes（`RoomGatewayOpCodes`）
| OpCode | 值 | 方向 |
|--------|-----|------|
| GuestLogin | 100 | C→S |
| AccountLogin | 111 | C→S |
| CreateRoom | 101 | C→S |
| JoinRoom | 102 | C→S |
| LeaveRoom | 112 | C→S |
| SetReady | 104 | C→S |
| PickHero | 105 | C→S |
| StartBattle | 106 | C→S |
| SubmitBattleInput | 107 | C→S |
| RequestFullStateSync | 108 | C→S |
| RestoreRoom | 109 | C→S |
| SubscribeStateSync | 103 | C→S |
| RenewSession | 120 | C→S |
| AckReliableBattleEvents | 116 | C→S |
| BeginLoading | 112 | C→S |
| ReportAssetsLoaded | 113 | C→S |
| SnapshotPushed | 9002 | S→C |
| DeltaSnapshotPushed | 9003 | S→C |
| ReliableBattleEventsPushed | 9005 | S→C |
| RoomStateChanged | 9004 | S→C |

### Wire 类型（`[MemoryPackable]` struct）
- **认证**：WireRoomGuestLoginReq/Res、WireRoomAccountLoginReq/Res、WireRenewSessionReq
- **房间**：WireCreateRoomReq/Res、WireJoinRoomReq/Res、WireLeaveRoomReq、WireRoomReadyReq/Res
- **战斗**：WireSubmitBattleInputReq/Res、WireSubscribeStateSyncReq/Res、WireRequestFullStateSyncReq/Res、WireAckReliableBattleEventsReq/Res
- **快照**：WireStateSyncSnapshotPush、WireStateSyncActorSnapshot、WireReliableBattleEventPush
- **加载**：WireBeginLoadingReq/Res、WireReportAssetsLoadedReq/Res、WireReportLoadingProgressReq/Res
- **恢复**：WireRestoreRoomReq、WireRestoreRoomRes
- **通用**：WireRoomSnapshot（房间快照类）

### Codec
- **`WireRoomGatewayBinary`**：`Serialize<T>` / `Deserialize<T>` / `SerializeTransient<T>`（pooled buffer 变体）
- **`ReusableMemoryPackSerializationBuffer`**：`IBufferWriter<byte>` 池化序列化缓冲区（热路径避免 `new byte[]`）

## 相关
- 序列化模型 → `com.abilitykit.protocol` README
- 房间会话 → `com.abilitykit.network.room`
- Moba/Shooter 专属协议 → `com.abilitykit.protocol.moba` / `.shooter`
