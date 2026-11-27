# com.abilitykit.protocol.editor

用于仓库自有协议 YAML 工作区的 Unity 编辑器工具。

## 环境与首次使用

版本、Unity 最低版本与声明依赖见 [package.json](package.json)。本工具仅在 Editor 中运行；当前桥接依赖 AbilityKit 仓库布局、可在 PATH 调用的 .NET SDK、已恢复依赖的协议编译器工程，以及 Windows `powershell.exe`。只安装此 UPM 包不能获得完整工作区。程序集还引用 `AbilityKit.Editor.Platform`、Snapshot 与 NetworkFragments，需由宿主项目提供，详见[使用指南](Documentation~/Usage.md#1-环境与能力边界)。

在已具备这些前置条件的仓库 Unity 工程中，从下述菜单加载工作区，先检查诊断，再按任务保存 YAML、编译目录或导出项目。保存、编译和导出都有文件写入副作用；首次操作前请阅读[操作与写入边界](Documentation~/Usage.md#2-操作步骤与写入边界)。

## YAML 工作区

打开 `Tools/AbilityKit/Framework/Protocol/Protocol Workspace`，可编辑并验证协议目录及分组线协议结构文档，检查项目、消息和类型覆盖，并导出单个项目的 MemoryPack DTO 与协议目录源码。

新结构默认采用带属性的版本容忍类。既有按位置序列化的 MemoryPack 契约可选择 `sequential`、`struct` 和字段成员；顺序模式的字段 ID 必须从零连续编号。项目导出会沿自定义类型引用纳入完整的自有结构依赖闭包。

YAML 是权威来源。编辑器调用 `AbilityKit.Protocol.CatalogCompiler` 解析、验证、规范化写入及导出，使 CLI、CI 和 Unity 共用一套实现。

线协议结构 v2 按分组文档编辑。选择组内任意类型后，使用 **添加类型** 暂存同一 YAML 文件中的新类型；保存时追加，并保留组内其他类型及文档归属。`+` 按钮创建新分组文档。既有类型可重命名，编译器会拒绝重名和归属变更。检查器显示每个类型的最终有效生成设置；文档级默认值及归属元数据仍由版本管理中的 YAML 维护，防止修改单个类型时静默改写同组其他类型。

## 旧版 ScriptableObject 路径已冻结（FROZEN / superseded，2026-08）

旧的双入口路径——`ProtocolDefinition` ScriptableObject 资产与 `ProtocolCodeGenerator`——已冻结，并已退出编辑和生成流程。YAML 协议工作区是唯一正式入口。

- `ProtocolDefinition` 的 `[CreateAssetMenu]` 已移除；结构类型标记为 `[Obsolete]`，仅以只读形式保留，确保既有 `.asset` 仍能反序列化。
- 旧版生成器（MemoryPack DTO、`OpCodes.g.cs`、快照路由衔接代码、编解码后端桩）和快照路由导入器已删除。除正式编译器导出外，本包不再生成 C# 代码。
- 保留一个明确标记的一次性迁移入口：`Tools/AbilityKit/Framework/Protocol/Migrate Legacy ProtocolDefinition (one-time)`。它只读取旧 `ProtocolDefinition` 资产，通过与工作区相同的 `AbilityKit.Protocol.CatalogCompiler` 验证及写入路径生成 YAML 协议目录。迁移后应删除旧资产。

## 文档

- [使用文档索引](Documentation~/README.md)与[工作区使用指南](Documentation~/Usage.md)：前置环境、写入范围、字段规则、迁移和排错。
- [冻结设计资料](Document/ProtocolEditor协议编辑器模块开发设计文档.md)：保留旧路径供历史引用；不作为现行生成 API 教程。

2026-11-25 仅核对源码契约及本地编译器/冻结守护测试；未进行 Unity GUI、独立 UPM 安装或 Player 验收。
