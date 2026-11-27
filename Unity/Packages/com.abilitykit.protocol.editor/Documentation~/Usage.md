# Protocol Editor 工作区使用指南

> 文档类型：使用指南
> 适用范围：`com.abilitykit.protocol.editor` 0.1.0；2026-11-25 仓库工作区源码
> 核校状态：部分核校；窗口、编译器桥接和冻结迁移的 E0 契约已对照源码，局部 .NET E3 见第 6 节
> 事实基线：2026-11-25；未进行 Unity GUI 操作或独立安装验收

## 1. 环境与能力边界

本工具编辑仓库自有协议的 YAML 源文件，复用正式编译器解析、校验、写回和导出。运行时收发、连接、消息路由、serializer 安装和业务处理属于消费者。旧 ScriptableObject 仅为迁移保留。

版本与声明依赖以 [package.json](../package.json) 为准。当前[Editor 程序集](../Editor/com.abilitykit.protocol.editor.asmdef)还引用 `AbilityKit.Editor.Platform`、`AbilityKit.World.Snapshot`、`AbilityKit.World.NetworkFragments`；这些引用没有全部列入 package 依赖，不能据此声明独立 UPM 依赖闭包已完整。

现有桥接按 Unity `Application.dataPath` 上两级定位仓库根，并读取根下的 `Protocols/Catalogs`、`Protocols/WireSchemas` 和 `tools/AbilityKit.Protocol.CatalogCompiler`。执行 `dotnet run --no-restore`，因此需事先安装匹配编译器工程的 .NET SDK、恢复依赖并使 `dotnet` 可从 PATH 调用；目录编译按钮另依赖 Windows `powershell.exe`。在其他项目中仅安装此包、移动 Unity 工程，或在未验证平台运行，均不能假定这些条件成立。

## 2. 操作步骤与写入边界

打开 `Tools/AbilityKit/Framework/Protocol/Protocol Workspace`。窗口启用时加载 YAML，界面分为“协议目录”“线协议结构”“导出”。加载成功后应看到项目、目录和类型，并检查诊断列表；进程退出成功不代表诊断为空。

| 操作 | 输入与预期结果 | 文件副作用 |
| --- | --- | --- |
| 打开或重新加载 | 编译器产生工作区投影，窗口显示目录/类型/诊断 | 写 `Unity/Library/AbilityKit/ProtocolEditor/workspace.json` 及编译缓存，不改 YAML；重载未保存编辑前有丢弃确认 |
| 保存所选目录 | 校验当前目录，规范化写回后重载 | 覆盖该目录的 `sourcePath` YAML；不是保存全部未提交文件 |
| 保存所选类型 | 校验字段与保留 ID，更新所属分组文档后重载 | 写该类型的 `sourcePath` YAML，保留组内其他类型及分组归属 |
| 编译协议目录 | 要求先保存未保存编辑，调用仓库正式编译脚本 | 更新仓库配置的 manifest、catalog、metadata/API 等生成物；这不是只读诊断按钮 |
| 导出单个项目 | 选择项目、输出目录和 catalog 命名空间，导出 MemoryPack DTO 与目录源码 | 写所选输出目录，默认 `local/ProtocolExports`；读取磁盘上的 YAML，应先保存编辑 |

保存的中间 JSON 也在 `Unity/Library/AbilityKit/ProtocolEditor`。编译/导出后会刷新 AssetDatabase；输出目录若位于 Unity 导入范围，会触发相应导入。导出成功不能代替目标程序集编译和兼容验证。桥接同步等待子进程结束，当前没有后台取消或超时保证，大型工作区操作可能阻塞窗口。

先检查版本差异再保留 YAML 与生成物；不要通过直接修改生成 C# 修复源定义。运行报告按宿主仓库规范单独归档，不作为协议资产提交。

## 3. 目录与线协议结构契约

目录与线协议结构分别治理传输标识/策略和载荷字段/序列化布局；它们不是可互换的两种协议源。

