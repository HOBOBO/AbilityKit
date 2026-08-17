# MOBA 战斗诊断测试与验证指南

> 最后更新：2026-08-08
>
> 默认环境：Windows 11、Unity 2022.3 LTS、工作区根目录执行命令。

## 验证层级

| 层级 | 目的 | 能证明什么 |
| --- | --- | --- |
| 静态检查 | 检查文档、路径、GUID 和差异格式 | 文件结构和仓库约束正确 |
| 范围化构建 | 编译 Core、Runtime、Editor 和 Tests | 程序集能够编译和链接 |
| 聚焦 EditMode 测试 | 验证新增 DTO、Store、Session、ViewModel 或 Producer | 目标行为通过 NUnit |
| 完整诊断 EditMode 回归 | 验证诊断测试程序集 | 诊断功能未发生已覆盖回归 |
| 手工 Editor 验收 | 验证真实 Play Mode 工具工作流 | 窗口、选择、空态和交互符合预期 |

编译通过不等于测试通过。只有实际执行 NUnit 并获得明确结果，才能报告测试通过。

## 前置检查

1. 确认 Unity 版本与包清单要求一致。
2. 检查当前是否已有 Unity Editor 打开 `Unity` 项目。
3. 检查工作树中的既有改动，不回退与当前任务无关的文件。
4. 确认新增源码和文档均有 Unity `.meta` 文件。
5. 确认生成 `.csproj` 已包含新增 C# 文件。

Unity Editor 已运行时，不启动第二实例，也不结束用户进程。此时可以执行范围化 `dotnet build`，EditMode 测试应在现有 Editor 中运行或明确记录未执行。

## 范围化构建

从仓库根目录依次执行：

```powershell
 dotnet build .\Unity\AbilityKit.Demo.Moba.Diagnostics.Core.csproj --no-restore -p:BuildProjectReferences=false
 dotnet build .\Unity\AbilityKit.Demo.Moba.Runtime.csproj --no-restore -p:BuildProjectReferences=false
 dotnet build .\Unity\AbilityKit.Demo.Moba.View.Runtime.csproj --no-restore
 dotnet build .\Unity\AbilityKit.Demo.Moba.Editor.csproj --no-restore
 dotnet build .\Unity\AbilityKit.Demo.Moba.Diagnostics.Core.Tests.csproj --no-restore -p:BuildProjectReferences=false
 dotnet build .\Unity\AbilityKit.Game.UnitTests.csproj --no-restore
```

使用 `BuildProjectReferences=false` 可以隔离无关脏工作区依赖错误，但不能替代完整项目构建。若改动修改了依赖契约，应至少再构建直接消费者，条件允许时执行完整 Unity 编译。

记录每个项目的：

- 退出码。
- error 数量。
- warning 数量及是否为本次新增。
- 是否关闭了项目引用构建。

## Unity EditMode 测试

诊断测试程序集：`AbilityKit.Demo.Moba.Diagnostics.Core.Tests`

录像驱动测试程序集：`AbilityKit.Game.UnitTests`

### 编辑器测试运行器

1. 在 Unity 打开 `Window > General > Test Runner`。
2. 选择 EditMode。
3. 搜索 `AbilityKit.Demo.Moba.Diagnostics.Core.Tests` 或目标 fixture。
4. 先运行与本次变更直接相关的 fixture。
5. 聚焦测试通过后，运行完整诊断测试程序集。
6. 保存或记录 Test Runner 的结构化结果。

### 命令行原则

无人占用项目且需要自动化时，可以使用 Unity batchmode：

```powershell
& "<UnityEditorPath>\Unity.exe" -batchmode -projectPath ".\Unity" -runTests -testPlatform EditMode -testFilter "AbilityKit.Demo.Moba.Diagnostics.Tests" -testResults ".\Unity\TestResults-MobaDiagnostics.xml" -logFile ".\Unity\TestResults-MobaDiagnostics.log"

& "<UnityEditorPath>\Unity.exe" -batchmode -projectPath ".\Unity" -runTests -testPlatform EditMode -testFilter "AbilityKit.Game.Test.UnitTest.FrameReplayDriverTests" -testResults ".\Logs\frame-replay-driver-tests.xml" -logFile ".\Logs\frame-replay-driver-tests.log"

& "<UnityEditorPath>\Unity.exe" -batchmode -projectPath ".\Unity" -runTests -testPlatform EditMode -testFilter "AbilityKit.Game.Test.UnitTest.SessionReplayControllerTests" -testResults ".\Logs\session-replay-controller-tests.xml" -logFile ".\Logs\session-replay-controller-tests.log"
```

