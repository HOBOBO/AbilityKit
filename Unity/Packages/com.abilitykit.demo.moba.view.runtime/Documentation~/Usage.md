# MOBA View Runtime 使用与源码导航

版本和安装见[包入口](../README.md)。从已有示例的客户端装配路径进入，不直接启动数据投影或兼容传输对象。

| 要查看的职责 | 随包入口 |
| --- | --- |
| 启动、进入对局及退出的客户端流程 | [流程规格](../Runtime/Game/App/Flow/MobaFlowSpec.md)、[装配源码](../Runtime/Game/App/Flow/Composition) |
| 表现逻辑、Unity 适配及 UI 归属 | [当前视图目录布局](../Document/ViewRuntimeDirectoryLayout.md) |
| 启动菜单、根调试及战斗调试 | [启动与调试入口](../Runtime/Game/App/Flow/Boot/README.md)、[战斗调试边界](../Runtime/Game/Battle/Debug/README.md) |
| 旧版 API 的维护边界 | [兼容定义](../Runtime/Game/Battle/Legacy/README.md)；仅在边界适配，不作为新接入入口 |

View 负责表现与会话流程。表现播放、OnGUI 或本地调试操作不定义权威战斗规则；状态与同步策略仍由示例所用的运行时及宿主契约决定。
