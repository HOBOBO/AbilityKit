# PlayMode 与 Editor

## 4 种 PlayMode（view.runtime/Runtime/PlayMode/）

`ShooterPlaySessionRunner` + `ShooterPlaySessionPorts` + `ShooterPlayModeSessionOptions`：

- **RemoteStateSync** — 进入 PlayMode 连接 Orleans state-sync 服务器（默认 `127.0.0.1:41001`，`restart_shooter_state_sync.bat`），走 Gateway 房间流程
- **FrameRecordReplay** — 离线回放模式（非联机，从文件读取录像）
- **EditorDirect**（Editor only）— `EditorApplication.update` 驱动 + SceneView 渲染
- **HostAttach** — 进入 PlayMode，`ShooterPlayModeSessionHost` 发布宿主，窗口挂接观察

启动选项：

- `ShooterRemoteStateSyncLaunchOptions` / `ShooterRemoteStateSyncConnectionFlow`
- `ShooterFrameRecordInputSource`（FrameRecordReplay 用）
- `ShooterGameplayScenarioWorldHostFactory`
- `ShooterNullPlayViewSink`（无视图）

## Unity PlayMode 启动器（`view.runtime/Runtime/Unity/PlayMode/`）

- `ShooterRemoteStateSyncPlayModeLauncher` + `Profile` + `ProfileCatalog`
- `ShooterFrameRecordReplayPlayModeLauncher` + `Profile` + `ProfileCatalog`
- `ShooterPlayModeSessionHost`
- `ShooterInitialFullStateSyncCoordinator` — 初始全量同步协调
- `ShooterRemoteInputPump` + `SubmitStrategy` — 远程输入泵
- `ShooterRemotePresentationFrameBuilder`
- `ShooterReconnectLaunchOptionsBuilder`
- `UnityShooterPlayAdapters` / `UnityShooterViewBackends`

## Editor 包（6 个文件）

`Unity/Packages/com.abilitykit.demo.shooter.editor/Editor/`：

- `ShooterDemoWindow` — 菜单 `Tools/AbilityKit/Shooter Demo`
- `ShooterDemoDriveMode` — 枚举：`EditorDirect` / `HostAttach` / `RemoteStateSync`
- `ShooterEditorSceneViewSink` — SceneView 渲染 sink（`EditorDirect` 用）
- `ShooterEditorInputProvider` — WASD/方向键 + Space 输入（`EditorDirect` 用）
- `ShooterDemoDiagnostics` — Editor 诊断
- `ShooterAcceptanceAutomation` — 验收自动化

## SceneView 渲染

`EditorDirect` 模式下：

- `ShooterEditorInputProvider` 读 WASD/方向键 + Space
- `EditorApplication.update` 驱动 `ShooterBattleRuntimePort.Tick`
- `ShooterEditorSceneViewSink` 在 SceneView 画玩家/子弹/敌人（Gizmos）

## 远程 StateSync 服务器

`restart_shooter_state_sync.bat`（项目根目录）：启动本地 Orleans state-sync 服务器。

Gateway 房间流程（`RemoteStateSync` 模式）：

```
GuestLogin → ListRooms → CreateRoom（或 JoinRoom）→ Ready → Start → Subscribe
```

详见 host-extension skill 的 `RoomGatewaySessionFlow`。

## Replay

`view.runtime/Runtime/PlayMode/` + `Client/Replay/`：

- 录制：`--record` CLI（Console 模式）或 Editor DriveMode 切换
- 回放：`ShooterFrameRecordReplayPlayModeLauncher` + `ShooterFrameRecordInputSource`
- 元信息：录像文件含随机种子、规则集、tick 数等