`<UnityEditorPath>` 必须替换为本机 Unity 2022.3 Editor 实际安装目录。不要在已有 Editor 打开项目时执行该命令。使用 `-runTests` 时不要附加 `-quit`；Test Framework 会在测试完成后退出，提前 `-quit` 可能只完成工程刷新而不生成 XML。

判定通过需同时确认：

- Unity 进程正常退出。
- XML 结果文件存在。
- XML 中失败数为 0。
- 日志没有编译错误、测试框架异常或未触达测试程序集的迹象。

只有日志退出码而没有 XML 时，应明确记录证据限制；不要把缺失结果文件描述为完整结构化测试通过。

## 推荐聚焦范围

| 变更类型 | 首选测试 |
| --- | --- |
| Health、交互状态、统一选择、导航历史、过滤与分页 | `BattleDiagnosticCoreTests` |
| Event DTO、Payload、Ring Store 与结构化技能失败搜索 | `BattleDiagnosticStoreTests` |
| World/Actor State Store | `BattleDiagnosticStateStoreTests` |
| Actor Attributes | `BattleDiagnosticActorAttributeStoreTests` |
| Actor Buffs | `BattleDiagnosticActorBuffStoreTests` |
| Actor Tags | `BattleDiagnosticActorTagStoreTests` |
| Actor Effects | `BattleDiagnosticActorEffectStoreTests` |
| Local Session 和状态采样 | `MobaBattleDiagnosticStateSamplerTests` |
| Trace 查询 | `MobaBattleDiagnosticTraceReadStoreTests` |
| Collector 端口和 Event 流转 | `MobaBattleDiagnosticEventCollectorTests` |
| Editor ViewModel 缓存、Selection Inspector、Workspace/局部 Filter 合并、问题簇、案例、QueryStatus 生命周期与统一空态投影 | `BattleDebugDiagnosticViewModelTests`、`BattleDebugSkillInvestigationModelTests` |
| 标准 Artifact、导入校验、离线 Session 与 Editor 数据源切换 | `MobaBattleDiagnosticArtifactCodecTests` |
| 录像驱动状态、速度、末帧、Seek 哈希状态与真实本地 Session 帧包提交 | `FrameReplayDriverTests` |
| 向后 Seek 模式策略、渲染模式表现 Suspend/Restore 与 Session 重建顺序 | `SessionReplayControllerTests` |
| 单一 Producer | 对应 `Moba*DiagnosticProducerTests` |
| 系统执行顺序 | `MobaDiagnosticSystemOrderTests` |

