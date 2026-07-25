# MOBA 战斗诊断测试与验证指南

> 最后更新：2026-07-21
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

### Editor Test Runner

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
| 交互状态、过滤、分页 | `BattleDiagnosticCoreTests` |
| Event DTO、Payload、Ring Store | `BattleDiagnosticStoreTests` |
| World/Actor State Store | `BattleDiagnosticStateStoreTests` |
| Actor Attributes | `BattleDiagnosticActorAttributeStoreTests` |
| Actor Buffs | `BattleDiagnosticActorBuffStoreTests` |
| Actor Tags | `BattleDiagnosticActorTagStoreTests` |
| Actor Effects | `BattleDiagnosticActorEffectStoreTests` |
| Local Session 和状态采样 | `MobaBattleDiagnosticStateSamplerTests` |
| Trace 查询 | `MobaBattleDiagnosticTraceReadStoreTests` |
| Collector 端口和 Event 流转 | `MobaBattleDiagnosticEventCollectorTests` |
| Editor ViewModel 缓存与投影 | `BattleDebugDiagnosticViewModelTests` |
| 标准 Artifact、导入校验、离线 Session 与 Editor 数据源切换 | `MobaBattleDiagnosticArtifactCodecTests` |
| 录像驱动状态、速度、末帧、Seek 哈希状态与真实本地 Session 帧包提交 | `FrameReplayDriverTests` |
| 向后 Seek 模式策略、渲染模式表现 Suspend/Restore 与 Session 重建顺序 | `SessionReplayControllerTests` |
| 单一 Producer | 对应 `Moba*DiagnosticProducerTests` |
| 系统执行顺序 | `MobaDiagnosticSystemOrderTests` |

新增功能的聚焦测试至少覆盖正常路径、不可用语义、revision/缓存和关键输入不变量。标准 Artifact 测试还应覆盖旧顶层格式缺失可选 Section 的兼容性、顶层与 Section 独立版本、必需轨道、损坏输入、已知结构化 Payload round-trip，以及有效文件切换、损坏替换保持当前离线现场和返回实时清理状态。录像 Driver fixture 已覆盖真实本地 `BattleLogicSession` 对录制移动输入的帧号、玩家、操作码、载荷透传，以及暂停后不再提交。Controller fixture 覆盖纯逻辑模式才允许 Rollback 的策略矩阵、渲染模式向后 Seek 的 Suspend/重建/Restore 调用顺序，以及重建失败仍恢复表现；这些自动化测试仍不能替代完整 View/HUD/VFX 清理、连续模式切换和诊断数据随帧更新的 Play Mode 验收。

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
9. 对带 Root Context ID 的事件点击“打开 Trace”，确认自动切换 Trace、加载正确根并选中事件 Context。
10. 在 Trace 中搜索 Kind、Context、Actor 或 Config，确认只显示命中节点及祖先路径；使用 `<` / `>` 只在直接命中节点间首尾循环，并确认目标自动滚动回树视口。
11. 折叠分支后搜索其后代，确认搜索期间仍可见，清除搜索后恢复折叠状态；选择深层节点后点击“全部折叠”，确认选中父链保持展开且无关分支收起，再点击“全部展开”。
12. Pin 一个 Trace 节点，切换到其他节点后点击“返回 Pin”；确认目标自动进入视口。刷新导致该节点淘汰时，确认按钮禁用且详情显示不可用提示。
13. 修改事件过滤条件，确认缓存不会保留旧过滤结果；选择没有对应集合的 Actor，确认显示正常空态而不是未采样错误。
14. 关闭“自动刷新”后生成或移除 Actor，确认窗口列表保持不变且底层诊断事件仍继续产生；点击“刷新”后列表更新，重新开启自动刷新后恢复轮询。
15. 在实体集合不变时观察自动刷新，确认列表滚动位置和选中状态稳定；生成或移除 Actor 后确认列表更新。
16. 在活动战斗中点击“导出”，保存 JSON 后确认状态栏显示成功文件名；导出按钮在离线模式或没有捕获服务时应禁用。
17. 在 Play Mode 的活动战斗中点击“录像”，保持“渲染表现”开启并加载标准录像，确认来源退出 Artifact 离线模式，当前 Session 切换为 Replay，控制区显示路径、当前帧、末帧和“表现渲染”。
18. 使用播放/暂停和速度输入，确认暂停期间 World Frame 与诊断数据冻结，播放恢复后继续推进，速度限制在 `0.1x` 至 `8x`，到达末帧后自动暂停。
19. 使用前进/后退单步与进度 Slider，确认向前和向后都到达目标帧，Actor、Attributes、Buff、Tag、Effect、Events 和 Trace 面板随重演世界更新，而不是只改变游标文本。
20. 在渲染模式向后 Seek，确认旧 HUD、View、VFX、Timeline、插值对象、对象池实例和快照订阅没有残留或重复；目标帧重演完成后表现重新创建并与逻辑状态一致。
21. 关闭“渲染表现”重新加载录像，确认主 HUD、View、VFX 和相机不创建或驱动，状态显示“纯逻辑”；播放或 Seek 到特定帧并暂停后，确认 Actor、Attributes、Buff、Tag、Effect、Events 和 Trace 仍可查询当前逻辑世界。
22. 在纯逻辑模式验证启用 Rollback 时的向后 Seek 快路径；在渲染模式验证无论 Rollback 是否可用都执行表现重置、Session 重建和从头重演，且没有重复或漏提交输入。
23. 连续验证“表现渲染 -> 纯逻辑 -> 表现渲染”切换，确认没有重复 Feature、残留 GameObject 或失效订阅；录像加载失败时确认原 Session 和原表现模式仍可用或成功恢复。
24. 加载成功后确认 Live 与 Replay 不并行混入同一窗口数据；在窄窗口宽度下确认“渲染表现”开关、状态和录像控件不重叠或截断关键操作。
25. 点击“打开”加载刚导出的 Artifact，确认来源显示“离线”、文件名、Session、World 和 `Disconnected/Frozen`，Actor 列表来自文件且 DTO 面板可查询。
26. 离线模式下确认帧同步面板和录像控制隐藏，后台实时或 Replay Facade 不混入列表或详情；打开损坏 JSON 时确认显示稳定错误码且当前离线现场保持不变。
27. 点击“返回实时”，确认离线来源释放；Play Mode 恢复当前活动会话，Edit Mode 显示可打开文件或进入播放模式的提示。
28. 退出 Play Mode 后再次打开有效 Artifact，确认无需活动 `BattleLogicSession` 也能完成离线 Actor 导航和 DTO 面板浏览，同时不能启动 Replay 逻辑世界。

如果验证的是 DTO-only 面板边界，还应搜索 Panel 代码中是否出现 `SelectedUnit`、`IUnitFacade` 或具体 Runtime 容器读取。

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
静态检查：
已知限制：
```

历史批次中的测试数量只代表当时工作区和结果文件，不应作为当前分支持续通过的证明。