- 目录编辑项目、消息、方向、消息种类、操作码、payload 类型与兼容元数据。诊断应在编译、导出与协议评审前处理，不能依靠编辑器自动建立业务 handler 或 decoder。
- 分组线协议 v2 把相同项目、`groupId` 和命名空间下的类型归在同一 YAML。选中组内类型后，“添加类型”暂存新条目，保存时追加；`+` 创建新的分组文档。
- 类型可以重命名，但重名与归属变更由编译器拒绝。文档级 defaults、project/group/namespace 元数据由版本管理中的 YAML 维护；窗口显示的是类型的最终有效设置，不应把保存一个类型理解为修改全组默认值。
- 缺省生成设置为 `version-tolerant`、`class`、`property`。既有按位置序列化契约可选 `sequential`、`struct`、`field`；顺序模式字段 ID 必须从零连续编号。变更模式或字段布局需由所属协议维护者评审，不能以“能导出”证明字节兼容。
- 项目导出沿自定义类型引用包含自有结构的依赖闭包；`includeUnreferenced` 额外包含未引用结构。`external` 只用于已由其他程序集拥有的确切类型，不应用于掩盖遗漏定义；导出不负责安装那些外部程序集。

## 4. 旧版资产的一次性迁移

`ProtocolDefinition` 路径为 **FROZEN / superseded**，无 CreateAssetMenu，类型带 `[Obsolete]`。旧代码生成器与快照路由导入器已删除。不要新建旧资产或恢复旧生成路径。

使用 `Tools/AbilityKit/Framework/Protocol/Migrate Legacy ProtocolDefinition (one-time)`，选择既有资产并填写项目、目录和领域信息，再选择 YAML 输出位置。迁移窗口只读取旧资产，通过同一编译器写目录 YAML；它不生成 DTO、操作码或路由胶水，也不自动完成所有 wire schema 和业务装配。

迁移后在正式工作区检查目录、补齐所需 wire schema，并校验消费者与生成差异；确认没有资产引用依赖后再删除旧资产。可从版本管理回退源文件和生成物，迁移窗口不会替调用者清理引用。

## 5. 常见问题

| 症状 | 核对与处理 |
| --- | --- |
| 找不到编译器或 YAML 根目录 | 检查是否使用预期仓库布局；路径按 `Application.dataPath` 推导，当前没有任意 UPM 项目根自动发现保证 |
| `dotnet` 无法启动或缺少 assets 文件 | 检查 PATH、SDK 与编译器工程依赖恢复；桥接使用 `--no-restore` |
| 程序集引用缺失 | 对照 asmdef 和宿主安装，特别是 Editor Platform、Snapshot、NetworkFragments；不要把仅有 package.json 安装视为验证完成 |
| 加载成功仍有诊断 | 区分命令执行结果与目录、类型覆盖诊断；根据文件和条目修正源 YAML |
| 导出没有反映修改 | 先保存 YAML，再导出；检查项目筛选、依赖闭包、`external` 和输出目录 |
| 编译按钮改了仓库生成文件 | 这是正式编译动作；只做核查时使用宿主提供的只读检查路径并审阅差异 |

## 6. 验证范围与关联资料

本指南对照同一个[窗口与桥接实现](../Editor/ProtocolEditor/UI/ProtocolEditorWindow.cs)和[冻结 Schema](../Editor/ProtocolEditor/Schema/ProtocolDefinition.cs)。2026-11-25 的本地工作区投影加载 4 个目录和 62 个展开类型，零诊断；Protocol 测试 171/171（含冻结守护 6/6）通过；编译器测试 103/104 通过，当前协议源与已提交兼容基线的一致性断言失败。本批没有更新基线来消除失败。证据限于这些局部 E3，命令和原始结果由宿主仓库批次记录维护。

这些检查没有启动 Unity 窗口、写入真实协议 YAML、导出正式入库产物，或验证独立包安装、跨平台进程调用、协议跨版本和真实收发。历史资料见[冻结设计文档](../Document/ProtocolEditor协议编辑器模块开发设计文档.md)，其旧生成 API 仅作背景。
