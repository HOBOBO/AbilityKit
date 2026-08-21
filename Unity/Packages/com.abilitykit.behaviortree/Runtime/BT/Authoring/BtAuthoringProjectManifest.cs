using System.Collections.Generic;

namespace AbilityKit.BehaviorTree.Authoring
{
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

        /// <summary>导出目标目录列表（相对仓库根；扇出到全部）。</summary>
        public List<string> ExportTargets { get; set; } = new();
    }
}
