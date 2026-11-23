# MOBA Runtime 使用与源码导航

安装条件和职责见[包入口](../README.md)。已有宿主的最小接入顺序是：从[启动链路](../Runtime/Docs/StartupChainGuide.md)确认所用世界蓝图与 Bootstrap 入口，再从[服务装配](../Runtime/Docs/ServiceRegistrationGuide.md)和[系统顺序](../Runtime/Docs/SystemOrderGuide.md)核对注册及帧驱动，不自行复制另一套执行链。

## 运行时代码导航

本包是 MOBA 示例的运行时，不是通用玩法框架。先按业务职责找代码，再按具体类型定位；不要把可选玩法放进对局基础流程目录。

| 职责 | 源码入口 | 归类规则 |
| --- | --- | --- |
| 对局基础流程 | [`Application/Gameplay/Match`](../Runtime/Application/Gameplay/Match) | 对局阶段、规则契约、变量、结果与重生调度。这里的“基础”指 MOBA 示例，不是 AbilityKit Core。 |
| 示例玩法扩展 | [`Application/Gameplay/Extensions`](../Runtime/Application/Gameplay/Extensions) | 兵线、建筑、死亡竞赛计分及具体限时规则；兵线与建筑共用的既有驱动器放在扩展根下。扩展目录不代表可独立安装或裁剪。 |
| 对局与触发器集成 | [`Application/Gameplay/Triggering`](../Runtime/Application/Gameplay/Triggering) | 玩法事件、绑定、校验和载荷适配，不放玩法规则本体。 |
| 技能运行时 | [`Application/Skills`](../Runtime/Application/Skills) | `Cast` 管输入、施法状态与取消清理；`Pipeline` 管阶段编排与帧推进；`Passive` 管被动订阅；`Economy` 管费用、冷却与提交。`Modifiers`、`Derived`、`Triggering` 分别承载参数解析、派生施法和技能专属触发适配。 |
| 共享触发执行 | [`Application/Services/Triggering/Execution`](../Runtime/Application/Services/Triggering/Execution) | 执行来自技能、Buff、HFSM 等来源的效果与 Trigger Plan，不归技能模块独占。 |
| Buff 运行时 | [`Application/Buffs`](../Runtime/Application/Buffs) | 根目录是 Buff 服务入口；`Core` 管实例和上下文，`Lifecycle` 管应用、结束和来源认领，`Systems` 管帧内驱动，`Triggering` 管事件与阶段效果适配，`Recovery` 管 Buff 本域的状态恢复。世界级回滚编排仍在 `Application/Rollback`。 |
| 其他战斗领域 | [`Application/Services`](../Runtime/Application/Services)、[`Application/Systems`](../Runtime/Application/Systems) | 投射物、区域等仍按现有服务与系统目录定位，尚未迁移。 |
| 领域数据、装配与回滚 | [`Domain`](../Runtime/Domain)、[`Infrastructure`](../Runtime/Infrastructure)、[`Worlds`](../Runtime/Worlds)、[`Application/Rollback`](../Runtime/Application/Rollback) | 分别查领域数据、配置和基础设施、世界蓝图、恢复适配。不要因为目录归类变更而改变系统装配或恢复语义。 |

运行时上下文的包内用法见[上下文模块指南](../Runtime/Application/Services/Context/README.md)。跨包身份、快照恢复设计和测试证据不在包内维护，见仓库 MOBA 专题索引。
