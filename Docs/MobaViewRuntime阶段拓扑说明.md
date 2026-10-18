# MOBA 客户端副本阶段拓扑

`battle_flow_profiles.json` 可以通过 `TopologyId`、`TopologyStartStateId`、`TopologyStates` 和 `TopologyTransitions` 配置客户端副本的阶段顺序与转移。

默认未填写拓扑字段时使用 `moba.root-battle.v1`，自动展开为 `Prepare`、`Connect`、`CreateOrJoinWorld`、`LoadAssets`、`InMatch`、`End` 六个阶段，运行行为与旧版本一致。

拓扑校验会检查状态唯一性、起始状态、转移端点和事件是否合法。`MobaFlowConfiguration` 与状态机装配器读取解析后的拓扑，新增副本只需要新增 profile，不需要修改状态机代码。

阶段仍映射到稳定的 `MobaBattleState` 枚举。波次、积分、BOSS、胜负等局内规则由触发器和玩法服务驱动，客户端副本流程只负责 profile 加载、功能装配以及进入和退出协议。
