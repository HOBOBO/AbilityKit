namespace AbilityKit.Entitas.CodeGen.Pipeline.PostProcessors;

/// <summary>
/// 规范化行尾为 LF（管线内部规范）。最终字节级 EOL/BOM 由 OutputWriter 按配置在写盘时决定，
/// 这样 check 模式的归一化对比与写盘策略互不干扰。
/// </summary>
public sealed class NewLinePostProcessor : IPostProcessor
{
    public string Name => "CanonicalNewLine";

    public FileSet Process(FileSet files, PipelineContext ctx)
    {
        var result = new FileSet();
        foreach (var file in files.Ordered)
        {
            result.Add(file.Path, file.Content.Replace("\r\n", "\n"));
        }
        return result;
    }
}
