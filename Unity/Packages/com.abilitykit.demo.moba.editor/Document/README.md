# AbilityKit Demo MOBA Editor 工具指南

> 适用包：`com.abilitykit.demo.moba.editor`
>
> Unity 版本：2022.3 或更高的 2022.3 LTS 版本
>
> 最后更新：2026-07-19

本包提供 MOBA 示例的配置生产、场景预览、战斗诊断、帧同步检查、Scene Gizmo 和热重载等 Unity Editor 工具。本文是工具使用与维护文档的统一入口。

## 快速开始

### 打开战斗调试窗口

1. 在 Unity 中打开 MOBA 示例工程。
2. 打开可启动 `BattleLogicSession` 的 MOBA 战斗场景。
3. 进入 Play Mode，并等待战斗会话启动。
4. 选择菜单 `Tools/AbilityKit/Battle/战斗调试`。
5. 在左侧实体列表选择 Actor，在右侧先选择 `Actor` 或 `Diagnostics` 工作区，再从“面板”下拉框切换工具。

窗口只在 Play Mode 下工作。未进入 Play Mode、`BattleDebugFacadeProvider.Current` 为空或没有活动 `BattleLogicSession` 时，窗口会显示对应提示。

### 常用操作

- 在顶部“过滤”输入框按实体标识或工具支持的文本条件筛选实体；计数显示“可见/总数”，可点击 `×` 清除过滤。
- 在左侧输入 Actor ID 后点击“跳转”，或使用 `<` / `>` 在当前可见实体间循环定位；`×` 清除当前 Actor 选择。实体被过滤时选择保持不变并显示提示；实体离开当前世界时不会自动切换到相邻对象。
- 点击“刷新”立即检查实体列表；窗口默认每 0.25 秒自动检查，可关闭“自动刷新”暂停窗口轮询。该开关不冻结也不停止底层诊断采集。
- 拖动实体列表和详情区之间的分隔条可调整左栏宽度；栏宽、当前工作区和各工作区面板位置会在窗口关闭后保留。
- 在 `Actor` 工作区查看“总览”“属性”“标签”“效果”“Buff”等选中实体快照。
- 在 `Diagnostics` 工作区查看帧同步、诊断事件、Trace 和诊断状态等全局信息。
- 在“诊断事件”中按选中 Actor、失败结果和文本条件过滤事件；点击事件序列号可查看完整上下文和 Payload。
- 事件带有 Root Context ID 时，可在详情中点击“打开 Trace”，自动切换并定位对应 Trace 节点；手工输入 Root Context ID 仍作为辅助入口。
- Trace 支持按 Kind、状态、结束原因、Context、Actor 或 Config 搜索；搜索结果保留祖先路径。可在直接命中之间循环前后定位、批量展开/折叠分支，也可 Pin 当前节点并在浏览其他节点后快速返回。程序化定位会将目标节点滚动回树视口。
- 在“诊断状态”中点击 Actor ID，可同步更新窗口实体选择。
- 面板提供复制按钮时，复制内容来自诊断 DTO 在采样时固化的数据。

## 当前面板

