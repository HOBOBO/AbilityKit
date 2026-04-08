/// <summary>
/// 文件名称: GeneratorContext.cs
/// 
/// 功能描述: 定义代码生成器的上下文对象，包含生成代码所需的所有信息。
/// 
/// 创建日期: 2026-04-06
/// 修改日期: 2026-04-06
/// </summary>

using System;
using System.Collections.Generic;
using System.Threading;

namespace AbilityKit.CodeGen.Core
{

/// <summary>
/// 代码生成器上下文。
/// </summary>
public readonly struct GeneratorContext
{
    /// <summary>被特性标记的符号信息</summary>
    public ISymbolInfo TargetSymbol { get; }

    /// <summary>触发代码生成的特性信息</summary>
    public AttributeInfo SourceAttribute { get; }

    /// <summary>目标符号的声明类型信息</summary>
    public ITypeInfo DeclaringType { get; }

    /// <summary>包含目标符号的程序集信息</summary>
    public IAssemblyInfo Assembly { get; }

    /// <summary>当前编译会话的信息</summary>
    public ICompilationInfo Compilation { get; }

    /// <summary>用于取消生成操作的取消令牌</summary>
    public CancellationToken CancellationToken { get; }

    private GeneratorContext(
        ISymbolInfo targetSymbol,
        AttributeInfo sourceAttribute,
        ITypeInfo declaringType,
        IAssemblyInfo assembly,
        ICompilationInfo compilation,
        CancellationToken cancellationToken)
    {
        TargetSymbol = targetSymbol;
        SourceAttribute = sourceAttribute;
        DeclaringType = declaringType;
        Assembly = assembly;
        Compilation = compilation;
        CancellationToken = cancellationToken;
    }

    /// <summary>创建生成器上下文实例</summary>
    public static GeneratorContext Create(
        ISymbolInfo targetSymbol,
        AttributeInfo sourceAttribute,
        ITypeInfo declaringType,
        IAssemblyInfo assembly,
        ICompilationInfo compilation,
        CancellationToken cancellationToken = default)
    {
        return new GeneratorContext(
            targetSymbol,
            sourceAttribute,
            declaringType,
            assembly,
            compilation,
            cancellationToken);
    }

    /// <summary>获取目标符号的全限定名称</summary>
    public string GetFullName()
    {
        return TargetSymbol?.FullName ?? string.Empty;
    }

    /// <summary>获取目标符号所在的命名空间</summary>
    public string GetNamespace()
    {
        return DeclaringType?.Namespace ?? TargetSymbol?.Namespace ?? string.Empty;
    }
}

/// <summary>
/// 符号信息接口。
/// </summary>
public interface ISymbolInfo
{
    string Name { get; }
    string FullName { get; }
    string Namespace { get; }
    string Documentation { get; }
    LocationInfo Location { get; }
}

/// <summary>
/// 符号位置信息。
/// </summary>
public readonly struct LocationInfo
{
    public string FilePath { get; }
    public int Line { get; }
    public int Column { get; }

    public LocationInfo(string filePath, int line, int column)
    {
        FilePath = filePath ?? string.Empty;
        Line = line;
        Column = column;
    }
}

/// <summary>
/// 特性信息接口。
/// </summary>
public interface IAttributeInfo
{
    string TypeName { get; }
    IReadOnlyDictionary<string, object> NamedArguments { get; }
    IReadOnlyList<object> PositionalArguments { get; }
    T GetNamedArgument<T>(string name);
    T GetNamedArgument<T>(string name, T defaultValue);
}

/// <summary>
/// 特性信息实现类。
/// </summary>
public sealed class AttributeInfo : IAttributeInfo
{
    public string TypeName { get; }
    public IReadOnlyDictionary<string, object> NamedArguments { get; }
    public IReadOnlyList<object> PositionalArguments { get; }

    public AttributeInfo(
        string typeName,
        IEnumerable<KeyValuePair<string, object>> namedArguments = null,
        IEnumerable<object> positionalArguments = null)
    {
        TypeName = typeName;
        NamedArguments = namedArguments != null
            ? new Dictionary<string, object>(namedArguments)
            : new Dictionary<string, object>();
        PositionalArguments = positionalArguments != null
            ? new List<object>(positionalArguments)
            : new List<object>();
    }

    public T GetNamedArgument<T>(string name)
    {
        return NamedArguments.TryGetValue(name, out var value) ? (T)value : default;
    }

    public T GetNamedArgument<T>(string name, T defaultValue)
    {
        return NamedArguments.TryGetValue(name, out var value) ? (T)value : defaultValue;
    }
}

/// <summary>
/// 类型信息接口。
/// </summary>
public interface ITypeInfo
{
    string Name { get; }
    string Namespace { get; }
    string FullName { get; }
    bool IsReferenceType { get; }
    bool IsValueType { get; }
    IReadOnlyList<ITypeInfo> TypeArguments { get; }
}

/// <summary>
/// 程序集信息接口。
/// </summary>
public interface IAssemblyInfo
{
    string Name { get; }
    string Version { get; }
    string Culture { get; }
    bool IsDebugBuild { get; }
}

/// <summary>
/// 编译信息接口。
/// </summary>
public interface ICompilationInfo
{
    string Language { get; }
    string LanguageVersion { get; }
    bool IsAnalyzeMode { get; }
}

}