新增功能的聚焦测试至少覆盖正常路径、不可用语义、revision/缓存和关键输入不变量。Health 应覆盖有效性、各轨道 Produced 判定、错误边界、长度上限和值相等；统一空态应覆盖 SelectionRequired、FilteredEmpty、Empty、NotProduced、NotCaptured、Evicted、Unsupported、Disconnected 和 Failed，并验证 Partial/Truncated 且存在可显示结果时不投影为空态。Trace 还应验证 Root Context 选择主体文案；Trace 与 Actor DTO ViewModel 应验证真实 QueryStatus 在查询后保留、缓存失效后清除。Selection Inspector 应覆盖 Actor 按选择帧查询及 State revision 缓存失效、Event 精确帧与固定 Event revision、跨页稳定 Sequence 恢复、最多四页的有界扫描与 Partial/Truncated、Evicted 状态保留，以及 Trace 使用 Related Root 并匹配选中 Context。Events Workspace Filter 应覆盖共享与局部条件的 Channel 交集、共享标量优先、局部补位、FailuresOnly OR、Frame/UnfinishedOnly 保留，以及 Filter 值变化进入缓存键并使固定 revision 分页失效；清除局部条件不得修改 Workspace Filter，`RecentFrameCount` 不得进入共享 Filter。失败调查变更还应覆盖：可靠 Root Trace 聚合、无 Trace 失败隔离、置信度/根因分类与组合筛选、结构化技能失败字段搜索、Artifact round-trip、剪贴板输出，以及不同 Summary 但相同 Code/Source/Stage 的稳定问题簇。调查工作集还应覆盖首屏 offset/limit、同一固定 revision 的下一页、追加后的顺序与问题簇范围、live revision 重建首屏，以及固定 revision 淘汰时保留已加载结果。标准 Artifact 测试还应覆盖旧顶层格式缺失可选 Section 的兼容性、顶层与 Section 独立版本、必需轨道、损坏输入、已知结构化 Payload round-trip，以及有效文件切换、损坏替换保持当前离线现场和返回实时清理状态。录像 Driver fixture 已覆盖真实本地 `BattleLogicSession` 对录制移动输入的帧号、玩家、操作码、载荷透传，以及暂停后不再提交。Controller fixture 覆盖纯逻辑模式才允许 Rollback 的策略矩阵、渲染模式向后 Seek 的 Suspend/重建/Restore 调用顺序，以及重建失败仍恢复表现；这些自动化测试仍不能替代完整 View/HUD/VFX 清理、连续模式切换和诊断数据随帧更新的 Play Mode 验收。

## 手工 Editor 验收

实时导出与运行时面板在本地 Play Mode 验收，离线导入同时在 Play Mode 和 Edit Mode 验收：

