# com.abilitykit.network.client

两连接多人战斗客户端宿主（`GatewayBattleClientHost`）：把"房间控制面连接 + 战斗数据面连接"的组装、凭证传递和推送绑定纪律收进一个组件，替代各 demo 各自手拼的 300-700 行接入外壳。

## 解决什么

两连接拓扑下每个接入方都要自己处理三件易错的事：

1. **凭证传递**：login 的 sessionToken、房间流程产出的 battleId/roomId 要正确喂给战斗连接。
2. **推送绑定纪律**：网关推送绑定按 observerKey（accountId:roomId）单槽 last-writer-wins —— 战斗面必须是唯一订阅者。宿主在挂接战斗面时自动跳过房间侧 `SubscribeStateSync`，把这条纪律从"约定"变成结构。
3. **生命周期配对**：两条连接的 Tick/Dispose 顺序。

## 用法

```csharp
var host = await GatewayBattleClientHost.EnterAsync(
    "127.0.0.1", 4000, "player-1",
    new RoomGatewayLaunchSpec(region, serverId, "yourgame", "title", maxPlayers, gameplayId, ruleSetId, configVersion, protocolVersion, "yourgame-world", clientId),
    configureBattle: config => config
        .UseRoomGatewayStateSyncInput(battleId, playerIdToUInt, worldIdToUlong)
        .WithSnapshotDeserializer(payload => /* decode snapshot push */));

host.Battle.StateSyncSnapshotPushed += snapshot => { /* apply */ };
host.Tick(realDeltaTime);   // 必须是真实墙钟间隔，不是游戏逻辑帧 delta
host.Dispose();
```

`configureBattle` 收到的是已预填网关地址 + 会话身份 + room-gateway 协议预设的 `NetworkBattleConfig`，只需补玩法回调（输入/快照/帧反序列化），不要在回调里调 `Build()`。

## 契约边界

- **一次性会话**：断线重连 = 新建宿主（或用 `Room.RoomClient` 自己走 staged restore）。
- `attachBattle: true` 时 `Room.Result.Subscribed` 为 `false` 是刻意的（战斗面独占订阅）。
- `Tick` 只认真实墙钟时间（心跳/重连计时直接累加该 delta）。
- Dispose 不发 LeaveRoom（服务端靠断连检测回收房间）；先释放战斗面再释放房间面。

## 依赖

network.sdk / network.room / network.battle / network.battle.config / network.runtime / protocol.room
