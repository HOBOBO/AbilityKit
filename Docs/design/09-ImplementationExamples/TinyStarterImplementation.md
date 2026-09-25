# Tiny 战斗 Starter 接入基线

## 定位

Tiny 是新玩法接入 AbilityKit 的可运行参考项目：两名玩家、整数坐标移动、一次带冷却的攻击。规则保持简单，用真实的 Room、Gateway、同步和恢复链路展示项目需要承担的职责。`Runtime` 是正式实现；后续教学片段应引用这份实现，不复制第二套战斗代码。

“最少自定义代码”指沿预设装配路径复用框架能力。同步权威、输入语义、状态序列化、表现和失败策略仍是项目决策，不能由通用框架替项目猜测。

## 接入责任

| 责任 | 新项目实现 | 现有框架能力 | Tiny 位置 |
| --- | --- | --- | --- |
| 确定性规则 | 输入、战斗状态、Tick、状态哈希、快照编解码 | 世界与回滚基础类型 | 独立 `com.abilitykit.demo.tiny.logic/Runtime/Logic`，由 Unity 和 .NET 共同编译 |
| 服务端玩法 | Room/Battle 适配、玩法模块声明、同步模板选择 | Room 生命周期、权威帧调度、快照推送 | `AbilityKit.Demo.Tiny.Server`；Host 组合根注入模块 |
| 协议 | 业务载荷 opcode 与字段映射 | YAML 协议目录、wire 生成、类型化 Room API | Tiny 输入载荷及 `Protocols` 的 Room 定义 |
| 客户端会话 | 创建房间的策略、玩家槽位、输入和快照映射 | Network SDK、连接恢复、Room 流程、能力协商、帧收件箱 | `Runtime/View/TinyBattleSession.cs`、`TinyGatewayClient.cs` |
| 同步策略 | 预测状态与回滚适配、快照校正的选择 | 输入历史、快照环、回滚协调器 | `Runtime/FrameSync`、`Runtime/View/Sync` |
| 表现与入口 | 场景、输入、角色外观和 UI | 可选 View 模块宿主、Loading 步骤 | `Runtime/View`、`Composition`、Starter 配置 |

`TinyBattleSession` 承担 Tiny 项目的 Room 与战斗会话编排。Host 通过 `ServerGameplayModuleCatalog.Default.WithModule(TinyServerGameplayModule.Create())` 注入 Tiny；Grains 的默认目录只包含现有 MOBA/Shooter。仅注册 Tiny 的目录也可构造，未注册默认 `battle` 类型时使用首个模块作为默认玩法。客户端 `TinyProjectLaunch.Open` 提供不经过 Starter 场景的入口，但仍使用 DemoCommon 的启动请求类型和场景路由；这是模板依赖边界。

## 模式语义

| 模式 | 客户端行为 | 恢复方式 |
| --- | --- | --- |
| State | 展示权威状态快照；当前没有本地状态预测 | 新的权威全量快照 |
| Frame | 根据权威输入帧推进确定性规则，迟到输入触发重放 | 历史缺失、哈希失配或连接恢复时请求全量快照 |
| Hybrid | 本地输入先预测，再由权威帧确认；周期快照校验历史状态 | 校验不一致时校正到权威状态，无法重放时请求全量快照 |

这里的 Hybrid 是“帧输入预测 + 周期状态快照”，不是独立的状态同步预测回滚实现。若将来要展示 `acceptedSeq` 裁剪和状态重演，应作为新增能力单独实现与验收。

`TinySyncSettings` 集中定义服务端 TickRate、房间输入延迟和客户端提交余量。房间创建标签与服务端 Tick 使用这些定义；TCP 压力验收允许独立的具名提交余量。更复杂的项目可将这些参数改为经服务端能力协商下发的配置。

正式会话会观察帧输入响应的服务端帧号。Frame 模式只对已处理的旧帧做有界重排；过早输入转入全量恢复，限速保留当前基线。Hybrid 已经预测的输入若被拒绝则请求全量基线，不将其静默迁移到另一帧。输入模块每帧采样并在网络请求等待期间保留最新移动和一次攻击短按。

## 当前验收

从仓库根目录运行 `./tools/verify-tiny-starter.ps1`：只读检查协议，执行 Room 与 Tiny 复制器测试，构建 Host/Gateway/客户端，在隔离端口依次完成 Room、State、Frame、Hybrid 独立双客户端工程、Recovery 四类故障验收及三模式综合 TCP 恢复验收。服务端仍运行时，隔离 Unity 网络 PlayMode 用两套正式会话和角色 View 验证三模式双端投影、访客恢复，并写入 `tiny-network-playmode.json`；随后运行 Tiny EditMode/PlayMode、无 Starter 消费工程与独立 Logic 工程测试。`-SkipUnity` 可省略 Unity 阶段。脚本检查每份 Unity XML 至少一项测试且全部通过。.NET 构建产物写入当次验收目录，避免与工作区其他构建共用 `bin/obj`。04–06 工程共编译 UPM 样例，并通过 `AbilityKit.Demo.Tiny.ClientHarness` 复用 .NET 无头网络驱动；正式会话实现仍只有 Unity 包 `Runtime/View` 中的一份。

上述验证覆盖网络闭环、权威输入重放和断线全量恢复。Unity EditMode 验证工程编译和定向行为；普通 PlayMode 验证场景加载、独立项目入口、Bootstrap、返回大厅和 View 对象生命周期，并断言 State、Frame、Hybrid 到角色对象的投影。网络 PlayMode 使用真实 Gateway，但两套会话仍在同一个无头 Unity 进程中，直接驱动正式会话和 View 模块；它不等于两份可视化工程通过 `TinyGameplayRoot` 的完整界面交互。最终画面与交互仍需独立验收，不能以这些无头结果代替。

## 后续交付顺序

1. 已提供可移动的 Unity 工程模板：Starter 模式和不含 Starter 的 Tiny 消费模式均嵌入本地包闭包，manifest 不含本仓库绝对路径。MemoryPack 需要的 Unsafe 程序集由 `com.abilitykit.thirdparty.unsafe` 包提供，不再从源码工程 `Assets/Plugins` 补拷。消费工程用自有大厅及 `TinyProjectLaunch.Open` 验证进场；首次打开仍依赖公共 UPM 包解析。
2. 待在两个独立 Unity 客户端中验收登录、创建/加入、准备、开战、输入、三种模式画面和重连，并保存可复查的日志或录像。
3. 收敛项目装配边界：项目侧可注册服务端玩法模块，客户端入口无需修改公共 Starter 的 Tiny 分支；保持规则和表现为项目所有。
4. `Docs/tutorials/tiny` 已按 State 主线、Frame/Hybrid 分支和恢复横切能力组织；Logic 包已独立。02–05 各有独立可运行的双客户端 .NET 工程，06 有独立恢复故障工程及三模式综合联机验收。08 提供跨层规则变更练习；正式 `Runtime` 实现只保留一份。