1. 打开 `Tools/AbilityKit/Battle/战斗调试`。
2. 确认实体列表自动刷新，过滤、清除过滤、可见/总数反馈与 Actor ID 跳转可用。
3. 使用 `<` / `>` 在当前可见实体间首尾循环导航，并用 `×` 清除选择；改变过滤条件和实体数量后，确认选择不会静默切换，对象被过滤或离开世界时显示明确提示。
4. 在 `Actor` 与 `Diagnostics` 工作区之间切换，并确认面板下拉框只显示当前工作区内容。
5. 选择一个有属性、Tag、Effect 或 Buff 的 Actor，检查总览、属性、标签、效果、Buff 的字段、空态和滚轮归属。
6. 检查诊断状态中的 World Frame、ActorCount 和 Actor 列表；点击 Actor ID 后确认左侧选择同步更新。
7. 拖动实体列表与详情区之间的分隔条，确认宽度限制合理；关闭并重新打开窗口，确认栏宽、工作区和各工作区面板位置恢复，但 Actor 选择不跨窗口恢复。
8. 触发技能、伤害、治疗或 Effect，确认诊断事件出现；点击事件序列号检查 Context、Config、Payload 等详情。
9. 在“失败调查”区确认共享可靠 Root Context 的失败聚为一个案例，而无 Root Trace 的失败保持独立；检查 Confirmed、Inferred、InsufficientEvidence 置信度与根因结论符合证据。
10. 组合切换置信度和根因筛选，确认案例数和当前选择正确更新；用前后按钮切换案例，点击证据序列号后确认事件详情同步更新。
11. 对案例点击“聚焦证据链”，确认复用 Root Context、Skill Runtime、Attack 或 Context 关联过滤；选择来源 Actor、复制调查摘要和“打开 Trace”均可用。缩窄窗口后确认较长证据链自动换行且按钮不重叠。
12. 制造两个 Summary 文案不同但 Code/Source/Stage 相同的结构化技能失败，确认稳定问题簇合并计数，并能按 Code 聚焦；按 Code、Message 和 Slot 搜索均能命中。
13. 让当前筛选产生超过 200 条事件，确认调查工作集显示固定 SnapshotRevision 和“仍有更早结果”；选中一个调查案例后点击“加载更多”，确认事件、案例和问题簇扩展，当前案例按稳定 Key 保持且证据更新。
14. 确认问题簇显示次数、首次帧、最近帧和跨度；加载更多期间继续产生 live 事件，确认追加页不混入新的 live revision。让固定 revision 淘汰后再次加载，确认已加载结果保留且显示明确淘汰提示。
15. 对带 Root Context ID 的事件点击“打开 Trace”，确认自动切换 Trace、加载正确根并选中事件 Context。
16. 在窗口顶部 Frame Cursor 状态条输入有效帧并确认跟随状态切换为固定帧；点击“跟随最新”后确认跳到最新完整帧。点击 Event、Trace Root 或 Trace Node 后确认 Selection、Frame Cursor 和历史记录保持同一帧语义；使用后退/前进确认帧随选择恢复。
17. 在 Replay 中点击“定位 Replay”或 Trace 起止帧定位，确认先更新共享 Frame Cursor 再执行 Seek；Seek 不可用或失败时不伪装 Live/Artifact 具备回放能力，诊断游标仍保留用户选择的帧。
18. 在宽度不小于 960 的窗口中打开“检查器”，确认 Actor、Event、Trace Root 和 Trace Node 选择始终显示在右栏，切换 Actor/Diagnostics 工作区和二级面板后详情不丢失；拖动 Inspector 分隔条并重开窗口，确认宽度和显示状态恢复。在 720 宽度下确认 Inspector 降级到主工作区下方且可收起，内容和按钮不重叠。
19. 在 Inspector 中验证 Actor 跳转、Event 来源/目标 Actor、配置、Trace、复制，以及 Trace Actor、配置、根、起止帧和复制操作；制造被淘汰 Event、不可用 Trace 或非当前帧 Actor 状态，确认显示真实 Evicted/Unavailable/NotCaptured/Partial 状态而不生成伪详情。
20. 从 Event 或 Trace 点击“打开配置”，确认 Inspector 立即持续显示完整 Config Kind、Id 和可选 PhaseId，且原有选中、Ping 和外部编辑器打开行为不回归；配置定位成功时确认显示权威 JSON 路径和准确行号，SkillFlow 节点必须定位到精确 `PhaseId`，不能回退到同 Flow 的其他节点。
21. 对不存在的配置 Id、SkillFlow Phase、缺失配置源和无效 JSON 分别验证持续错误状态；点击“重新解析”后确认重新读取配置源，点击“复制引用”确认包含完整 Kind、Id 和 PhaseId。切换到另一个 Workspace Actor、Event 或 Trace Selection 后，确认旧 Config 投影清除，不继续显示为当前选择；只切换工作区或二级面板时投影应保持。
22. 在 Trace 中搜索 Kind、Context、Actor 或 Config，确认只显示命中节点及祖先路径；使用 `<` / `>` 只在直接命中节点间首尾循环，并确认目标自动滚动回树视口。
23. 折叠分支后搜索其后代，确认搜索期间仍可见，清除搜索后恢复折叠状态；选择深层节点后点击“全部折叠”，确认选中父链保持展开且无关分支收起，再点击“全部展开”。
24. Pin 一个 Trace 节点，切换到其他节点后点击“返回 Pin”；确认目标自动进入视口。刷新导致该节点淘汰时，确认按钮禁用且详情显示不可用提示。
25. 分别设置 Workspace 共享 Filter 和 Events 局部调查条件，确认工具栏持续标明来源且结果遵循 Channel 交集、共享标量优先和局部补位；改变共享条件后确认旧缓存和固定 revision 分页不可复用。依次验证“清除局部”“设为共享”“清除共享”只影响对应层；设置“最近帧”后执行“设为共享”，确认最近帧窗口被清除、未写入共享 Filter 且状态栏明确提示。选择没有对应集合的 Actor，确认显示正常空态而不是未采样错误。
26. 关闭“自动刷新”后生成或移除 Actor，确认窗口列表保持不变且底层诊断事件仍继续产生；点击“刷新”后列表更新，重新开启自动刷新后恢复轮询。
27. 在实体集合不变时观察自动刷新，确认列表滚动位置和选中状态稳定；生成或移除 Actor 后确认列表更新。
28. 在活动战斗中点击“导出”，保存 JSON 后确认状态栏显示成功文件名；导出按钮在离线模式或没有捕获服务时应禁用。
29. 在 Play Mode 的活动战斗中点击“录像”，保持“渲染表现”开启并加载标准录像，确认来源退出 Artifact 离线模式，当前 Session 切换为 Replay，控制区显示路径、当前帧、末帧和“表现渲染”。
30. 使用播放/暂停和速度输入，确认暂停期间 World Frame 与诊断数据冻结，播放恢复后继续推进，速度限制在 `0.1x` 至 `8x`，到达末帧后自动暂停。
31. 使用前进/后退单步与进度 Slider，确认向前和向后都到达目标帧，Actor、Attributes、Buff、Tag、Effect、Events 和 Trace 面板随重演世界更新，而不是只改变游标文本。
32. 在渲染模式向后 Seek，确认旧 HUD、View、VFX、Timeline、插值对象、对象池实例和快照订阅没有残留或重复；目标帧重演完成后表现重新创建并与逻辑状态一致。
33. 关闭“渲染表现”重新加载录像，确认主 HUD、View、VFX 和相机不创建或驱动，状态显示“纯逻辑”；播放或 Seek 到特定帧并暂停后，确认 Actor、Attributes、Buff、Tag、Effect、Events 和 Trace 仍可查询当前逻辑世界。
34. 在纯逻辑模式验证启用 Rollback 时的向后 Seek 快路径；在渲染模式验证无论 Rollback 是否可用都执行表现重置、Session 重建和从头重演，且没有重复或漏提交输入。
35. 连续验证“表现渲染 -> 纯逻辑 -> 表现渲染”切换，确认没有重复 Feature、残留 GameObject 或失效订阅；录像加载失败时确认原 Session 和原表现模式仍可用或成功恢复。
36. 加载成功后确认 Live 与 Replay 不并行混入同一窗口数据；分别在 720、960 和 1180 宽度下确认 Frame Cursor、Selection、状态和录像控件不重叠或截断关键操作。
37. 点击“打开”加载刚导出的 Artifact，确认来源显示“离线”、文件名、Session、World 和 `Disconnected/Frozen`，Actor 列表来自文件且 DTO 面板可查询。
38. 离线模式下确认帧同步面板和录像控制隐藏，后台实时或 Replay Facade 不混入列表或详情；打开损坏 JSON 时确认显示稳定错误码且当前离线现场保持不变。
39. 点击“返回实时”，确认离线来源释放；Play Mode 恢复当前活动会话，Edit Mode 显示可打开文件或进入播放模式的提示。
40. 退出 Play Mode 后再次打开有效 Artifact，确认无需活动 `BattleLogicSession` 也能完成离线 Actor 导航和 DTO 面板浏览，同时不能启动 Replay 逻辑世界。

