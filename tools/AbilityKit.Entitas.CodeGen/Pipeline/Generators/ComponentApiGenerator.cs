using AbilityKit.Entitas.CodeGen.Model;
using AbilityKit.Entitas.CodeGen.Templating;

namespace AbilityKit.Entitas.CodeGen.Pipeline.Generators;

/// <summary>组件 API 文件（entity partial + matcher partial）。custom 组件跳过（手工维护，只做存在性校验）。</summary>
public sealed class ComponentApiGenerator : IGenerator
{
    public string Name => "ComponentApi";

    public void Generate(CodeGenModel model, FileSet files, PipelineContext ctx)
    {
        foreach (var context in model.Contexts)
        {
            foreach (var pinned in context.Components)
            {
                if (!pinned.Generate || pinned.Custom || pinned.Definition is null) continue;
                var path = $"{context.Name}/Components/{context.Name}{pinned.Name}Component.cs";
                files.Add(path, Templates.ComponentApi(context.Name, pinned.Definition,
                    Templates.Header(HeaderNames.Resolve(model, "ComponentEntityApi", "Entitas.CodeGeneration.Plugins.ComponentEntityApiGenerator")),
                    Templates.Header(HeaderNames.Resolve(model, "ComponentMatcherApi", "Entitas.CodeGeneration.Plugins.ComponentMatcherApiGenerator"))));
            }
        }
    }
}
