# 03 状态同步

`TinyStateExample.RequireStateCapability` 协商服务器权威的状态同步配置，`SubscribeAsync` 通过公开房间 API 订阅，`ReadActor` 在调用方接受完整基线后，从快照读取强类型角色。调用方负责 Gateway 连接、帧游标和表现更新；正式流程使用 `TinyBattleSession.TryGetNewSnapshot` 和 `TinyActorViewModule`。

同一份示例源码编译进 `src/AbilityKit.Demo.Tiny.StateSample` 和 `src/AbilityKit.Demo.Tiny.Client`。从仓库根运行 `./tools/verify-tiny-starter.ps1 -SkipUnity`。查看 `state-sample.log` 中的完整基线和双客户端权威结果；重连由 `state.log` 和 `session-state.log` 单独覆盖。
