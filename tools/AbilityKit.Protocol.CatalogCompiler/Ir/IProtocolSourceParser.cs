namespace AbilityKit.Protocol.CatalogCompiler.Ir;

/// <summary>
/// 将单个目录源文档适配为编解码无关中间表示。实现对应 YAML、JSON、数据库等源格式；其余编译流程仅消费返回的 <see cref="ProtocolCatalogIr"/>，新增格式只需新增解析器，无需修改验证或生成。
/// </summary>
public interface IProtocolSourceParser
{
    /// <summary>
    /// 将从 <paramref name="sourcePath"/> 读取的 <paramref name="sourceText"/> 解析为单个目录中间表示节点。文档为空、结构版本不支持或枚举值无效时，抛出 <see cref="System.IO.InvalidDataException"/>。
    /// </summary>
    ProtocolCatalogIr Parse(string sourcePath, string sourceText);
}
