using System.Collections.Generic;

namespace AbilityKit.BehaviorTree.Authoring
{
    /// <summary>项目清单中的源文件契约。</summary>
    public enum BtAuthoringSourceKind
    {
        /// <summary>兼容旧清单：源文件本身是运行时 IR。</summary>
        RuntimeDefinition = 0,
        /// <summary>推荐：源文件是含独立编辑元数据的授权文档。</summary>
        AuthoringDocument = 1,
    }

    /// <summary>
    /// 导出项目清单（纯数据）：声明一组树 + 源 JSON 目录 + 导出目标。
    /// 是 headless 导出（CLI/CI/AI 脚本）与编辑器项目目录资产共用的协议——同一份清单
    /// 既可被 `dotnet run` 消费，也可被编辑器导入成 `BtAuthoringProjectAsset`。
    /// </summary>
    public sealed class BtAuthoringProjectManifest
    {
        /// <summary>树 id 列表（= 源文件名，不含扩展名）。</summary>
        public List<string> Trees { get; set; } = new();

        /// <summary>源 JSON 目录（相对仓库根；绝对路径亦可）。</summary>
        public string SourceDirectory { get; set; } = "";

        /// <summary>源目录内 JSON 的契约；默认兼容既有运行时定义目录。</summary>
        public BtAuthoringSourceKind SourceKind { get; set; } = BtAuthoringSourceKind.RuntimeDefinition;

        /// <summary>导出目标目录列表（相对仓库根；扇出到全部）。</summary>
        public List<string> ExportTargets { get; set; } = new();
    }
}
