namespace AbilityKit.Protocol.CatalogCompiler.Ir;

/// <summary>
/// Adapts one catalog source document into the codec-neutral IR. Implementations are specific
/// to a source format (YAML, JSON, a database, …); the rest of the compiler consumes only the
/// <see cref="ProtocolCatalogIr"/> this returns, so a new source format means a new parser and
/// no changes to validation or emission.
/// </summary>
public interface IProtocolSourceParser
{
    /// <summary>
    /// Parses <paramref name="sourceText"/> (read from <paramref name="sourcePath"/>) into a
    /// single catalog IR node. Throws <see cref="System.IO.InvalidDataException"/> when the
    /// document is empty, uses an unsupported schema version, or carries an invalid enum value.
    /// </summary>
    ProtocolCatalogIr Parse(string sourcePath, string sourceText);
}
