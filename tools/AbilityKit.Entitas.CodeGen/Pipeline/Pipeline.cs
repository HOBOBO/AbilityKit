using AbilityKit.Entitas.CodeGen.Configuration;
using AbilityKit.Entitas.CodeGen.Diagnostics;
using AbilityKit.Entitas.CodeGen.Model;
using AbilityKit.Entitas.CodeGen.Pipeline.Generators;
using AbilityKit.Entitas.CodeGen.Pipeline.PostProcessors;

namespace AbilityKit.Entitas.CodeGen.Pipeline;

/// <summary>生成文件（相对路径 + 内容）。内容行尾规范为 LF，后处理器/写盘按配置转换。</summary>
public sealed record GeneratedFile(string Path, string Content);

/// <summary>有序文件集：按路径 Ordinal 排序保证确定性；重复路径视为管线 bug。</summary>
public sealed class FileSet
{
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _files.Count;

    public IEnumerable<GeneratedFile> Ordered =>
        _files.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => new GeneratedFile(kv.Key, kv.Value));

    public void Add(string path, string content)
    {
        if (!_files.TryAdd(path, content))
            throw new CodeGenException($"管线产出重复文件: {path}", ExitCodes.Internal);
    }

    public bool Contains(string path) => _files.ContainsKey(path);
}

/// <summary>管线上下文：生成器/后处理器需要的配置面。</summary>
public sealed class PipelineContext(string repoRoot, CodeGenConfig config)
{
    public string RepoRoot { get; } = repoRoot;
    public CodeGenConfig Config { get; } = config;
}

/// <summary>
/// 生成器：从模型产出文件。对照 Jenny 的 CodeGenerator 阶段——
/// 每个生成器独立消费同一份模型，互不依赖，新增输出物（序列化/archetype matcher/entity index）= 新增一个类。
/// </summary>
public interface IGenerator
{
    string Name { get; }
    void Generate(CodeGenModel model, FileSet files, PipelineContext ctx);
}

/// <summary>
/// 后处理器：对文件集做内容/清单级变换。对照 Jenny 的 PostProcessor 阶段
/// （行尾、BOM、清理陈旧文件、未来可能的 manifest 生成）。
/// </summary>
public interface IPostProcessor
{
    string Name { get; }
    FileSet Process(FileSet files, PipelineContext ctx);
}

/// <summary>三段式管线执行器：generators → post-processors。确定性：文件集排序 + 管线顺序注册。</summary>
public sealed class Pipeline
{
    private readonly IReadOnlyList<IGenerator> _generators;
    private readonly IReadOnlyList<IPostProcessor> _postProcessors;

    public Pipeline(IEnumerable<IGenerator> generators, IEnumerable<IPostProcessor> postProcessors)
    {
        _generators = generators.ToList();
        _postProcessors = postProcessors.ToList();
    }

    /// <summary>默认管线（生产配置）。新增生成器在这里注册。</summary>
    public static Pipeline CreateDefault() => new(
        new IGenerator[]
        {
            new ContextFilesGenerator(),
            new ComponentApiGenerator(),
            new SerializationGenerator(),
            new ContextsFileGenerator(),
            new FeatureFileGenerator(),
        },
        new IPostProcessor[]
        {
            new NewLinePostProcessor(),
        });

    public FileSet Run(CodeGenModel model, PipelineContext ctx)
    {
        var files = new FileSet();
        foreach (var generator in _generators)
        {
            generator.Generate(model, files, ctx);
        }

        foreach (var post in _postProcessors)
        {
            files = post.Process(files, ctx);
        }

        return files;
    }
}