如果验证的是 DTO-only 面板边界，还应搜索 Panel 代码中是否出现 `SelectedUnit`、`IUnitFacade` 或具体 Runtime 容器读取。

### 真实技能失败调查场景

该场景用于验证第一批 Overview、Health、稳定导航和空态闭环，必须在 Play Mode 的真实技能链执行，不使用手工伪造 Event 代替 Producer：

1. 启动包含可控 Actor 和目标 Actor 的本地战斗，打开 Battle Debug，记录来源、Session Scope、Event/State/Trace revision 和最后帧。
2. 触发一次可重复的技能失败，优先使用目标超出距离、目标无效或战斗规则拒绝；记录技能 Slot、Source Actor、Target Actor、失败 Code 和预期失败阶段。
3. 回到 Overview，确认 Health 没有采集错误，Event revision 与最后事件序列增长；State/Trace 是否增长按真实 Producer 结果记录，不把未增长伪装为成功。
4. 点击“最近失败”，确认 Events 自动启用失败预设并定位到新案例；再从 Overview 点击“全部事件”，确认打开全局事件且不会残留最近失败或 Actor 限制。
5. 在失败案例中选择证据 Event，检查结构化 Slot、Source、Stage、Code、Message 和稳定关联 ID；沿入口打开 Source/Target Actor 与 Trace。没有 Root Context 时必须显示证据不足，不能跳转到无关 Trace。
6. 在 Actor、Event、Trace Root 和 Trace Node 之间使用后退/前进，确认选择、面板和稳定 ID 正确恢复；清除或修改列表筛选后，不允许静默选择另一个对象。
7. 制造 Actor 筛选无匹配、普通空 Event、未生产 State/Trace 或不可用来源中的至少两种状态，确认文案能区分 SelectionRequired、FilteredEmpty、Empty、NotProduced、NotCaptured、Evicted、Unsupported、Disconnected 或 Failed。
8. 保存截图或录像，并记录每一步的实际交互次数、首次可疑阶段、阻塞点和是否满足“失败案例到可疑阶段不超过 3 次主要交互”。