| 面板 | 主要用途 | 数据边界 |
| --- | --- | --- |
| 总览 | Actor 类型、名称、Tag 和 Effect 数量 | Actor、Tag、Effect 只读诊断查询 |
| 属性 | 属性最终值及 Modifier | Actor Attributes 只读诊断查询 |
| 标签 | Actor 当前 Tag | Actor Tags 只读诊断查询 |
| 效果 | Ability Effect 实例和计时状态 | Actor Effects 只读诊断查询 |
| Buff | MOBA Buff 实例和上下文 | Actor Buffs 只读诊断查询 |
| 诊断事件 | 战斗事件过滤与结果检查 | Event Ring Store 查询 |
| Trace | 按 Root Context ID 查看、搜索与命中导航、批量折叠 Trace 树，检查节点状态、父链路径和临时 Pin | Trace Read Store 查询 |
| 诊断状态 | World 与 Actor 最新状态 | State Store 查询 |
| 帧同步/* | 帧、预测、回滚、对账和网络状态 | 既有帧同步调试接口；按运行环境动态显示 |

诊断面板只消费 `IBattleDiagnosticReadOnlySession` 返回的不可变 DTO，不应回退读取活动 Runtime 对象。主窗口以稳定 Actor ID 保存选择，并通过窄导航命令支持 State → Actor 和 Event → Trace；实体枚举、选择解析，以及左侧列表中的 Tag/Effect 简要计数仍依赖活动 `IBattleDebugFacade` 和 `IUnitFacade`，这部分不属于已完成的面板 DTO 化范围。

## 使用边界

- 当前可用入口是本地 Unity Play Mode。
- 当前状态类 Actor Store 使用 latest-only 语义：帧 `0` 表示最新快照；指定帧只有等于当前快照帧时才可查询。
- 事件 Store 有界保留历史，并以 revision 保证同一次分页读取的一致性。
- 远端会话、离线文件、导出、完整 Timeline 和完整 Skill Runtime 检查尚未形成可用工作流。
- 本工具是只读诊断客户端，不应修改生命、属性、Buff、Effect、技能运行时或 Trace。
- 工具不替代 Unity Profiler、Memory Profiler、正式 Record/Replay 或网络抓包工具。

## 其他 Editor 工具入口

工具入口分布在 Unity 顶部的 `Tools`、`AbilityKit` 和 `Moba` 三个菜单根下。窗口类工具会打开独立窗口；命令类工具执行后在 Console 输出结果；Scene Gizmo 是 Scene View 叠加层，不会打开窗口。

### 窗口与场景命令

| 类型 | 工具 | 菜单入口 | 使用前提 |
| --- | --- | --- | --- |
| 窗口 | 战斗调试 | `Tools/AbilityKit/Battle/战斗调试` | 进入 Play Mode 并启动战斗会话 |
| 命令 | 打开 Demo 场景 | `Tools/AbilityKit/MOBA Demo/Open Demo Scene` | 无 |
| 命令 | 创建或刷新 Demo 场景 | `Tools/AbilityKit/MOBA Demo/Create Or Refresh Demo Scene` | 会创建或改写 Demo 场景资产 |
| 命令 | 播放 Demo 场景 | `Tools/AbilityKit/MOBA Demo/Play Demo Scene` | 自动打开场景并进入 Play Mode |
| 窗口 | Editor Flow Pump | `Tools/AbilityKit/Preview/编辑器驱动(Flow Pump)` | 按窗口内流程驱动预览 |
| 窗口 | 帧同步测试 | `Tools/AbilityKit/FrameSync Test` | 按窗口内配置运行测试 |
| 命令 | 编译 Hotfix | `Tools/AbilityKit/Hot Reload/Compile Hotfix` | 工程需具备 Hotfix 编译环境 |
| 命令 | 重载 Hotfix | `Tools/AbilityKit/Hot Reload/Reload Hotfix` | 先完成 Hotfix 编译 |
| 窗口 | 创建英雄 | `AbilityKit/Moba/Hero/Create New Hero…` | 按向导填写英雄资源信息 |

### Scene Gizmo

进入 Play Mode 并启动战斗会话后，在 Unity 顶部 `Moba/Gizmos` 菜单勾选需要的叠加层，然后查看 Scene View。它们读取逻辑战斗 World，表示判定位置和范围；表现模型启用插值时，移动中的模型可能与逻辑位置存在短暂视觉差异。

| 功能 | 菜单入口 |
| --- | --- |
| 攻击范围开关 | `Moba/Gizmos/Attack Range` |
| Buff 范围开关 | `Moba/Gizmos/Buff Range` |
| 出生区域开关 | `Moba/Gizmos/Spawn Area` |
| 恢复默认开关 | `Moba/Gizmos/Reset Defaults` |
| 清空出生点缓存 | `Moba/Gizmos/Clear Spawn Cache` |

### 配置 JSON 与 VFX

| 命令 | 菜单入口 | Project 窗口选择要求 |
| --- | --- | --- |
| 导出选中目录的 MOBA 配置 | `AbilityKit/Moba/Export Config Json` | 选择目标目录或目录内资产；未选择时扫描 `Assets` |
| 文件夹 JSON 导入选中配置 SO | `AbilityKit/Moba/Config Json/Import Folder -> Selected SO` | 选择一个 `MobaConfigTableAssetSO` |
| 选中配置 SO 导出到文件夹 | `AbilityKit/Moba/Config Json/Export Selected SO -> Folder` | 选择一个 `MobaConfigTableAssetSO` |
| 数组 JSON 拆分到文件夹 | `AbilityKit/Moba/Config Json/Export Array Json -> Folder (Selected SO Type)` | 选择一个 `MobaConfigTableAssetSO` 以确定配置类型 |
| 文件夹合并为数组 JSON | `AbilityKit/Moba/Config Json/Import Folder -> Array Json (Selected SO Type)` | 选择一个 `MobaConfigTableAssetSO` 以确定配置类型 |
| 批量导入选中目录内全部配置 SO | `AbilityKit/Moba/Config Json/Import Folder -> All SOs In Selected Folder` | 选择包含配置 SO 的目录或目录内资产 |
| 批量导出选中目录内全部配置 SO | `AbilityKit/Moba/Config Json/Export Folder -> All SOs In Selected Folder` | 选择包含配置 SO 的目录或目录内资产 |
| 导出选中目录的 VFX 配置 | `AbilityKit/Vfx/Export Vfx Json` | 选择目标目录或目录内资产；未选择时扫描 `Assets` |
| 导出全部 Assets 下的 VFX 配置 | `AbilityKit/Vfx/Export Vfx Json (Assets)` | 无 |

导入命令会修改配置资产或 JSON 文件；执行前应确认版本控制状态。导出结果写入 `com.abilitykit.demo.moba.view.runtime` 包的 Resources 目录，并在完成后刷新 AssetDatabase。

## 文档导航

- [当前能力与限制](CURRENT-CAPABILITIES.md)：查询、能力位、面板、Producer 及未实现范围的当前快照。
- [故障排查](TROUBLESHOOTING.md)：Session、Capability、查询状态、刷新和 Unity 工程问题。
- [扩展开发指南](EXTENDING.md)：新增 DTO、Store、Session Query、ViewModel 和 Panel 的标准步骤。
- [测试与验证](TESTING.md)：构建、EditMode 测试、元数据和提交前检查。
- [架构设计](Moba战斗诊断与溯源工具设计.md)：长期目标、架构约束、数据模型与设计决策。
- [实施历史](IMPLEMENTATION-HISTORY.md)：按批次记录已完成的诊断正式化工作和当时验证结果。
- [Editor 包模块设计](DemoMobaEditor示例编辑器工具模块开发设计文档.md)：整个 Editor 包的职责和依赖边界。

文档中“当前能力”以 [当前能力与限制](CURRENT-CAPABILITIES.md) 为准；架构设计和实施历史中的阶段性描述不替代当前能力快照。
