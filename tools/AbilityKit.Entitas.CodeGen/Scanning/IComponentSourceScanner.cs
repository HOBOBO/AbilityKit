namespace AbilityKit.Entitas.CodeGen.Scanning;

/// <summary>
/// 组件源扫描器接口。两个实现：
/// - <see cref="Scanner"/>（零依赖正则，默认——离线可跑、快；字段类型是未解析短名，由 ModelBuilder 经 TypeResolver 解析）；
/// - <see cref="RoslynComponentScanner"/>（语义级解析，字段类型直接给 FQN）。
/// 由配置 scanner 项选择；管线不动。
/// </summary>
public interface IComponentSourceScanner
{
    /// <summary>true = RawField.TypeText 已是全限定名，ModelBuilder 跳过 TypeResolver。</summary>
    bool ProvidesResolvedFieldTypes { get; }

    /// <summary>扫描源目录，返回原始组件定义。源码不合规时抛 <see cref="Diagnostics.CodeGenException"/>（exit 3）。</summary>
    List<Scanner.RawComponent> Scan(IEnumerable<string> sourceDirs);

    /// <summary>扫描普通类型（嵌套数据类/枚举），供序列化生成器展开。</summary>
    List<Scanner.RawPlainType> ScanPlainTypes(IEnumerable<string> sourceDirs);
}