第四十一批 2026-08-09 的状态：持久 Selection Inspector 代码和六个聚焦测试已补齐；仅清理当前工程关联进程时未发现残余进程，未终止其他 Unity 工程。Unity 2022.3.62f1 EditMode Test Runner 已生成有效 XML，完整程序集 373/373 通过；生成 Diagnostics Tests 项目构建为 175 warnings、0 errors。窗口视觉、文件对话框、真实技能场景和 Play Mode 验收未执行，没有截图、录像或手工通过结论。

第四十二批 2026-08-09 的当前状态：Config 持久投影代码和三个聚焦 ViewModel 测试已补齐，范围化 `git diff --check` 已通过。Diagnostics Tests 与 Moba Editor 生成项目均已尝试完整引用构建，但被任务外 `SessionMobaWorldBootstrapFactory.cs` 对当前 `BattleSyncMode` 不存在的 `StateSync` 和 `Hybrid` 成员引用阻断；完整日志只发现这两个错误，未发现本批文件编译错误。随后运行 Unity 2022.3.62f1 EditMode Test Runner，但项目脚本编译又被任务外 `MobaLogicWorldDriveGateTests.cs` 对缺失 `MobaGameStartSpec`/`AbilityKit.Protocol.Moba` 引用的错误阻断，未生成本批 XML，因此没有 total/passed/failed/skipped 统计。未执行 Config 窗口视觉、文件打开行为、真实技能场景或 Play Mode 验收，不宣称新增测试或手工场景通过。

## Unity 元数据

每个新增 Unity 包内文件都需要 `.meta`。文本资产通用格式：

```yaml
fileFormatVersion: 2
guid: <32-character-lowercase-hex>
TextScriptImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
```

要求：

- GUID 为 32 位小写十六进制。
- 全仓唯一。
- 不复制其他文件的 `.meta`。
- 移动文件时保留原 GUID；新建文件生成新 GUID。

## 文档检查

文档变更至少执行：

1. 检查 README 中所有相对链接的目标存在。
2. 检查新增文档都有 `.meta`。
3. 检查 `.meta` GUID 全仓唯一。
4. 检查 `CURRENT-CAPABILITIES.md` 与 capability、Session 和面板代码一致。
5. 检查主设计文档不再把历史状态描述成当前事实。
6. 执行范围化 `git diff --check`，确认没有尾随空格或冲突标记。

对于 Markdown，不要求通过 C# 构建证明内容正确；应使用链接、事实和差异检查作为主要验证。

## 提交前门禁

- [ ] Core 仍保持纯 C# 和 `noEngineReferences`。
- [ ] Runtime 没有新增 Editor 引用。
- [ ] Editor 面板只消费只读 Session 和 DTO。
- [ ] Capability 与真实数据源一致。
- [ ] `NotProduced`、`NotCaptured`、`Empty` 和 `Unsupported` 没有混淆。
- [ ] 各数据面使用正确的独立 revision。
- [ ] 新增文件 `.meta` GUID 唯一。
- [ ] 范围化构建为 0 errors。
- [ ] 实际运行的 NUnit 结果被准确记录；未运行时明确说明。
- [ ] 手工 Editor 验收范围与结果已记录。
- [ ] 当前能力、排障和实施历史已同步。

## 验证结果记录模板

```text
变更范围：
Unity 版本：
构建：
- Diagnostics Core：
- MOBA Runtime：
- MOBA Editor：
- Diagnostics Tests：
EditMode 测试：
- Diagnostics 聚焦 fixture：
- Replay Driver fixture：
- Replay Controller fixture：
- 完整程序集：
- XML 路径：
手工验收：
- Replay 表现重置/纯逻辑/Session 重建/Rollback/UI：
- 真实技能失败 Overview/Health/Event/Trace/Actor/History 闭环：
- 证据路径：
静态检查：
已知限制：
```

历史批次中的测试数量只代表当时工作区和结果文件，不应作为当前分支持续通过的证明。
