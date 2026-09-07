using System.Collections.Generic;

namespace AbilityKit.BehaviorTree.Authoring
{
    public enum SourceKind
    {
        RuntimeDefinition = 0,
        AuthoringDocument = 1,
    }

    public sealed class ProjectManifest
    {
        public List<string> Trees { get; set; } = new();
        public string SourceDirectory { get; set; } = "";
        public SourceKind SourceKind { get; set; } = SourceKind.RuntimeDefinition;
        public List<string> ExportTargets { get; set; } = new();
    }

    /// <summary>项目清单中的源文件契约。</summary>
    /// <summary>
    /// 导出项目清单（纯数据）：声明一组树 + 源 JSON 目录 + 导出目标。
    /// 是 headless 导出（CLI/CI/AI 脚本）与编辑器项目目录资产共用的协议——同一份清单
    /// 既可被 `dotnet run` 消费，也可被编辑器导入成 `AuthoringProjectAsset`。
    /// </summary>
}
