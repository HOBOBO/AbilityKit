# Game/App/Flow 状态机

## App 层状态机

位置：`view.runtime/Runtime/Game/App/Flow/`

```
App/Flow/
├── Boot/      BootPhase + 多个 OnGUIFeature（Boot/DemoLobby/FormalLobby/RootDebug）+ LobbyBattleEntrySelection
└── Core/      BattleWorldModule / BattleWorldScopeHost / BattleAssetLoadCoordinator / FlowStateMachineBuilder
              / FlowGateProvider / MobaFlowPhaseIds / MobaFlowActions / GamePhaseContracts
              / IBattleSessionFeature / Multiplayer/MultiplayerRoomFlowController ...（50+ 类）
```

## 关键类

- `GameManager`（`App/Entry/`）— Unity 入口 MonoBehaviour
- `BootPhase` — 启动阶段（菜单/ lobby 选择）
- `BattleWorldModule` — 战斗 world 模块
- `BattleWorldScopeHost` — 战斗 world 作用域 host
- `BattleAssetLoadCoordinator` — 资源加载协调
- `FlowStateMachineBuilder` / `FlowGateProvider` — 流程状态机构建
- `MobaFlowPhaseIds` / `MobaFlowActions` — Phase ID 与 Action 常量
- `GamePhaseContracts` — 阶段契约
- `IBattleSessionFeature` — 战斗会话 Feature 接口（`BattleSessionFeature` 实现）

## Multiplayer

`App/Flow/Core/Multiplayer/MultiplayerRoomFlowController` — 多人房间流程控制（与 `RoomGatewaySessionFlow` 协作，详见 host-extension skill）。

## App → Battle 切换

```
BootPhase（选 demo/formal lobby）
    ↓
BattleWorldModule（装载战斗 world）
    ↓
BattleWorldScopeHost + FlowStateMachineBuilder
    ↓
IBattleSessionFeature（BattleSessionFeature 实例化，详见 state-handles-controllers）
    ↓
进入 Battle/Client 子树（Session / Gateway / Snapshot / Net / Presentation / View）
```

## IBattleSessionFeature 接口

`BattleSessionFeature` 实现的接口之一（在 `App/Flow/Core/`），是 view.runtime 与外部（App 层）沟通战斗会话的契约。其他接口：`ISessionDispatchersRuntime` / `ISessionEventsRuntime` / `ISessionPlanRuntime` / `ISessionReplayRuntime` / `ISessionNetAdapterRuntime` / `ISessionTickLoopRuntime` / `ISessionLifecycleRuntime` / `ISessionGatewayRuntime` / `ISessionSnapshotRoutingRuntime` 等。
