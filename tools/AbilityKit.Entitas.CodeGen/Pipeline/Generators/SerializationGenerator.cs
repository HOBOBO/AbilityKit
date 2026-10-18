using AbilityKit.Entitas.CodeGen.Model;
using AbilityKit.Entitas.CodeGen.Templating;

namespace AbilityKit.Entitas.CodeGen.Pipeline.Generators;

/// <summary>
/// 组件逐字段序列化生成器：为含 serialize:true 组件的 context 产出 {Ctx}ComponentSerialization.cs。
/// 消费方（如 MobaEntitasComponentRollbackProvider）的 Write/Restore 委托到该生成物，
/// 手写字段级序列化从此只保留 provider 的编排外壳。
/// </summary>
public sealed class SerializationGenerator : IGenerator
{
    public string Name => "ComponentSerialization";

    public void Generate(CodeGenModel model, FileSet files, PipelineContext ctx)
    {
        foreach (var context in model.Contexts)
        {
            if (!context.Components.Any(c => c.Serialize)) continue;
            files.Add($"{context.Name}/{context.Name}ComponentSerialization.cs",
                SerializationTemplates.ComponentSerialization(
                    context.Name,
                    context.Components.Where(c => c.Serialize && !c.Custom && c.Definition is not null).ToList(),
                    model.SerializablePlainTypes,
                    Templates.Header(HeaderNames.Resolve(model, "ComponentSerialization", "Entitas.CodeGeneration.Plugins.ComponentSerializationGenerator"))));
        }
    }
}
