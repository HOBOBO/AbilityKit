using AbilityKit.Entitas.CodeGen.Model;
using AbilityKit.Entitas.CodeGen.Templating;

namespace AbilityKit.Entitas.CodeGen.Pipeline.Generators;

/// <summary>
/// Contexts.cs：主 partial 必生成；可视化调试 observer partial 按 editor.emitContextObserver 开关生成，
/// 其 #if 守卫宏用 editor.defineSymbols（默认 UNITY_EDITOR，字节级复刻现状）。
/// </summary>
public sealed class ContextsFileGenerator : IGenerator
{
    public string Name => "Contexts";

    public void Generate(CodeGenModel model, FileSet files, PipelineContext ctx)
    {
        var names = model.Contexts.Select(c => c.Name).ToList();

        var main = Templates.ContextsClass(names,
            Templates.Header(HeaderNames.Resolve(model, "Contexts", "Entitas.CodeGeneration.Plugins.ContextsGenerator")));

        if (model.Editor.EmitContextObserver)
        {
            var observer = Templates.ContextsObserver(
                Templates.Header(HeaderNames.Resolve(model, "ContextObserver", "Entitas.VisualDebugging.CodeGeneration.Plugins.ContextObserverGenerator")),
                model.Editor.GuardExpression,
                names);
            files.Add("Contexts.cs", main + "\n\n" + observer + "\n");
        }
        else
        {
            files.Add("Contexts.cs", main + "\n");
        }
    }
}
