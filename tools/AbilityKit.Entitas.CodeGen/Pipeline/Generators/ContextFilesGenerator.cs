using AbilityKit.Entitas.CodeGen.Model;
using AbilityKit.Entitas.CodeGen.Templating;

namespace AbilityKit.Entitas.CodeGen.Pipeline.Generators;

/// <summary>context 级五件套：Attribute/Context/Entity/Matcher/ComponentsLookup。</summary>
public sealed class ContextFilesGenerator : IGenerator
{
    public string Name => "Context";

    public void Generate(CodeGenModel model, FileSet files, PipelineContext ctx)
    {
        foreach (var context in model.Contexts)
        {
            var c = context.Name;
            files.Add($"{c}/{c}Attribute.cs",
                Templates.ContextAttribute(c, Templates.Header(HeaderNames.Resolve(model, "ContextAttribute", "Entitas.CodeGeneration.Plugins.ContextAttributeGenerator"))));
            files.Add($"{c}/{c}Context.cs",
                Templates.ContextClass(c, Templates.Header(HeaderNames.Resolve(model, "Context", "Entitas.CodeGeneration.Plugins.ContextGenerator"))));
            files.Add($"{c}/{c}Entity.cs",
                Templates.EntityClass(c, Templates.Header(HeaderNames.Resolve(model, "Entity", "Entitas.CodeGeneration.Plugins.EntityGenerator"))));
            files.Add($"{c}/{c}Matcher.cs",
                Templates.MatcherClass(c, Templates.Header(HeaderNames.Resolve(model, "ContextMatcher", "Entitas.CodeGeneration.Plugins.ContextMatcherGenerator"))));

            var entries = context.Components
                .Where(p => p.Generate && p.Definition is not null)
                .Select(p => new Templates.LookupEntry(p.Name, p.Definition!.ClassFqn))
                .ToList();
            files.Add($"{c}/{c}ComponentsLookup.cs",
                Templates.ComponentsLookup(c, entries, Templates.Header(HeaderNames.Resolve(model, "ComponentLookup", "Entitas.CodeGeneration.Plugins.ComponentLookupGenerator"))));
        }
    }
}
