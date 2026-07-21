# view.runtime 包结构

根：`Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/`，命名空间 `AbilityKit.Game.Flow` / `AbilityKit.Game.Battle`。

```
Runtime/
├── Common/Log/
└── Game/
    ├── App/
    │   ├── Config/         RuntimeJsonSettingsCodec
    │   ├── Entry/          GameManager（Unity 入口）
    │   └── Flow/           ★ App 级状态机
    │       ├── Boot/       BootPhase / BootMenuOnGUIFeature / DemoLobbyOnGUIFeature / FormalLobbyFeature / RootDebugOnGUIFeature
    │       └── Core/       BattleWorldModule / BattleWorldScopeHost / BattleAssetLoadCoordinator / FlowStateMachineBuilder / FlowGateProvider / MobaFlowPhaseIds / MobaFlowActions / GamePhaseContracts / IBattleSessionFeature / Multiplayer/MultiplayerRoomFlowController ...（50+ 类）
    ├── Battle/
    │   ├── Bootstrap/{Config, Moba/Config/Sources, Phase}/   IBattleStartConfigProvider + Battle*ConfigSO（EnterGame/FeatureSet/Gateway/RunMode/StartRuntimeOverrides/StartPreset）+ MobaConfigLoader
    │   ├── Client/         ★ 战斗客户端核心
    │   │   ├── Gateway/Room/                房间网关
    │   │   ├── Prediction/FrameSync/        ClientPredictionDriverStatsFramesSource
    │   │   ├── Replay/
    │   │   ├── Session/                     ★★ BattleSessionFeature 所在地（详见 state-handles-controllers skill）
    │   │   ├── SnapshotRouting/{Declarations,Generated}/  IFrameSnapshotDeserializer
    │   │   ├── Synchronization/
    │   │   ├── Transport/                   IBattleLogicClient
    │   │   └── WorldStart/
    │   ├── Debug/          BattleDebugFacadeProvider + IBattleDebugFacade（静态 Current 单例，Editor 写入）
    │   ├── EntityViewModel/{Components,Entities,Features}/  Battle 视图实体模型（BattleCharacterEntity / BattleProjectileEntity / BattleEntityWorld / BattleEntityFactory + Transform/Vfx/Skill/Buff/Lobby/Snapshot Components）
    │   ├── Hierarchy/
    │   ├── Input/{Features,Mapping,Sources,Submission}/   输入采集
    │   ├── Legacy/Requests/
    │   ├── Presentation/   ★ 表现层
    │   │   ├── Camera/
    │   │   ├── Features/{Loading, Settlement, View/}/   BattleViewFeature + ConfirmedBattleViewFeature（各 3 partial）+ 20+ Shared SubFeatures
    │   │   ├── Hud/{Buff, Controls/Lib/{Joystick,Skill}}/
    │   │   ├── Vfx/
    │   │   ├── View/      IFrameSeekableView / MonoSeekableAnimator / MonoViewHandle / ViewTimeline
    │   │   └── ViewEvents/{Snapshot,Triggering}/
    │   ├── Shared/{Assets,Context/{Contracts},Domain,Logging,Modules,Subscriptions,Time}/   BattleLocalInputQueue / BattleEntityQuery / BattleSessionHooks / FeatureModuleContext
    │   └── Testing/
    ├── EntityCreation/    IEntityCreator / EntityGenerator / EntityCreator
    ├── EntityDebug/{Editor}/   EntityView / EntityComponentView / EntityDebugVisualizer
    ├── Test/{Expectations, FrameSync, UnitTest/Acceptance/Heroes/{Daji,LianPo,Mozi,XiaoQiao,YingZheng,ZhaoYun}, UnitTest/Acceptance/{Common,Infrastructure}}/  ★ 6 英雄验收测试
    └── UI/               UIManager / UIPanel / UIWidget / UIRoot / UILayer / UIContext
```

## BattleSessionFeature（40+ partial）

主文件 `Client/Session/Features/Core/BattleSessionFeature.cs`，**详细拆分准则与 partial 文件清单见 [state-handles-controllers](../state-handles-controllers/SKILL.md)**。

partial 分布在 6 个目录：
- **Core/**（21）：主 + Accessors / PhaseAccessors / StateAccessors / NetworkAccessors / SnapshotAccessors / Runtime / RuntimeContracts / Lifecycle / SessionStart / Frame / World / OrchestratorHost / HostInterfaces / HostBridges / NetAdapterContextHost / EventsHost / AutoPlan / SubFeaturePipeline / SubFeatureSetup / DispatcherDispose
- **Editor/**（1）：`.EditorHooks`
- **Gateway/**（6）：GatewayConnection / GatewayFrameTiming / GatewayPreparation / GatewayRoom / GatewayTimeSync / GatewayTimeSyncStats
- **Net/**（3）：NetAdapter / NetworkCondition / TransportFactory
- **Sim/**（5+）：ConfirmedAuthorityWorld / RemoteDrivenLocalSim / SimDispose / SimTick.Confirmed / SimTick.RemoteDriven
- **Snapshot/**（2）：NullRegistries / SnapshotRouting

## 双 Sim 模式（关键）

- **ConfirmedAuthority**：权威确认世界（服务端或主机的权威模拟）
  - `ConfirmedAuthorityWorldInstaller` / `RuntimeFactory` / `TickDriver` / `InputRuntime` / `DebugStatsPublisher`
  - `ConfirmedViewContextFactory` / `ConfirmedViewSideRuntimeFactory`
- **RemoteDriven**：远端驱动本地模拟（客户端预测）
  - `RemoteDrivenWorldInstaller` / `RuntimeFactory` / `TickDriver` / `InputRuntime` / `PredictionContextBinder` / `PredictionStateFactories`

辅助类：`SessionWorldBootstrapValidator`、`IBattleSessionWorldInstaller`、`BattleLogicSessionOptions`、`BattleLogicMode`、`SessionSubFeatures`、7 个 `Session*SubFeature`、`BattleDebugFacadeProvider`（静态 `Current` 单例）、`IBattleDebugFacade`。
