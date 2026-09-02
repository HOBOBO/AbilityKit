namespace AbilityKit.Protocol.CatalogCompiler.Ir;

/// <summary>Stable convenience name for the YAML wire-schema source parser.</summary>
/// <remarks>The schema is intentionally source-format specific today; this facade leaves room
/// for a JSON source parser without coupling consumers to the YAML implementation.</remarks>
public sealed class WireSchemaParser : IWireSchemaParser
{
    private readonly YamlWireSchemaParser _yaml = new();

    public WireSchemaIr Parse(string sourcePath, string sourceText) => _yaml.Parse(sourcePath, sourceText);

    public WireSchemaDocumentIr ParseDocument(string sourcePath, string sourceText) =>
        _yaml.ParseDocument(sourcePath, sourceText);
}
