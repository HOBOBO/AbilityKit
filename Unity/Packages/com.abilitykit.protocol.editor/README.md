# com.abilitykit.protocol.editor

用于仓库自有协议 YAML 工作区的 Unity 编辑器工具。

打开 `Tools/AbilityKit/Framework/Protocol/Protocol Workspace`，可编辑并验证协议目录及分组线协议结构文档，检查项目、消息和类型覆盖，并导出单个项目的 MemoryPack DTO 与协议目录源码。

新结构默认采用带属性的版本容忍类。既有按位置序列化的 MemoryPack 契约可选择 `sequential`、`struct` 和字段成员；顺序模式的字段 ID 必须从零连续编号。项目导出会沿自定义类型引用纳入完整的自有结构依赖闭包。

YAML 是权威来源。编辑器调用 `AbilityKit.Protocol.CatalogCompiler` 解析、验证、规范化写入及导出，使 CLI、CI 和 Unity 共用一套实现。

线协议结构 v2 按分组文档编辑。选择组内任意类型后，使用 **添加类型** 暂存同一 YAML 文件中的新类型；保存时追加，并保留组内其他类型及文档归属。`+` 按钮创建新分组文档。既有类型可重命名，编译器会拒绝重名和归属变更。检查器显示每个类型的最终有效生成设置；文档级默认值及归属元数据仍由版本管理中的 YAML 维护，防止修改单个类型时静默改写同组其他类型。

## 旧版 ScriptableObject 路径已冻结（2026-08 被替代）

旧的双入口路径——`ProtocolDefinition` ScriptableObject 资产与 `ProtocolCodeGenerator`——已冻结，并已退出编辑和生成流程。YAML 协议工作区是唯一正式入口。

- `ProtocolDefinition` 的 `[CreateAssetMenu]` 已移除；结构类型标记为 `[Obsolete]`，仅以只读形式保留，确保既有 `.asset` 仍能反序列化。
- 旧版生成器（MemoryPack DTO、`OpCodes.g.cs`、快照路由衔接代码、编解码后端桩）和快照路由导入器已删除。除正式编译器导出外，本包不再生成 C# 代码。
- 保留一个明确标记的一次性迁移入口：`Tools/AbilityKit/Framework/Protocol/Migrate Legacy ProtocolDefinition (one-time)`。它只读取旧 `ProtocolDefinition` 资产，通过与工作区相同的 `AbilityKit.Protocol.CatalogCompiler` 验证及写入路径生成 YAML 协议目录。迁移后应删除旧资产。
