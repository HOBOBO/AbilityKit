namespace AbilityKit.Entitas.CodeGen.Model;

/// <summary>组件字段（类型已解析为全限定名）。</summary>
public sealed class FieldInfo(string typeFqn, string name)
{
    public string TypeFqn { get; } = typeFqn;
    public string Name { get; } = name;

    /// <summary>Jenny 参数命名约定：字段名加 new 前缀。</summary>
    public string ParamName => "new" + Name;
}

/// <summary>从源码扫描出的组件定义（已通过源校验）。</summary>
public sealed class ComponentInfo(string componentName, string classFqn, List<FieldInfo> fields, HashSet<string> contexts, string sourceFile)
{
    /// <summary>组件名（类名去掉 Component 后缀）。</summary>
    public string ComponentName { get; } = componentName;
    public string ClassFqn { get; } = classFqn;
    public List<FieldInfo> Fields { get; } = fields;
    public HashSet<string> Contexts { get; } = contexts;
    public string SourceFile { get; } = sourceFile;
    public bool IsFlag => Fields.Count == 0;
}
