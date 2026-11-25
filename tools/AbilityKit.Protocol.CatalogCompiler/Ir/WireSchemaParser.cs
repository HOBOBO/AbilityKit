namespace AbilityKit.Protocol.CatalogCompiler.Ir;

/// <summary>
/// YAML 线协议源解析器的稳定便捷入口。
/// </summary>
/// <remarks>
/// 当前结构有意与源格式绑定；此外观为后续 JSON 源解析器保留空间，避免消费方耦合到 YAML 实现。
/// </remarks>
public sealed class WireSchemaParser : IWireSchemaParser
{
    private readonly YamlWireSchemaParser _yaml = new();

    public WireSchemaIr Parse(string sourcePath, string sourceText) => _yaml.Parse(sourcePath, sourceText);

    public WireSchemaDocumentIr ParseDocument(string sourcePath, string sourceText) =>
        _yaml.ParseDocument(sourcePath, sourceText);
}
