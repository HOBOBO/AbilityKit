# MOBA View Runtime 使用与源码导航

版本和安装见[包入口](../README.md)。从已有示例的客户端装配路径进入，不直接启动数据投影或兼容传输对象。

| 要查看的职责 | 随包入口 |
| --- | --- |
| 启动、进入对局及退出的客户端流程 | [流程规格](../Runtime/Game/App/Flow/MobaFlowSpec.md)、[装配源码](../Runtime/Game/App/Flow/Composition) |
| 表现逻辑、Unity 适配及 UI 归属 | [当前视图目录布局](../Document/ViewRuntimeDirectoryLayout.md) |
| 启动菜单、根调试及战斗调试 | [启动与调试入口](../Runtime/Game/App/Flow/Boot/README.md)、[战斗调试边界](../Runtime/Game/Battle/Debug/README.md) |
| 旧版 API 的维护边界 | [兼容定义](../Runtime/Game/Battle/Legacy/README.md)；仅在边界适配，不作为新接入入口 |

View 负责表现与会话流程。表现播放、OnGUI 或本地调试操作不定义权威战斗规则；状态与同步策略仍由示例所用的运行时及宿主契约决定。

## 对局录制

在 `BattleRunModeConfig` 中将运行模式选为 `Record`，再选择录制范围：

- `InputsAndHashes`（默认）：写入优化二进制 `.bin`，保留输入与调用链能提供的哈希，不写快照。需要与原对局一致的开局配置才能从头重演。
- `WithSnapshots`：在同样的输入和哈希上追加录制链收到的快照；可选二进制 `.bin` 或用于排查的 JSON。不请求额外快照，不改变同步模式、世界恢复或检查点语义。

本入口使用内置 codec，不读取或修改进程级覆盖。现有内置 `.bin` / JSON 回放仍可读；早期通过外部安装器产出的 MemoryPack 文件须交给匹配的 codec，不属于本入口的格式契约。成功关闭并写入文件时，`[BattleRecord]` 日志报告实际 codec、轨道范围和各轨条数。
