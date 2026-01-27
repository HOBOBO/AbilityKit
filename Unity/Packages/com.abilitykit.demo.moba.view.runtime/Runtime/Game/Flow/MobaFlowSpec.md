# MOBA Flow Spec

## 目标
- Root GameFlow 负责宏观阶段（登录/大厅/匹配/房间BP/战斗/结算）
- BattleFlow 负责对局内部子流程（加载/连接/进房/对局/结束）
- Connectivity 并行域负责横切网络状态（断线/重连/被踢）

## Root 状态树（GameFlowState）
- Boot
- Auth
- Lobby
- Matchmaking (sub)
- Room (sub)
- Battle (sub)
- PostBattle

## Root 事件（GameFlowEvent）
- Sys.Started
- Auth.LoginRequested
- Auth.LoginSucceeded
- Auth.LoginFailed
- Auth.LogoutRequested
- Match.StartQueue
- Match.CancelQueue
- Match.Found
- Room.Joined
- Room.ReadyChanged
- Room.BpStarted
- Room.BpFinished
- Battle.EnterRequested
- Battle.Ended
- Conn.Disconnected
- Conn.ReconnectSucceeded
- Conn.Kicked

## Root 转移表（简化）
- Boot + Sys.Started -> Auth
- Auth + Auth.LoginSucceeded -> Lobby
- Lobby + Match.StartQueue -> Matchmaking
- Matchmaking + Match.Found -> Room
- Room + Room.BpFinished -> Battle
- Battle + Battle.Ended -> PostBattle
- PostBattle + (用户返回) -> Lobby
- Any + Auth.LogoutRequested -> Auth
- Any + Conn.Kicked -> Auth

## RoomFlow（房间/组队/BP）
RoomFlowState:
- Assemble
- Ready
- BP
- Confirm

转移（示例）：
- Assemble + Room.Joined -> Ready
- Ready + Room.BpStarted -> BP
- BP + Room.BpFinished -> Confirm（并产出 BattlePlan）
- Confirm + Battle.EnterRequested -> ExitToBattle

## BattleFlow（对局内部）
BattleFlowState:
- Prepare
- LoadAssets
- Connect
- CreateOrJoinWorld
- InMatch
- End
- Return

转移（示例）：
- Prepare -> LoadAssets
- LoadAssets + Battle.LoadingDone -> Connect
- Connect + Battle.Connected -> CreateOrJoinWorld
- CreateOrJoinWorld + Battle.JoinedWorld -> InMatch
- InMatch + Battle.Ended -> End
- End -> Return

## Connectivity（并行域）
ConnectivityState:
- Online
- Reconnecting
- Kicked
- Offline

规则：
- Conn.Kicked 必须打断任何主流程，清理会话并回到 Auth
