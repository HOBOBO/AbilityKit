using AbilityKit.Entitas.CodeGen.Model;
using AbilityKit.Entitas.CodeGen.Templating;

namespace AbilityKit.Entitas.CodeGen.Pipeline.Generators;

/// <summary>Feature.cs（可视化调试基类）。editor.emitFeatureClass=false 时整个文件不生成（纯服务端目标）。</summary>
public sealed class FeatureFileGenerator : IGenerator
{
    public string Name => "Feature";

    public void Generate(CodeGenModel model, FileSet files, PipelineContext ctx)
    {
        if (!model.Editor.EmitFeatureClass) return;
        files.Add("Feature.cs", Templates.FeatureClass(model.Editor.GuardExpression));
    }
}
